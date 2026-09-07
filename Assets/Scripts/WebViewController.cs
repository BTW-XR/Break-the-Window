using UnityEngine;
using Vuplex.WebView;

[ExecuteAlways]
public class WebViewController : MonoBehaviour
{
    [SerializeField]
    private Transform anchor;

    [SerializeField]
    private WebViewPrefab webViewPrefab;

    [SerializeField]
    private float width = 1.0f;

    [SerializeField]
    private float height = 1.0f;

    private Vector3 offset = Vector3.zero;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        webViewPrefab = GetComponent<WebViewPrefab>();

        offset = new Vector3(0.5f, 0, 0);

        if (webViewPrefab != null)
        {
            SetTransparentBackground();
        }
    }

    async void SetTransparentBackground()
    {
        await webViewPrefab.WaitUntilInitialized();
        webViewPrefab.WebView.SetDefaultBackgroundEnabled(false);
    }

    // Update is called once per frame
    void Update()
    {
        if (Application.isPlaying)
        {
            ResizeWebView();
        }
        else
        {
            offset = Vector3.zero;
        }

        FollowAnchor();
        Debug.Log($"Anchor position: {anchor.position}, WebView position: {transform.position}");
        width = anchor.localScale.x;
        height = anchor.localScale.y;
    }

    void FollowAnchor()
    {
        if (anchor != null)
        {
            transform.position = anchor.position + anchor.rotation * offset;
            transform.rotation = anchor.rotation;
        }
    }

    void ResizeWebView()
    {
        if (webViewPrefab != null && width > 0 && height > 0 && anchor.hasChanged)
        {
            // Debug.Log($"Resizing WebView to {width} x {height}");
            webViewPrefab.Resize(width, height);

            offset = new Vector3(0.5f * width, 0, 0);
        }
    }
}
