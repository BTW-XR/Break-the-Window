using System;
using System.Collections.Generic;
using Newtonsoft.Json;

[Serializable]
public sealed class UrlRuleSetDefinition
{
    [JsonProperty("rules")]
    public List<UrlSourceRuleDefinition> Rules = new List<UrlSourceRuleDefinition>();
}

[Serializable]
public sealed class UrlSourceRuleDefinition
{
    [JsonProperty("ruleId")]
    public string RuleId;

    [JsonProperty("displayName")]
    public string DisplayName;

    [JsonProperty("supportedHosts")]
    public List<string> SupportedHosts = new List<string>();

    [JsonProperty("matchPattern")]
    public string MatchPattern;

    [JsonProperty("derivedUrls")]
    public List<DerivedUrlTemplateDefinition> DerivedUrls = new List<DerivedUrlTemplateDefinition>();
}

[Serializable]
public sealed class DerivedUrlTemplateDefinition
{
    [JsonProperty("name")]
    public string Name;

    [JsonProperty("template")]
    public string Template;
}
