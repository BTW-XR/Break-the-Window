#if UNITY_INCLUDE_TESTS
using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed class RelatedUrlParserTests
{
    [Test]
    public void Parse_ReturnsDerivedUrls_ForYoutubeWatchRule()
    {
        RelatedUrlParser parser = new RelatedUrlParser(CreateDefinition());

        RelatedUrlParseResult result = parser.Parse("https://www.youtube.com/watch?v=dqbE2zTSkXE");

        Assert.That(result.IsMatch, Is.True);
        Assert.That(result.MatchedRuleId, Is.EqualTo("youtube-watch"));
        Assert.That(result.RelatedUrls.Count, Is.EqualTo(3));
        Assert.That(result.RelatedUrls.All(url => url.IsSuccess), Is.True);
        Assert.That(result.RelatedUrls[0].Url, Is.EqualTo("https://www.youtube.com/watch?v=dqbE2zTSkXE"));
        Assert.That(result.RelatedUrls[1].Url, Is.EqualTo("https://www.youtube.com/embed/dqbE2zTSkXE"));
        Assert.That(result.RelatedUrls[2].Url, Is.EqualTo("https://img.youtube.com/vi/dqbE2zTSkXE/hqdefault.jpg"));
        Assert.That(result.RelatedUrls[0].ExtractedValues["videoId"], Is.EqualTo("dqbE2zTSkXE"));
    }

    [Test]
    public void Parse_ReturnsExtractedValues_ForMultipleCaptures()
    {
        RelatedUrlParser parser = new RelatedUrlParser(CreateDefinition());

        RelatedUrlParseResult result = parser.Parse("https://example.com/alice/posts/42?tab=comments");

        Assert.That(result.IsMatch, Is.True);
        Assert.That(result.MatchedRuleId, Is.EqualTo("example-post"));
        Assert.That(result.RelatedUrls.Count, Is.EqualTo(2));
        Assert.That(result.RelatedUrls[0].Url, Is.EqualTo("https://api.example.com/users/alice/posts/42/summary"));
        Assert.That(result.RelatedUrls[1].Url, Is.EqualTo("https://www.example.com/alice/posts/42?tab=comments"));
        Assert.That(result.RelatedUrls[0].ExtractedValues["userSlug"], Is.EqualTo("alice"));
        Assert.That(result.RelatedUrls[0].ExtractedValues["postId"], Is.EqualTo("42"));
        Assert.That(result.RelatedUrls[0].ExtractedValues["tab"], Is.EqualTo("comments"));
    }

    [Test]
    public void Parse_ReturnsNonMatch_WhenNoRuleMatches()
    {
        RelatedUrlParser parser = new RelatedUrlParser(CreateDefinition());

        RelatedUrlParseResult result = parser.Parse("https://vimeo.com/123456");

        Assert.That(result.IsMatch, Is.False);
        Assert.That(result.RelatedUrls, Is.Empty);
        Assert.That(result.Errors.Count, Is.EqualTo(1));
    }

    [Test]
    public void Parse_ReturnsStructuredError_ForInvalidUrl()
    {
        RelatedUrlParser parser = new RelatedUrlParser(CreateDefinition());

        RelatedUrlParseResult result = parser.Parse("not-a-url");

        Assert.That(result.IsMatch, Is.False);
        Assert.That(result.Errors.Single(), Does.Contain("valid absolute URL"));
    }

    [Test]
    public void Parse_ReturnsDerivedEntryError_WhenTemplatePlaceholderIsMissing()
    {
        UrlRuleSetDefinition definition = CreateDefinition();
        definition.Rules[0].DerivedUrls.Add(
            new DerivedUrlTemplateDefinition
            {
                Name = "broken",
                Template = "https://www.youtube.com/{missingValue}"
            }
        );

        RelatedUrlParser parser = new RelatedUrlParser(definition);
        RelatedUrlParseResult result = parser.Parse("https://www.youtube.com/watch?v=dqbE2zTSkXE");

        Assert.That(result.IsMatch, Is.True);
        Assert.That(result.RelatedUrls.Count, Is.EqualTo(4));
        RelatedUrlResult brokenResult = result.RelatedUrls.Single(url => url.RelatedName == "broken");
        Assert.That(brokenResult.IsSuccess, Is.False);
        Assert.That(brokenResult.Error, Does.Contain("missingValue"));
        Assert.That(result.Errors.Any(error => error.Contains("missingValue")), Is.True);
    }

    [Test]
    public void Loader_ThrowsForInvalidRegex()
    {
        UrlRuleSetDefinition definition = CreateDefinition();
        definition.Rules[0].MatchPattern = "(?<bad";

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => new RelatedUrlParser(definition));

        Assert.That(exception.Message, Does.Contain("invalid regex pattern"));
    }

    [Test]
    public void Loader_LoadsDefinitionFromTextAssetJson()
    {
        TextAsset textAsset = new TextAsset(
            "{\n" +
            "  \"rules\": [\n" +
            "    {\n" +
            "      \"ruleId\": \"youtube-watch\",\n" +
            "      \"displayName\": \"YouTube Watch\",\n" +
            "      \"supportedHosts\": [\"youtube.com\"],\n" +
            "      \"matchPattern\": \"^https?:\\\\/\\\\/(?:www\\\\.)?youtube\\\\.com\\\\/watch\\\\?(?:.*&)?v=(?<videoId>[A-Za-z0-9_-]+)(?:&.*)?$\",\n" +
            "      \"derivedUrls\": [\n" +
            "        {\n" +
            "          \"name\": \"embed\",\n" +
            "          \"template\": \"https://www.youtube.com/embed/{videoId}\"\n" +
            "        }\n" +
            "      ]\n" +
            "    }\n" +
            "  ]\n" +
            "}"
        );

        UrlRuleSetDefinition definition = UrlRuleSetLoader.LoadFromTextAsset(textAsset);
        RelatedUrlParser parser = new RelatedUrlParser(definition);
        RelatedUrlParseResult result = parser.Parse("https://www.youtube.com/watch?v=dqbE2zTSkXE");

        Assert.That(definition.Rules.Count, Is.EqualTo(1));
        Assert.That(result.IsMatch, Is.True);
        Assert.That(result.RelatedUrls.Single().Url, Is.EqualTo("https://www.youtube.com/embed/dqbE2zTSkXE"));
    }

    private static UrlRuleSetDefinition CreateDefinition()
    {
        return new UrlRuleSetDefinition
        {
            Rules =
            {
                new UrlSourceRuleDefinition
                {
                    RuleId = "youtube-watch",
                    DisplayName = "YouTube Watch",
                    SupportedHosts = { "youtube.com", "www.youtube.com" },
                    MatchPattern = "^https?:\\/\\/(?:www\\.)?youtube\\.com\\/watch\\?(?:.*&)?v=(?<videoId>[A-Za-z0-9_-]+)(?:&.*)?$",
                    DerivedUrls =
                    {
                        new DerivedUrlTemplateDefinition
                        {
                            Name = "watch",
                            Template = "https://www.youtube.com/watch?v={videoId}"
                        },
                        new DerivedUrlTemplateDefinition
                        {
                            Name = "embed",
                            Template = "https://www.youtube.com/embed/{videoId}"
                        },
                        new DerivedUrlTemplateDefinition
                        {
                            Name = "thumbnail",
                            Template = "https://img.youtube.com/vi/{videoId}/hqdefault.jpg"
                        }
                    }
                },
                new UrlSourceRuleDefinition
                {
                    RuleId = "example-post",
                    DisplayName = "Example Multi-Part Post",
                    SupportedHosts = { "example.com" },
                    MatchPattern = "^https?:\\/\\/(?:www\\.)?example\\.com\\/(?<userSlug>[^\\/]+)\\/posts\\/(?<postId>[^\\/?#]+)\\?tab=(?<tab>[^&#]+).*$",
                    DerivedUrls =
                    {
                        new DerivedUrlTemplateDefinition
                        {
                            Name = "summary",
                            Template = "https://api.example.com/users/{userSlug}/posts/{postId}/summary"
                        },
                        new DerivedUrlTemplateDefinition
                        {
                            Name = "tabView",
                            Template = "https://www.example.com/{userSlug}/posts/{postId}?tab={tab}"
                        }
                    }
                }
            }
        };
    }
}
#endif
