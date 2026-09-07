using System.Collections.Generic;
using UnityEngine;

public partial class ModuleController
{
    public void DestroyModule()
    {
        ClearMergePreview();

        List<string> contentIds = layoutTree.CollectLeafContentIds();

        CanvasManager[] allCanvasManagers = FindObjectsByType<CanvasManager>(FindObjectsSortMode.None);
        foreach (CanvasManager canvasManager in allCanvasManagers)
        {
            if (canvasManager.TargetContentId != null && contentIds.Contains(canvasManager.TargetContentId))
            {
                if (DataCollector.Active != null)
                {
                    canvasManager.TryGetURL(out string resolvedUrl);
                    string websiteObjectId = null;
                    string moduleObjectId = null;
                    DataCollector.Active.TryGetTrackedWebsiteObjectId(canvasManager, out websiteObjectId);
                    DataCollector.Active.TryGetTrackedWebsiteModuleObjectId(canvasManager, out moduleObjectId);
                    DataCollector.Active.LogInteraction(
                        "website_closed",
                        websiteObjectId,
                        "Website",
                        moduleObjectId,
                        url: resolvedUrl,
                        contentId: canvasManager.TargetContentId);
                }

                Destroy(canvasManager.gameObject);
            }
        }

        Destroy(gameObject);
    }
}
