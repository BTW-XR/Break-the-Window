using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Vuplex.WebView;

[ExecuteAlways]
public class CanvasManager : MonoBehaviour
{

    [SerializeField]
    private float zOffset = 0.01f;

    [SerializeField]
    private float width = 1f;

    [SerializeField]
    private float height = 1f;

    [SerializeField]
    private bool forceDimensions = true;

    [SerializeField]
    private CanvasWebViewPrefab webViewPrefab;

    [SerializeField]
    private Transform target;

    [SerializeField]
    private string targetContentId;

    [SerializeField]
    private float resizeEpsilon = 0.0001f;

    private Vector2 lastAppliedSize = new Vector2(-1f, -1f);

    [SerializeField]
    private string url;

    public string Url => url;

    void Start()
    {
        if (webViewPrefab == null)
        {
            webViewPrefab = GetComponentInChildren<CanvasWebViewPrefab>();
        }

        if (webViewPrefab == null)
        {
            Debug.LogError("CanvasManager could not find a CanvasWebViewPrefab in its children.");
        }
    }

    void LateUpdate()
    {
        if (target == null)
        {
            target = FindTargetLeafTransformByContentId();
        }

        ResizeWebView(width, height);

        FollowTarget();
    }

    void FollowTarget()
    {
        if (target != null)
        {
            transform.position = target.position - target.forward * zOffset;
            transform.rotation = target.rotation;
        }
    }

    public Vector2 GetWidthHeight()
    {
        return new Vector2(width, height);
    }

    void ResizeWebView(float width, float height)
    {
        if (forceDimensions && target != null)
        {
            width = target.lossyScale.x;
            height = target.lossyScale.y;
        }

        width = Mathf.Max(0.001f, width);
        height = Mathf.Max(0.001f, height);

        this.width = width;
        this.height = height;

        if (webViewPrefab != null)
        {
            Vector2 requestedSize = new Vector2(width, height);
            if (Vector2.Distance(lastAppliedSize, requestedSize) <= resizeEpsilon)
            {
                return;
            }

            RectTransform rectTransform = webViewPrefab.GetComponent<RectTransform>();
            if (rectTransform == null)
            {
                return;
            }

            rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
            Canvas.ForceUpdateCanvases();

            lastAppliedSize = requestedSize;
        }
    }

    public Transform FindTargetLeafTransformByContentId()
    {
        target = null;

        if (string.IsNullOrWhiteSpace(targetContentId))
        {
            return null;
        }

        // Leaf quad names are generated as: "Leaf_{contentId}_{path}".
        // Match by exact content-id prefix so IDs with shared prefixes do not collide.
        string expectedPrefix = $"Leaf_{targetContentId}_";

        Transform[] allTransforms = FindObjectsByType<Transform>(FindObjectsSortMode.None);
        for (int i = 0; i < allTransforms.Length; i++)
        {
            Transform current = allTransforms[i];
            if (current.gameObject.activeInHierarchy == false)
            {
                continue;
            }

            if (
                current == null
                || !current.name.StartsWith(expectedPrefix, StringComparison.Ordinal)
            )
            {
                continue;
            }

            // Restrict to generated leaf visuals.
            if (
                current.GetComponent<MeshRenderer>() == null
                || current.GetComponent<MeshFilter>() == null
            )
            {
                continue;
            }

            return current;
        }

        return null;
    }

    public string TargetContentId => targetContentId;

    public Transform Target => target;

    public void SetTargetContentId(string contentId)
    {
        targetContentId = contentId;
        target = FindTargetLeafTransformByContentId();
    }

    public async void SetURL(string url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return;
        }

        this.url = url;
        if (DataCollector.Active != null)
        {
            string websiteObjectId = null;
            string moduleObjectId = null;
            DataCollector.Active.TryGetTrackedWebsiteObjectId(this, out websiteObjectId);
            DataCollector.Active.TryGetTrackedWebsiteModuleObjectId(this, out moduleObjectId);
            DataCollector.Active.LogInteraction(
                "website_opened",
                websiteObjectId,
                "Website",
                moduleObjectId,
                url: url,
                contentId: targetContentId);
        }

        CanvasWebViewPrefab webView = GetComponentInChildren<CanvasWebViewPrefab>();

        await webView.WaitUntilInitialized();

        webView.WebView.LoadUrl(url);
        Debug.Log($"CanvasManager set WebView URL to: {url}");
    }

    public void SetURL()
    {
        SetURL(url);
    }

    public string GetURL()
    {
        CanvasWebViewPrefab webView = GetComponentInChildren<CanvasWebViewPrefab>();

        if (webView != null && webView.WebView != null)
        {
            return webView.WebView.Url;
        }

        throw new InvalidOperationException("CanvasManager cannot get URL: WebView is not initialized.");
    }

    public bool TryGetURL(out string resolvedUrl)
    {
        CanvasWebViewPrefab webView = GetComponentInChildren<CanvasWebViewPrefab>();
        if (webView != null && webView.WebView != null && !string.IsNullOrWhiteSpace(webView.WebView.Url))
        {
            resolvedUrl = webView.WebView.Url;
            return true;
        }

        if (!string.IsNullOrWhiteSpace(url))
        {
            resolvedUrl = url;
            return true;
        }

        resolvedUrl = null;
        return false;
    }

    public bool TryGetTitle(out string resolvedTitle)
    {
        CanvasWebViewPrefab webView = GetComponentInChildren<CanvasWebViewPrefab>();
        if (webView != null && webView.WebView != null && !string.IsNullOrWhiteSpace(webView.WebView.Title))
        {
            resolvedTitle = webView.WebView.Title;
            return true;
        }

        resolvedTitle = null;
        return false;
    }

    public static Dictionary<string, string> BuildContentDisplayLookup()
    {
        CanvasManager[] allCanvasManagers = FindObjectsByType<CanvasManager>(FindObjectsSortMode.None);
        Dictionary<string, string> lookup = new Dictionary<string, string>();

        foreach (CanvasManager canvasManager in allCanvasManagers)
        {
            if (canvasManager == null || string.IsNullOrWhiteSpace(canvasManager.TargetContentId))
            {
                continue;
            }

            if (canvasManager.TryGetTitle(out string title))
            {
                lookup[canvasManager.TargetContentId] = title;
            }
            else if (canvasManager.TryGetURL(out string url))
            {
                lookup[canvasManager.TargetContentId] = url;
            }
        }

        return lookup;
    }
}
