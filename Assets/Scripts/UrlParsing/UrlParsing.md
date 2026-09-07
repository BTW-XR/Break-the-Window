# URL Parsing Rule Set Guide

This system lets you define a set of source URL rules in JSON, then parse an original URL into one or more related URLs.

By default, the runtime looks for this file:

```text
Assets/Resources/UrlParsingRules.json
```

The main runtime APIs are:

- `UrlRuleSetLoader`
- `RelatedUrlParser`
- `RelatedUrlParseResult`
- `RelatedUrlResult`

## JSON shape

Your JSON file must contain a top-level `rules` array.

Each rule describes:

- `ruleId`: unique ID for the rule
- `displayName`: human-readable name
- `supportedHosts`: optional host filter list
- `matchPattern`: regex used to match the original URL and extract named values
- `derivedUrls`: list of related URL templates to build

Each derived URL entry contains:

- `name`: label for the related URL
- `template`: output URL template using `{captureName}` placeholders

## Full example

```json
{
  "rules": [
    {
      "ruleId": "youtube-watch",
      "displayName": "YouTube Watch",
      "supportedHosts": ["youtube.com", "www.youtube.com"],
      "matchPattern": "^https?:\\/\\/(?:www\\.)?youtube\\.com\\/watch\\?(?:.*&)?v=(?<videoId>[A-Za-z0-9_-]+)(?:&.*)?$",
      "derivedUrls": [
        {
          "name": "watch",
          "template": "https://www.youtube.com/watch?v={videoId}"
        },
        {
          "name": "embed",
          "template": "https://www.youtube.com/embed/{videoId}"
        },
        {
          "name": "thumbnail",
          "template": "https://img.youtube.com/vi/{videoId}/hqdefault.jpg"
        }
      ]
    }
  ]
}
```

## Rule field details

### `supportedHosts`

This is optional.

- If empty or omitted, the rule is allowed to try matching any host.
- If present, the parser checks the input URL host before running the regex.
- A rule host matches both the exact host and subdomains.

Examples:

- `youtube.com` matches `youtube.com`
- `youtube.com` also matches `www.youtube.com`

### `matchPattern`

This must be a valid .NET regular expression.

Use named capture groups to extract values:

```regex
(?<videoId>[A-Za-z0-9_-]+)
```

Those capture names become template variables.

If your URL needs multiple chunks, add multiple named groups:

```regex
^https?:\/\/example\.com\/(?<userSlug>[^\/]+)\/posts\/(?<postId>[^\/?#]+)\?tab=(?<tab>[^&#]+).*$
```

That would create three extracted values:

- `userSlug`
- `postId`
- `tab`

### `template`

Templates use `{name}` placeholders that map directly to named regex captures.

Example:

```text
https://api.example.com/users/{userSlug}/posts/{postId}/summary
```

If a template references a missing placeholder, parsing still succeeds for the rule, but that one derived URL result is returned as a failure with an error message.

## Important JSON authoring notes

- Escape backslashes in JSON regex strings.
- Most regexes that look like `\/` or `\.` in C# regex will become `\\/` or `\\.` in JSON.
- `matchPattern` should match the full normalized absolute URL that `System.Uri.AbsoluteUri` produces.
- The parser uses first-match wins. Put more specific rules before broader rules.
- Input URLs must be absolute URLs such as `https://...`.

## Default rule file

The default rule file name is:

```text
UrlParsingRules.json
```

The default location is:

```text
Assets/Resources/UrlParsingRules.json
```

That location is used by:

- `new RelatedUrlParser()`
- `UrlRuleSetLoader.LoadDefault()`

If the file is missing, those APIs throw.

## Loading a rule set

## Option 1: use the default `Resources` file

This is now the recommended workflow.

Example:

```csharp
using UnityEngine;

public sealed class UrlParsingExample : MonoBehaviour
{
    private RelatedUrlParser parser;

    private void Awake()
    {
        parser = new RelatedUrlParser();
    }
}
```

This loads:

```text
Assets/Resources/UrlParsingRules.json
```

You can also load just the definition:

```csharp
UrlRuleSetDefinition definition = UrlRuleSetLoader.LoadDefault();
RelatedUrlParser parser = new RelatedUrlParser(definition);
```

## Option 2: assign a `TextAsset` in the Inspector

Use this only if you want a non-default rule file.

Example:

```csharp
using UnityEngine;

public sealed class UrlParsingExample : MonoBehaviour
{
    [SerializeField]
    private TextAsset ruleSetJson;

    private RelatedUrlParser parser;

    private void Awake()
    {
        parser = new RelatedUrlParser(ruleSetJson);
    }
}
```

This constructor internally calls:

```csharp
UrlRuleSetLoader.LoadFromTextAsset(ruleSetJson)
```

and precompiles the regex rules once.

## Option 3: load from JSON text yourself

If you already have the JSON string, load it manually:

```csharp
string json = /* read JSON from somewhere */;
UrlRuleSetDefinition definition = UrlRuleSetLoader.LoadFromJson(json);
RelatedUrlParser parser = new RelatedUrlParser(definition);
```

## Option 4: load from `Resources` manually

If you want to load by name at runtime with `Resources.Load<TextAsset>()`, the JSON file must be inside an `Assets/Resources/...` folder.

Example:

```csharp
using UnityEngine;

public sealed class UrlParsingExample : MonoBehaviour
{
    private RelatedUrlParser parser;

    private void Awake()
    {
        TextAsset textAsset = Resources.Load<TextAsset>("UrlParsingRules");
        parser = new RelatedUrlParser(textAsset);
    }
}
```

That would load:

```text
Assets/Resources/UrlParsingRules.json
```

## Parsing a URL

Once you have a parser:

```csharp
RelatedUrlParseResult result = parser.Parse("https://www.youtube.com/watch?v=dqbE2zTSkXE");
```

The parse result contains:

- `OriginalUrl`
- `IsMatch`
- `MatchedRuleId`
- `MatchedRuleDisplayName`
- `RelatedUrls`
- `Errors`

Example:

```csharp
if (!result.IsMatch)
{
    foreach (string error in result.Errors)
    {
        Debug.LogWarning(error);
    }

    return;
}

Debug.Log($"Matched rule: {result.MatchedRuleId}");

foreach (RelatedUrlResult related in result.RelatedUrls)
{
    if (!related.IsSuccess)
    {
        Debug.LogWarning(related.Error);
        continue;
    }

    Debug.Log($"{related.RelatedName}: {related.Url}");
}
```

## Reading extracted values

Each `RelatedUrlResult` includes the extracted values dictionary used to build the templates.

Example:

```csharp
foreach (RelatedUrlResult related in result.RelatedUrls)
{
    if (related.ExtractedValues.TryGetValue("videoId", out string videoId))
    {
        Debug.Log($"Video ID: {videoId}");
    }
}
```

## Failure behavior

### Invalid original URL

If the input is not a valid absolute URL:

- `IsMatch` is `false`
- `RelatedUrls` is empty
- `Errors` contains the reason

### No matching rule

If no rule matches:

- `IsMatch` is `false`
- `RelatedUrls` is empty
- `Errors` contains a no-match message

### Invalid JSON

`UrlRuleSetLoader.LoadFromJson()` throws if:

- the JSON is empty
- deserialization fails
- the JSON deserializes to `null`

### Invalid rule definition

`new RelatedUrlParser(definition)` throws if:

- a rule is missing `ruleId`
- a rule is missing `matchPattern`
- a regex is invalid
- a derived URL entry is missing `name`
- a derived URL entry is missing `template`

## Recommended workflow

1. Start from `Assets/Resources/UrlParsingRules.json`.
2. Edit that file directly unless you need multiple rule sets.
3. Add one rule per source URL type.
4. Use named regex groups for every value you want to reuse.
5. Reference those groups in `derivedUrls[].template`.
6. Construct `new RelatedUrlParser()`.
7. Or load explicitly with `UrlRuleSetLoader.LoadDefault()`.
8. Call `Parse()` for each original URL.
