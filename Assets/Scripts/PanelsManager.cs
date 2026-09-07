using UnityEngine;
using System;

[ExecuteAlways]
public class PanelsManager : MonoBehaviour
{
    private enum MultiplePanelOffsetStrategy
    {
        Flat,
        Stack
    }

    public static PanelsManager Instance { get; private set; }

    [SerializeField]
    private GameObject modulePrefab;
    [SerializeField]
    private GameObject canvasPrefab;

    [SerializeField]
    private string newContentId;
    [SerializeField]
    private string newURL;
    [SerializeField]
    private Vector3 newPanelPosition = Vector3.zero;

    [SerializeField]
    private Quaternion newPanelRotation = Quaternion.identity;

    [SerializeField]
    private float spawnedPanelDistance = 1.25f;

    [SerializeField]
    private MultiplePanelOffsetStrategy multiplePanelOffsetStrategy = MultiplePanelOffsetStrategy.Flat;

    [SerializeField]
    private float flatPanelsSpacing = 0.45f;

    [SerializeField]
    private Vector3 stackedPanelOffset = new Vector3(0.16f, -0.09f, 0.05f);

    private static string urlPrefix = "http://localhost:5173";
    private UrlLauncher runtimeUrlLauncher;

    public float SpawnedPanelDistance => spawnedPanelDistance;

    private void Init()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }
    }

    private void Awake()
    {
        Init();
    }

    private void Start()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        BindUrlLauncher();
    }

    private void Update()
    {
        if (Instance != this)
        {
            Init();
        }
    }

    public void SetNewContentIdAndURL(string contentId, string url)
    {
        newContentId = contentId;
        newURL = url;
    }

    public void SetNewPanelPosition(Vector3 position)
    {
        newPanelPosition = position;
    }

    public void SetNewPanelRotation(Quaternion rotation)
    {
        newPanelRotation = rotation;
    }

    public void CreatePanel()
    {
        if (modulePrefab == null || canvasPrefab == null)
        {
            Debug.LogError("ModulePrefab or CanvasPrefab is not assigned in PanelsManager.");
            return;
        }

        if (string.IsNullOrEmpty(newContentId))
        {
            Debug.LogError("NewContentId is not set in PanelsManager.");
            return;
        }

        if (string.IsNullOrEmpty(newURL))
        {
            Debug.LogError("NewURL is not set in PanelsManager.");
            return;
        }

        GameObject moduleInstance = Instantiate(modulePrefab);
        GameObject canvasInstance = Instantiate(canvasPrefab);

        ModuleController moduleController = moduleInstance.GetComponent<ModuleController>();
        if (moduleController != null)
        {
            ModuleLayoutTree.Node node = ModuleLayoutTree.Node.CreateLeaf(newContentId);
            moduleController.LayoutTree = ModuleLayoutTree.CreateWithRoot(node);

            moduleInstance.transform.position = newPanelPosition;
            moduleInstance.transform.rotation = newPanelRotation;
            moduleController.TransformCorrection();
        }
        else
        {
            Debug.LogError("ModulePrefab does not have a ModuleController component.");
        }

        moduleController.RefreshAnchoredElements();

        canvasInstance.GetComponent<Canvas>().worldCamera = Camera.main;

        CanvasManager canvasManager = canvasInstance.GetComponentInChildren<CanvasManager>();
        if (canvasManager != null)
        {
            canvasManager.SetURL(ResolvePanelUrl(newURL));
            canvasManager.SetTargetContentId(newContentId);
        }
        else
        {
            Debug.LogError("CanvasPrefab does not have a CanvasManager component.");
        }


        newURL = string.Empty;
        newContentId = string.Empty;
        newPanelPosition = Vector3.zero;
        newPanelRotation = Quaternion.identity;
    }

    public void CreatePanel(string contentId, string url, Vector3 position)
    {
        SetNewContentIdAndURL(contentId, url);
        SetNewPanelPosition(position);
        SetNewPanelRotation(Quaternion.identity);
        CreatePanel();
    }

    public void CreatePanel(string contentId, string url, Vector3 position, Quaternion rotation)
    {
        SetNewContentIdAndURL(contentId, url);
        SetNewPanelPosition(position);
        SetNewPanelRotation(rotation);
        CreatePanel();
    }

    public void CreatePanelFromUrl(string absoluteUrl, Vector3 position, Quaternion rotation)
    {
        if (string.IsNullOrWhiteSpace(absoluteUrl))
        {
            Debug.LogError("Cannot create panel from URL because the URL is empty.");
            return;
        }

        string contentId = $"url-{Guid.NewGuid():N}".Substring(0, 12);
        CreatePanel(contentId, absoluteUrl, position, rotation);
    }

    public bool CreatePanelsFromUrls(string[] absoluteUrls)
    {
        if (absoluteUrls == null)
        {
            Debug.LogError("Cannot create panels because the URL list is null.");
            return false;
        }

        if (absoluteUrls.Length == 0)
        {
            Debug.LogError("Cannot create panels because the URL list is empty.");
            return false;
        }

        if (!GetMultipleOptimalPanelPos(absoluteUrls.Length, out Vector3[] positions, out Quaternion[] rotations))
        {
            Debug.LogError("Cannot create panels because optimal panel positions could not be resolved.");
            return false;
        }

        for (int i = 0; i < absoluteUrls.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(absoluteUrls[i]))
            {
                Debug.LogWarning($"Skipping panel creation for URL at index {i} because it is empty.");
                continue;
            }

            CreatePanelFromUrl(absoluteUrls[i], positions[i], rotations[i]);
        }

        return true;
    }

    public bool GetOptimalPanelPos(out Vector3 position, out Quaternion rotation)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;

        Transform head = ResolveHeadTransform();
        if (head == null)
        {
            return false;
        }

        Vector3 flatForward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
        if (flatForward.sqrMagnitude < 0.0001f)
        {
            flatForward = head.forward;
        }

        flatForward.Normalize();
        position = head.position + flatForward * spawnedPanelDistance;
        rotation = Quaternion.LookRotation(flatForward, Vector3.up);
        return true;
    }

    public bool GetMultipleOptimalPanelPos(int count, out Vector3[] positions, out Quaternion[] rotations)
    {
        if (count < 0)
        {
            positions = Array.Empty<Vector3>();
            rotations = Array.Empty<Quaternion>();
            Debug.LogError("Cannot get multiple optimal panel positions for a negative count.");
            return false;
        }

        positions = new Vector3[count];
        rotations = new Quaternion[count];

        if (count == 0)
        {
            return true;
        }

        if (!GetOptimalPanelPos(out Vector3 basePosition, out Quaternion baseRotation))
        {
            return false;
        }

        positions[0] = basePosition;
        rotations[0] = baseRotation;

        for (int i = 1; i < count; i++)
        {
            positions[i] = basePosition + ResolveMultiplePanelOffset(i, baseRotation);
            rotations[i] = baseRotation;
        }

        return true;
    }

    private void BindUrlLauncher()
    {
        runtimeUrlLauncher = FindFirstObjectByType<UrlLauncher>();
        if (runtimeUrlLauncher != null)
        {
            runtimeUrlLauncher.SetPanelsManager(this);
        }
    }

    private static Transform ResolveHeadTransform()
    {
        if (Camera.main != null)
        {
            return Camera.main.transform;
        }

        Camera anyCamera = FindFirstObjectByType<Camera>();
        return anyCamera != null ? anyCamera.transform : null;
    }

    private static string ResolvePanelUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return string.Empty;
        }

        if (Uri.TryCreate(url, UriKind.Absolute, out Uri absoluteUri))
        {
            return absoluteUri.AbsoluteUri;
        }

        Debug.Log("Resolving relative URL: " + urlPrefix + url);
        return urlPrefix + url;
    }

    private Vector3 ResolveMultiplePanelOffset(int index, Quaternion baseRotation)
    {
        return multiplePanelOffsetStrategy switch
        {
            MultiplePanelOffsetStrategy.Stack => ResolveStackOffset(index, baseRotation),
            _ => ResolveFlatOffset(index, baseRotation),
        };
    }

    private Vector3 ResolveFlatOffset(int index, Quaternion baseRotation)
    {
        Vector3 lateralDirection = baseRotation * Vector3.right;
        int offsetMultiplier = (index + 1) / 2;
        float side = index % 2 == 1 ? 1f : -1f;
        return lateralDirection * (flatPanelsSpacing * offsetMultiplier * side);
    }

    private Vector3 ResolveStackOffset(int index, Quaternion baseRotation)
    {
        return baseRotation * (stackedPanelOffset * index);
    }
}
