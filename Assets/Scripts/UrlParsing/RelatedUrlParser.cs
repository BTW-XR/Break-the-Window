using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

public sealed class RelatedUrlParser
{
    private static readonly Regex PlaceholderRegex = new Regex(
        @"\{(?<name>[A-Za-z_][A-Za-z0-9_]*)\}",
        RegexOptions.Compiled
    );

    private readonly List<CompiledUrlSourceRule> compiledRules;

    public RelatedUrlParser()
        : this(UrlRuleSetLoader.LoadDefault())
    {
    }

    public RelatedUrlParser(TextAsset textAsset)
        : this(UrlRuleSetLoader.LoadFromTextAsset(textAsset))
    {
    }

    public RelatedUrlParser(UrlRuleSetDefinition definition)
    {
        if (definition == null)
        {
            throw new ArgumentNullException(nameof(definition));
        }

        compiledRules = CompileRules(definition);
    }

    public RelatedUrlParseResult Parse(string originalUrl)
    {
        if (string.IsNullOrWhiteSpace(originalUrl))
        {
            return CreateFailure(originalUrl, "Original URL cannot be null or empty.");
        }

        if (!TryNormalizeUrl(originalUrl, out Uri normalizedUri, out string normalizedUrl, out string error))
        {
            return CreateFailure(originalUrl, error);
        }

        for (int i = 0; i < compiledRules.Count; i++)
        {
            CompiledUrlSourceRule rule = compiledRules[i];
            if (!rule.SupportsHost(normalizedUri.Host))
            {
                continue;
            }

            Match match = rule.MatchRegex.Match(normalizedUrl);
            if (!match.Success)
            {
                continue;
            }

            IReadOnlyDictionary<string, string> extractedValues = ExtractNamedGroups(rule.MatchRegex, match);
            List<RelatedUrlResult> relatedUrls = new List<RelatedUrlResult>(rule.DerivedUrls.Count);
            List<string> errors = new List<string>();

            for (int derivedIndex = 0; derivedIndex < rule.DerivedUrls.Count; derivedIndex++)
            {
                CompiledDerivedUrlTemplate derivedTemplate = rule.DerivedUrls[derivedIndex];
                if (TryExpandTemplate(derivedTemplate.Template, extractedValues, out string expandedUrl, out string templateError))
                {
                    relatedUrls.Add(
                        new RelatedUrlResult(
                            rule.RuleId,
                            rule.DisplayName,
                            derivedTemplate.Name,
                            expandedUrl,
                            extractedValues,
                            true,
                            null
                        )
                    );
                }
                else
                {
                    string derivedError =
                        $"Failed to build related URL '{derivedTemplate.Name}' for rule '{rule.RuleId}': {templateError}";
                    errors.Add(derivedError);
                    relatedUrls.Add(
                        new RelatedUrlResult(
                            rule.RuleId,
                            rule.DisplayName,
                            derivedTemplate.Name,
                            null,
                            extractedValues,
                            false,
                            derivedError
                        )
                    );
                }
            }

            return new RelatedUrlParseResult(
                normalizedUrl,
                true,
                rule.RuleId,
                rule.DisplayName,
                relatedUrls,
                errors
            );
        }

        return CreateFailure(normalizedUrl, "No URL derivation rule matched the provided URL.");
    }

    private static List<CompiledUrlSourceRule> CompileRules(UrlRuleSetDefinition definition)
    {
        List<CompiledUrlSourceRule> rules = new List<CompiledUrlSourceRule>();
        IReadOnlyList<UrlSourceRuleDefinition> sourceRules =
            definition.Rules ?? new List<UrlSourceRuleDefinition>();

        for (int i = 0; i < sourceRules.Count; i++)
        {
            UrlSourceRuleDefinition sourceRule = sourceRules[i];
            if (sourceRule == null)
            {
                throw new InvalidOperationException($"URL rule at index {i} is null.");
            }

            if (string.IsNullOrWhiteSpace(sourceRule.RuleId))
            {
                throw new InvalidOperationException($"URL rule at index {i} is missing ruleId.");
            }

            if (string.IsNullOrWhiteSpace(sourceRule.MatchPattern))
            {
                throw new InvalidOperationException($"URL rule '{sourceRule.RuleId}' is missing matchPattern.");
            }

            Regex matchRegex;
            try
            {
                matchRegex = new Regex(
                    sourceRule.MatchPattern,
                    RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
                );
            }
            catch (ArgumentException exception)
            {
                throw new InvalidOperationException(
                    $"URL rule '{sourceRule.RuleId}' has an invalid regex pattern.",
                    exception
                );
            }

            List<CompiledDerivedUrlTemplate> derivedUrls = new List<CompiledDerivedUrlTemplate>();
            IReadOnlyList<DerivedUrlTemplateDefinition> derivedDefinitions =
                sourceRule.DerivedUrls ?? new List<DerivedUrlTemplateDefinition>();

            for (int derivedIndex = 0; derivedIndex < derivedDefinitions.Count; derivedIndex++)
            {
                DerivedUrlTemplateDefinition derivedDefinition = derivedDefinitions[derivedIndex];
                if (derivedDefinition == null)
                {
                    throw new InvalidOperationException(
                        $"URL rule '{sourceRule.RuleId}' contains a null derived URL definition at index {derivedIndex}."
                    );
                }

                if (string.IsNullOrWhiteSpace(derivedDefinition.Name))
                {
                    throw new InvalidOperationException(
                        $"URL rule '{sourceRule.RuleId}' contains a derived URL with no name at index {derivedIndex}."
                    );
                }

                if (string.IsNullOrWhiteSpace(derivedDefinition.Template))
                {
                    throw new InvalidOperationException(
                        $"URL rule '{sourceRule.RuleId}' contains a derived URL with no template for '{derivedDefinition.Name}'."
                    );
                }

                derivedUrls.Add(new CompiledDerivedUrlTemplate(derivedDefinition.Name, derivedDefinition.Template));
            }

            rules.Add(
                new CompiledUrlSourceRule(
                    sourceRule.RuleId,
                    string.IsNullOrWhiteSpace(sourceRule.DisplayName) ? sourceRule.RuleId : sourceRule.DisplayName,
                    sourceRule.SupportedHosts,
                    matchRegex,
                    derivedUrls
                )
            );
        }

        return rules;
    }

    private static bool TryNormalizeUrl(string originalUrl, out Uri normalizedUri, out string normalizedUrl, out string error)
    {
        normalizedUri = null;
        normalizedUrl = null;
        error = null;

        string trimmedUrl = originalUrl.Trim();
        if (!Uri.TryCreate(trimmedUrl, UriKind.Absolute, out normalizedUri))
        {
            error = "Original URL is not a valid absolute URL.";
            return false;
        }

        normalizedUrl = normalizedUri.AbsoluteUri;
        return true;
    }

    private static IReadOnlyDictionary<string, string> ExtractNamedGroups(Regex regex, Match match)
    {
        Dictionary<string, string> extractedValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string[] groupNames = regex.GetGroupNames();

        for (int i = 0; i < groupNames.Length; i++)
        {
            string groupName = groupNames[i];
            if (int.TryParse(groupName, out _))
            {
                continue;
            }

            Group group = match.Groups[groupName];
            if (group.Success)
            {
                extractedValues[groupName] = group.Value;
            }
        }

        return new ReadOnlyDictionary<string, string>(extractedValues);
    }

    private static bool TryExpandTemplate(
        string template,
        IReadOnlyDictionary<string, string> extractedValues,
        out string expandedTemplate,
        out string error
    )
    {
        error = null;
        List<string> missingPlaceholders = new List<string>();

        expandedTemplate = PlaceholderRegex.Replace(
            template,
            match =>
            {
                string placeholderName = match.Groups["name"].Value;
                if (extractedValues.TryGetValue(placeholderName, out string value))
                {
                    return value;
                }

                missingPlaceholders.Add(placeholderName);
                return match.Value;
            }
        );

        if (missingPlaceholders.Count > 0)
        {
            error = $"Missing extracted values for: {string.Join(", ", missingPlaceholders.Distinct())}.";
            return false;
        }

        return true;
    }

    private static RelatedUrlParseResult CreateFailure(string originalUrl, string error)
    {
        return new RelatedUrlParseResult(
            originalUrl,
            false,
            null,
            null,
            Array.Empty<RelatedUrlResult>(),
            new[] { error }
        );
    }

    private sealed class CompiledUrlSourceRule
    {
        private readonly string[] supportedHosts;

        public string RuleId { get; }
        public string DisplayName { get; }
        public Regex MatchRegex { get; }
        public IReadOnlyList<CompiledDerivedUrlTemplate> DerivedUrls { get; }

        public CompiledUrlSourceRule(
            string ruleId,
            string displayName,
            IEnumerable<string> supportedHosts,
            Regex matchRegex,
            IReadOnlyList<CompiledDerivedUrlTemplate> derivedUrls
        )
        {
            RuleId = ruleId;
            DisplayName = displayName;
            this.supportedHosts = supportedHosts?
                .Where(host => !string.IsNullOrWhiteSpace(host))
                .Select(host => host.Trim().ToLowerInvariant())
                .Distinct()
                .ToArray() ?? Array.Empty<string>();
            MatchRegex = matchRegex;
            DerivedUrls = derivedUrls ?? Array.Empty<CompiledDerivedUrlTemplate>();
        }

        public bool SupportsHost(string host)
        {
            if (supportedHosts.Length == 0)
            {
                return true;
            }

            string normalizedHost = (host ?? string.Empty).Trim().ToLowerInvariant();
            for (int i = 0; i < supportedHosts.Length; i++)
            {
                string supportedHost = supportedHosts[i];
                if (
                    string.Equals(normalizedHost, supportedHost, StringComparison.OrdinalIgnoreCase)
                    || normalizedHost.EndsWith("." + supportedHost, StringComparison.OrdinalIgnoreCase)
                )
                {
                    return true;
                }
            }

            return false;
        }
    }

    private sealed class CompiledDerivedUrlTemplate
    {
        public string Name { get; }
        public string Template { get; }

        public CompiledDerivedUrlTemplate(string name, string template)
        {
            Name = name;
            Template = template;
        }
    }
}
