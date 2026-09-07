using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

[ExecuteAlways]
public class ContentListManager : MonoBehaviour
{
    private ModuleController moduleController;
    private ModuleLayoutTree layoutTree;
    private IReadOnlyList<string> lstContentIds;

    [SerializeField]
    private bool needRegenerateView = false;

    [SerializeField]
    private Canvas contentIdCanvas;

    [SerializeField]
    private TextMeshProUGUI contentIdText;

    [SerializeField]
    private GameObject popButtonPrefab;

    [SerializeField]
    private Vector3 popButtonOffset = Vector3.zero;

    [SerializeField]
    private bool alwaysRefresh = false;

    [SerializeField]
    private bool showUrl = false;

    [SerializeField]
    private int maxDisplayCharacters = 32;

    private string lastRenderedContentIdsText;

    public void Reset()
    {
        ClearPopButtons();
        FindDependencies();
        needRegenerateView = true;
    }

    public void FindDependencies()
    {
        moduleController = GetComponent<ModuleController>();
        if (moduleController == null)
        {
            Debug.LogError("ContentListManager requires a ModuleController component on the same GameObject.");
            layoutTree = null;
            return;
        }

        layoutTree = moduleController.LayoutTree;
        if (layoutTree == null)
        {
            Debug.LogError("ContentListManager requires a ModuleLayoutTree component on the same GameObject.");
        }

        if (contentIdCanvas == null)
        {
            Debug.LogWarning("ContentListManager: contentIdCanvas is not assigned.");
        }
        contentIdCanvas.GetComponent<Canvas>().worldCamera = Camera.main;
    }

    public void OnValidate()
    {
        Reset();
    }

    public void Start()
    {
        Reset();
    }

    public void RegenerateView()
    {
        ClearPopButtons();
        if (contentIdText == null)
        {
            return;
        }

        contentIdText.text = string.Empty;
        lstContentIds = null;
        lastRenderedContentIdsText = null;
    }

    private void Update()
    {
        if (layoutTree == null || moduleController == null)
        {
            FindDependencies();
        }

        if (moduleController == null || layoutTree == null || contentIdText == null)
        {
            Debug.LogWarning("ContentListManager is missing required dependencies.");
            return;
        }

        if (needRegenerateView)
        {
            RegenerateView();
            needRegenerateView = false;
        }

        UpdateContentIdListView();
    }

    private void UpdateContentIdListView()
    {
        IReadOnlyList<string> nextContentIds = layoutTree?.SplittableContentIds;
        if (nextContentIds == null)
        {
            return;
        }

        string nextRenderedText = string.Join("\n", BuildDisplayRows(nextContentIds));
        if (!alwaysRefresh && nextRenderedText == lastRenderedContentIdsText)
        {
            return;
        }

        lstContentIds = nextContentIds;
        contentIdText.text = nextRenderedText;
        lastRenderedContentIdsText = nextRenderedText;
        contentIdText.ForceMeshUpdate();

        var textInfo = contentIdText.textInfo;
        Vector3[] rowCenters = new Vector3[textInfo.lineCount];
        float[] rowWidths = new float[textInfo.lineCount];
        for (int rowIndex = 0; rowIndex < textInfo.lineCount; rowIndex++)
        {
            var lineInfo = textInfo.lineInfo[rowIndex];
            int firstCharIndex = lineInfo.firstCharacterIndex;
            int lastCharIndex = lineInfo.lastCharacterIndex;
            if (firstCharIndex < 0 || lastCharIndex < 0)
            {
                continue;
            }

            Vector3 bottomLeft = textInfo.characterInfo[firstCharIndex].bottomLeft;
            Vector3 bottomRight = textInfo.characterInfo[lastCharIndex].bottomRight;
            Vector3 topLeft = textInfo.characterInfo[firstCharIndex].topLeft;
            rowCenters[rowIndex] = (bottomRight + topLeft) * 0.5f;
            rowWidths[rowIndex] = Vector3.Distance(bottomLeft, bottomRight);
        }

        ClearPopButtons();

        for (int i = 0; i < lstContentIds.Count && i < rowCenters.Length; i++)
        {
            var popButtonInstance = Instantiate(popButtonPrefab, contentIdText.transform);
            popButtonInstance.transform.localPosition = rowCenters[i] + popButtonOffset;
            popButtonInstance.transform.localRotation = Quaternion.identity;

            Vector3 localScale = popButtonInstance.transform.localScale;
            popButtonInstance.transform.localScale = new Vector3(rowWidths[i], localScale.y, localScale.z);

            var popButtonScript = popButtonInstance.GetComponent<PopButton>();
            if (popButtonScript != null)
            {
                string contentId = lstContentIds[i];
                popButtonScript.Init(contentId, moduleController.CreateModuleFromDebugContentIdPop);
            }
        }
    }

    private IEnumerable<string> BuildDisplayRows(IReadOnlyList<string> contentIds)
    {
        if (!showUrl)
        {
            return contentIds;
        }

        Dictionary<string, string> contentDisplay = CanvasManager.BuildContentDisplayLookup();
        return contentIds.Select(contentId =>
            contentDisplay.TryGetValue(contentId, out string text) && !string.IsNullOrWhiteSpace(text)
                ? TruncateDisplayText(text)
                : contentId
        );
    }

    private string TruncateDisplayText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        if (maxDisplayCharacters <= 0 || text.Length <= maxDisplayCharacters)
        {
            return text;
        }

        return text.Substring(0, maxDisplayCharacters) + "...";
    }

    private void ClearPopButtons()
    {
        Transform popButtonParent = contentIdText?.transform;
        GameObject[] existingButtons = new GameObject[popButtonParent.childCount];
        for (int i = 0; i < existingButtons.Length; i++)
        {
            existingButtons[i] = popButtonParent.GetChild(i).gameObject;
        }

        foreach (GameObject popButton in existingButtons)
        {
            if (popButton != null)
            {
                Destroy(popButton);
            }
        }
    }
}
