using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

public sealed class RelatedUrlParseResult
{
    public string OriginalUrl { get; }
    public bool IsMatch { get; }
    public string MatchedRuleId { get; }
    public string MatchedRuleDisplayName { get; }
    public IReadOnlyList<RelatedUrlResult> RelatedUrls { get; }
    public IReadOnlyList<string> Errors { get; }

    public RelatedUrlParseResult(
        string originalUrl,
        bool isMatch,
        string matchedRuleId,
        string matchedRuleDisplayName,
        IReadOnlyList<RelatedUrlResult> relatedUrls,
        IReadOnlyList<string> errors
    )
    {
        OriginalUrl = originalUrl ?? string.Empty;
        IsMatch = isMatch;
        MatchedRuleId = matchedRuleId;
        MatchedRuleDisplayName = matchedRuleDisplayName;
        RelatedUrls = relatedUrls ?? Array.Empty<RelatedUrlResult>();
        Errors = errors ?? Array.Empty<string>();
    }
}

public sealed class RelatedUrlResult
{
    public string RuleId { get; }
    public string RuleDisplayName { get; }
    public string RelatedName { get; }
    public string Url { get; }
    public IReadOnlyDictionary<string, string> ExtractedValues { get; }
    public bool IsSuccess { get; }
    public string Error { get; }

    public RelatedUrlResult(
        string ruleId,
        string ruleDisplayName,
        string relatedName,
        string url,
        IReadOnlyDictionary<string, string> extractedValues,
        bool isSuccess,
        string error
    )
    {
        RuleId = ruleId;
        RuleDisplayName = ruleDisplayName;
        RelatedName = relatedName;
        Url = url;
        ExtractedValues = extractedValues ?? new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());
        IsSuccess = isSuccess;
        Error = error;
    }
}
