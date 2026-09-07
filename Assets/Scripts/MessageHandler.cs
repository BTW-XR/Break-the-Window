using UnityEngine;
using Vuplex.WebView;
using System;

[Serializable]
public class MapsInfoMessage
{
    public string type;
    public string placeId;
    public string name;
    public float lat;
    public float lng;
    public string url;
}

public class MessageHandler : MonoBehaviour
{
    CanvasWebViewPrefab webViewPrefab;

    async void Start()
    {
        webViewPrefab = GetComponentInChildren<CanvasWebViewPrefab>();
        // Wait for the WebViewPrefab to initialize because the WebViewPrefab.WebView property
        // is null until the prefab has initialized.
        await webViewPrefab.WaitUntilInitialized();
        // Add a handler to the IWebView.MessageEmitted event.
        webViewPrefab.WebView.MessageEmitted += OnWebViewMessageEmitted;
    }

    private void OnWebViewMessageEmitted(object sender, EventArgs<string> args)
    {
        string jsonMessage = args.Value;
        Debug.Log($"Received message from WebView: {jsonMessage}");


        MapsInfoMessage data = JsonUtility.FromJson<MapsInfoMessage>(jsonMessage);

        if (data.type == "open-maps-info-tab")
        {
            HandleOpenMapsTab(data);
        }

    }

    private void HandleOpenMapsTab(MapsInfoMessage data)
    {
        if (string.IsNullOrEmpty(data.url) || string.IsNullOrEmpty(data.placeId))
        {
            Debug.LogError("Received open-maps-info-tab message with missing URL or place ID.");
            return;
        }

        if (PanelsManager.Instance != null)
        {
            if (!PanelsManager.Instance.GetOptimalPanelPos(out Vector3 panelPosition, out Quaternion panelRotation))
            {
                Debug.LogError("Unable to resolve XR head/camera for panel spawn.");
                return;
            }

            PanelsManager.Instance.CreatePanel(data.placeId, data.url, panelPosition, panelRotation);
        }
        else
        {
            Debug.LogError("PanelsManager instance not found. Cannot create panel.");
        }
    }
}
