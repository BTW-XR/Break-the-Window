using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UrlLauncher : MonoBehaviour
{
    [SerializeField]
    private PanelsManager panelsManager;

    [SerializeField]
    private Transform xrHead;

    [SerializeField]
    private string defaultUrlText = "https://";

    [SerializeField]
    private float initOffsetUp = 0.5f;
    [SerializeField]
    private float initOffsetForward = 0.5f;

    [SerializeField]
    private Color statusOkColor = new Color(0.85f, 0.95f, 0.85f, 1f);

    [SerializeField]
    private Color statusErrorColor = new Color(0.95f, 0.65f, 0.65f, 1f);

    [SerializeField]
    private Canvas canvas;

    [SerializeField]
    private TMP_InputField urlInputField;

    [SerializeField]
    private TextMeshProUGUI statusText;

    [SerializeField]
    private Button submitButton;

    private EventTrigger inputFieldEventTrigger;
    private Image inputBackgroundImage;
    private Outline inputFocusOutline;
    private bool hasPlacedInitially;
    private bool listenersBound;
    private RelatedUrlParser relatedUrlParser;
    private bool attemptedRelatedUrlParserLoad;

    private static readonly Color InputIdleColor = new Color(0.16f, 0.18f, 0.22f, 1f);
    private static readonly Color InputFocusedColor = new Color(0.2f, 0.27f, 0.35f, 1f);
    private static readonly Color FocusOutlineIdleColor = new Color(0f, 0f, 0f, 0f);
    private static readonly Color FocusOutlineActiveColor = new Color(0.35f, 0.82f, 1f, 0.95f);

    public void SetPanelsManager(PanelsManager targetPanelsManager)
    {
        panelsManager = targetPanelsManager;
    }

    private void Start()
    {
        ResolveDependencies();
        // PlaceLauncher();
        BindUiReferences();
        BindUiEvents();
        SetCurrentText(defaultUrlText, true);
        SetStatus("Focus the field, type with your keyboard, then press Go.", false);
        SetInputFocusVisual(false);
    }

    public void SubmitCurrentUrl()
    {
        if (panelsManager == null)
        {
            panelsManager = PanelsManager.Instance;
        }

        if (panelsManager == null)
        {
            SetStatus("PanelsManager not found.", true);
            return;
        }

        string enteredUrl = urlInputField != null ? urlInputField.text : string.Empty;
        if (!TryNormalizeUrl(enteredUrl, out string normalizedUrl))
        {
            SetStatus("Enter a valid URL.", true);
            return;
        }

        if (TryGetRelatedUrlParser(out RelatedUrlParser parser))
        {
            RelatedUrlParseResult parseResult = parser.Parse(normalizedUrl);
            if (parseResult.IsMatch)
            {
                if (!TryCreatePanelsFromDerivedUrls(parseResult))
                {
                    SetStatus("Matched a URL parsing rule, but no derived URLs could be launched.", true);
                    return;
                }

                SetCurrentText(normalizedUrl, true);
                SetStatus(
                    $"Spawned {CountSuccessfulDerivedUrls(parseResult)} derived panel(s) for {parseResult.MatchedRuleId}.",
                    false
                );
                EventSystem.current?.SetSelectedGameObject(null);
                return;
            }
        }

        if (!panelsManager.GetOptimalPanelPos(out Vector3 spawnPosition, out Quaternion spawnRotation))
        {
            SetStatus("XR head/camera not found.", true);
            return;
        }

        panelsManager.CreatePanelFromUrl(normalizedUrl, spawnPosition, spawnRotation);
        SetCurrentText(normalizedUrl, true);
        SetStatus($"Spawned panel for {normalizedUrl}", false);
        EventSystem.current?.SetSelectedGameObject(null);
    }

    private void ResolveDependencies()
    {
        if (panelsManager == null)
        {
            panelsManager = PanelsManager.Instance;
        }

        xrHead = ResolveHeadTransform();
    }

    private void BindUiReferences()
    {
        if (canvas == null)
        {
            canvas = GetComponent<Canvas>();
        }

        if (canvas != null)
        {
            canvas.worldCamera = Camera.main;
        }

        if (urlInputField == null)
        {
            urlInputField = GetComponentInChildren<TMP_InputField>(true);
        }

        if (submitButton == null)
        {
            Button[] buttons = GetComponentsInChildren<Button>(true);
            submitButton = buttons.Length > 0 ? buttons[0] : null;
        }

        if (statusText == null)
        {
            TextMeshProUGUI[] texts = GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (TextMeshProUGUI text in texts)
            {
                if (text != null && text.gameObject.name == "Status")
                {
                    statusText = text;
                    break;
                }
            }
        }

        if (urlInputField != null)
        {
            inputBackgroundImage = urlInputField.GetComponent<Image>();
            inputFocusOutline = urlInputField.GetComponent<Outline>();
            inputFieldEventTrigger = urlInputField.GetComponent<EventTrigger>();
        }
    }

    private void BindUiEvents()
    {
        if (listenersBound)
        {
            return;
        }

        if (urlInputField != null)
        {
            urlInputField.onValueChanged.AddListener(_ => SetStatus("Typing into URL field.", false));
            urlInputField.onSubmit.AddListener(_ => SubmitCurrentUrl());
            urlInputField.onSelect.AddListener(_ =>
            {
                SetInputFocusVisual(true);
                SetStatus("URL field focused. Type with your keyboard.", false);
            });
            urlInputField.onDeselect.AddListener(_ =>
            {
                SetInputFocusVisual(false);
                SetStatus("Focus the field, type with your keyboard, then press Go.", false);
            });

            if (inputFieldEventTrigger == null)
            {
                inputFieldEventTrigger = urlInputField.gameObject.AddComponent<EventTrigger>();
            }

            AddEventTrigger(inputFieldEventTrigger, EventTriggerType.PointerClick, _ =>
            {
                ActivateInputField();
                SetInputFocusVisual(true);
            });
        }

        if (submitButton != null)
        {
            submitButton.onClick.AddListener(SubmitCurrentUrl);
        }

        listenersBound = true;
    }

    private Transform ResolveHeadTransform()
    {
        if (xrHead != null)
        {
            return xrHead;
        }

        if (Camera.main != null)
        {
            xrHead = Camera.main.transform;
            return xrHead;
        }

        Camera anyCamera = FindFirstObjectByType<Camera>();
        if (anyCamera != null)
        {
            xrHead = anyCamera.transform;
        }

        return xrHead;
    }

    private void AddEventTrigger(EventTrigger trigger, EventTriggerType type, Action<BaseEventData> callback)
    {
        EventTrigger.Entry entry = new EventTrigger.Entry
        {
            eventID = type
        };
        entry.callback.AddListener(data => callback(data));
        trigger.triggers.Add(entry);
    }

    private void ActivateInputField()
    {
        if (urlInputField == null)
        {
            return;
        }

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(urlInputField.gameObject);
        }

        urlInputField.Select();
        urlInputField.ActivateInputField();
        int length = urlInputField.text != null ? urlInputField.text.Length : 0;
        urlInputField.caretPosition = length;
        urlInputField.stringPosition = length;
    }

    private void SetCurrentText(string nextText, bool updateInputField)
    {
        string currentUrlText = nextText ?? string.Empty;
        if (updateInputField && urlInputField != null)
        {
            urlInputField.SetTextWithoutNotify(currentUrlText);
            urlInputField.caretPosition = currentUrlText.Length;
            urlInputField.stringPosition = currentUrlText.Length;
        }
    }

    private void SetStatus(string message, bool isError)
    {
        if (statusText == null)
        {
            return;
        }

        statusText.text = message;
        statusText.color = isError ? statusErrorColor : statusOkColor;
    }

    private void SetInputFocusVisual(bool isFocused)
    {
        if (inputBackgroundImage != null)
        {
            inputBackgroundImage.color = isFocused ? InputFocusedColor : InputIdleColor;
        }

        if (inputFocusOutline != null)
        {
            inputFocusOutline.effectColor = isFocused ? FocusOutlineActiveColor : FocusOutlineIdleColor;
        }
    }

    private bool TryNormalizeUrl(string rawInput, out string normalizedUrl)
    {
        normalizedUrl = null;
        string trimmed = rawInput?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return false;
        }

        if (!trimmed.Contains("://", StringComparison.Ordinal))
        {
            trimmed = $"https://{trimmed}";
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out Uri uri))
        {
            return false;
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(uri.Host))
        {
            return false;
        }

        normalizedUrl = uri.AbsoluteUri;
        return true;
    }

    private bool TryGetRelatedUrlParser(out RelatedUrlParser parser)
    {
        parser = relatedUrlParser;
        if (parser != null)
        {
            return true;
        }

        if (attemptedRelatedUrlParserLoad)
        {
            return false;
        }

        attemptedRelatedUrlParserLoad = true;

        try
        {
            relatedUrlParser = new RelatedUrlParser();
            parser = relatedUrlParser;
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Unable to load URL parsing rules. Falling back to direct URL launch. {exception.Message}");
            return false;
        }
    }

    private bool TryCreatePanelsFromDerivedUrls(RelatedUrlParseResult parseResult)
    {
        if (parseResult == null)
        {
            return false;
        }

        List<string> derivedUrlsToLaunch = parseResult.RelatedUrls
            .Where(relatedUrl => relatedUrl != null && relatedUrl.IsSuccess && !string.IsNullOrWhiteSpace(relatedUrl.Url))
            .Select(relatedUrl => relatedUrl.Url)
            .ToList();

        if (derivedUrlsToLaunch.Count == 0)
        {
            Debug.LogWarning($"Rule '{parseResult.MatchedRuleId}' matched but produced no launchable derived URLs.");
            return false;
        }

        if (!panelsManager.CreatePanelsFromUrls(derivedUrlsToLaunch.ToArray()))
        {
            SetStatus("Unable to resolve positions for derived URL panels.", true);
            return false;
        }

        return true;
    }

    private static int CountSuccessfulDerivedUrls(RelatedUrlParseResult parseResult)
    {
        if (parseResult == null || parseResult.RelatedUrls == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < parseResult.RelatedUrls.Count; i++)
        {
            RelatedUrlResult relatedUrl = parseResult.RelatedUrls[i];
            if (relatedUrl != null && relatedUrl.IsSuccess && !string.IsNullOrWhiteSpace(relatedUrl.Url))
            {
                count++;
            }
        }

        return count;
    }
}
