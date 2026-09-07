using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public class UrlDisplayController : MonoBehaviour
{
    [SerializeField]
    private TextMeshProUGUI urlText;

    private ModuleController moduleController;

    void Start()
    {
        moduleController = GetComponent<ModuleController>();
        if (moduleController == null)
        {
            Debug.LogError("UrlDisplayController requires a ModuleController on the same GameObject.");
        }

        if (urlText == null)
        {
            urlText = GetComponentInChildren<TextMeshProUGUI>();
        }
    }

    void Update()
    {
        if (Application.isPlaying == false)
        {
            return;
        }

        if (urlText == null || moduleController?.LayoutTree == null)
        {
            return;
        }

        List<string> leafContentIds = moduleController.LayoutTree.CollectLeafContentIds();

        Dictionary<string, string> contentDisplay = CanvasManager.BuildContentDisplayLookup();
        HashSet<string> uniqueLabels = new HashSet<string>(
            leafContentIds.Where(contentId => !string.IsNullOrWhiteSpace(contentId)
                && contentDisplay.TryGetValue(contentId, out string label) && !string.IsNullOrWhiteSpace(label))
                .Select(contentId => contentDisplay[contentId])
        );

        if (uniqueLabels.Count == 0)
        {
            urlText.text = "";
        }
        else if (uniqueLabels.Count == 1)
        {
            urlText.text = uniqueLabels.First();
        }
        else
        {
            urlText.text = "Multiple";
        }
    }
}
