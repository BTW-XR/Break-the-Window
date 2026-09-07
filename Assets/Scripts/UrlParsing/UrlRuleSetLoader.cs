using System;
using Newtonsoft.Json;
using UnityEngine;

public static class UrlRuleSetLoader
{
    public const string DefaultResourcePath = "UrlParsingRules";

    public static UrlRuleSetDefinition LoadDefault()
    {
        TextAsset textAsset = Resources.Load<TextAsset>(DefaultResourcePath);
        if (textAsset == null)
        {
            throw new InvalidOperationException(
                $"Could not load default URL parsing rules from Resources path '{DefaultResourcePath}'. Expected file: Assets/Resources/UrlParsingRules.json"
            );
        }

        return LoadFromTextAsset(textAsset);
    }

    public static UrlRuleSetDefinition LoadFromTextAsset(TextAsset textAsset)
    {
        if (textAsset == null)
        {
            throw new ArgumentNullException(nameof(textAsset));
        }

        return LoadFromJson(textAsset.text);
    }

    public static UrlRuleSetDefinition LoadFromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException("URL rule-set JSON cannot be null or empty.", nameof(json));
        }

        UrlRuleSetDefinition definition;

        try
        {
            definition = JsonConvert.DeserializeObject<UrlRuleSetDefinition>(json);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException("Failed to deserialize URL rule-set JSON.", exception);
        }

        if (definition == null)
        {
            throw new InvalidOperationException("URL rule-set JSON deserialized to null.");
        }

        definition.Rules ??= new System.Collections.Generic.List<UrlSourceRuleDefinition>();
        return definition;
    }
}
