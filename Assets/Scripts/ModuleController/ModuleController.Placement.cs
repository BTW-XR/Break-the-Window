using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using Meta.XR;
using MRMotifs.InstantContentPlacement.Placement;

/// <summary>
/// Surface placement (XRI port of Meta's Instant Content Placement motif) folded into
/// ModuleController. Sequence on grab release:
///   1. Prefer merging (TryConfirmMerge).
///   2. Else snap the module to the MRUK-detected surface beneath it.
///   3. Else release immediately (OnGrabberRelease).
/// OnGrabberRelease runs once the placement animation completes.
///
/// WIRING (Inspector on ModuleController):
///   - Placement Interactable: the module's XRGrabInteractable (drives selectEntered/selectExited).
///   - Line Indicator Prefab: Assets/MRMotifs/InstantContentPlacement/Prefabs/LineIndicator.prefab
///   - An "Environment Raycast Manager" prefab must exist in the scene.
/// </summary>
public partial class ModuleController
{
    #region Surface Placement

    [Header("Surface Placement Settings")]
    [SerializeField]
    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable placementInteractable;

    [SerializeField] private bool useHandAsFakeTable = true;
    [SerializeField] private Transform raycastOrigin;
    [SerializeField] private float placementDistance = 1.0f;
    [SerializeField] private float hoverDistance = 0.1f;
    [SerializeField] private GameObject lineIndicatorPrefab;
    [SerializeField] private bool showLineIndicator = true;

    private bool m_isGrabbed;

    private const float PLACEMENT_SMOOTH_TIME = 1.5f;
    private const float HIT_POINT_LAG_SMOOTH_TIME = 0.15f;

    private bool m_surfacePlacementSupported = true;

    private Vector3 m_hitPoint;
    private Vector3 m_targetPosition;
    private Vector3 m_laggedHitPoint;
    private Quaternion m_targetRotation;

    private LineRenderer m_lineRenderer;
    private Coroutine m_placementCoroutine;
    private EnvironmentRaycastManager m_raycastManager;

    private void PlacementStart()
    {
        m_raycastManager = FindAnyObjectByType<EnvironmentRaycastManager>();

        m_surfacePlacementSupported = ValidateSurfacePlacementSupport();

        if (showLineIndicator && lineIndicatorPrefab)
        {
            InitializePlacementLineRenderer();
        }
    }

    /// <summary>
    /// Surface placement needs the MRUK environment raycast / Depth API. When it is unavailable
    /// (no Environment Raycast Manager in the scene, or depth/spatial data not supported) the
    /// placement features are disabled so grab/move/merge still work without crashing.
    /// </summary>
    private bool ValidateSurfacePlacementSupport()
    {
        if (useHandAsFakeTable)
        {
            return FakeTableManager.instance != null;
        }

        if (m_raycastManager == null)
        {
            Debug.LogError(
                "Surface placement disabled: 'Environment Raycast Manager' prefab is not in the scene. " +
                "Drag in Assets/MRMotifs/InstantContentPlacement/Prefabs/Environment Raycast Manager.prefab."
            );
            return false;
        }

        if (!EnvironmentRaycastManager.IsSupported)
        {
            Debug.LogError(
                "Surface placement disabled: environment raycast / Depth API is unavailable. Enable the " +
                "OpenXR 'Meta Quest: Occlusion' feature (Project Settings > XR Plug-in Management > OpenXR > " +
                "OpenXR Feature Groups > Meta Quest), complete Space Setup, and enable spatial data " +
                "'(Settings > Developer > Spatial Data over Meta Quest Link)' or the on-device Spatial Data permission."
            );
            return false;
        }

        Debug.Log("Surface placement enabled: environment raycast / Depth API is available.");
        return true;
    }

    private void OnSurfacePlacementSelect(SelectEnterEventArgs args)
    {
        m_isGrabbed = true;

        if (m_placementCoroutine != null)
        {
            StopCoroutine(m_placementCoroutine);
        }

        OnGrabberGrab();
    }

    private void OnSurfacePlacementUnselect(SelectExitEventArgs args)
    {
        m_isGrabbed = false;
        UpdatePlacementIndicatorVisibility(false);
        Debug.Log("\n\n\n--------------------------------- Placement ---------------------------------\n\n\n");

        // 1) Prefer merging: if a valid merge preview exists, commit the merge and stop.
        if (TryConfirmMerge())
        {
            Debug.Log("Placement: confirmed merge on grab release.");
            return;
        }

        // 2) No merge -> try surface placement (only when the Depth API is supported).
        //    If it starts, SmoothMoveToTarget calls OnGrabberRelease once its animation completes.
        if (m_surfacePlacementSupported && PerformSurfaceRaycastAndSnap())
        {
            Debug.Log("Placement: surface placement started on grab release.");
            return;
        }
        else
        {
            Debug.Log("Placement: m_surfacePlacementSupported=" + m_surfacePlacementSupported);
        }

        // 3) Nothing to place on (or placement unsupported) -> release immediately.
        OnGrabberRelease();
    }

    private void InitializePlacementLineRenderer()
    {
        m_lineRenderer = Instantiate(lineIndicatorPrefab).GetComponent<LineRenderer>();
        m_lineRenderer.enabled = false;
    }

    private bool PerformSurfaceRaycastAndSnap()
    {
        Vector3 hitPoint;
        if (useHandAsFakeTable)
        {
            if (!FakeTableManager.instance.TableValid)
            {
                return false;
            }
            hitPoint = FakeTableManager.instance.TablePosition;
            m_targetPosition = FakeTableManager.instance.TablePosition;
            m_targetRotation = FakeTableManager.instance.GetOrientation();
        }
        else
        {

            Ray ray = new Ray(raycastOrigin.position, Vector3.down);
            if (!m_raycastManager.Raycast(ray, out var hitInfo))
            {
                return false;
            }

            hitPoint = hitInfo.point;
            m_targetPosition = new Vector3(hitPoint.x, hitPoint.y + hoverDistance, hitPoint.z);
            m_targetRotation = Quaternion.Euler(0, raycastOrigin.rotation.eulerAngles.y, 0);
        }

        if (Vector3.Distance(raycastOrigin.position, hitPoint) >= placementDistance)
        {
            return false;
        }

        m_placementCoroutine = StartCoroutine(SmoothMoveToTarget());
        return true;
    }

    private IEnumerator SmoothMoveToTarget()
    {
        var elapsedTime = 0f;

        var initialPosition = raycastOrigin.position;
        var initialRotation = raycastOrigin.rotation;

        while (elapsedTime < PLACEMENT_SMOOTH_TIME)
        {
            elapsedTime += Time.deltaTime;

            var t = elapsedTime / PLACEMENT_SMOOTH_TIME;
            var easedT = EaseOutExpo(t);

            raycastOrigin.position = Vector3.Lerp(initialPosition, m_targetPosition, easedT);
            raycastOrigin.rotation = Quaternion.Slerp(initialRotation, m_targetRotation, easedT);

            yield return null;
        }

        raycastOrigin.position = m_targetPosition;
        raycastOrigin.rotation = m_targetRotation;

        OnGrabberRelease();
    }

    private static float EaseOutExpo(float x)
    {
        return Mathf.Approximately(x, 1) ? 1 : 1 - Mathf.Pow(2, -10 * x);
    }

    // Invoked from ModuleController.Update so the placement indicator animates while grabbed.
    private void UpdateSurfacePlacementVisuals()
    {
        if (!m_surfacePlacementSupported || !m_isGrabbed)
        {
            return;
        }

        UpdatePlacementIndicator();

        if (showLineIndicator)
        {
            UpdateLineRenderer();
        }
    }


    private void UpdatePlacementIndicator()
    {
        bool validHitPoint;

        if (useHandAsFakeTable)
        {
            m_hitPoint = FakeTableManager.instance.TablePosition;
            validHitPoint = FakeTableManager.instance.TableValid;
        }
        else
        {
            Ray ray = new Ray(raycastOrigin.position, Vector3.down);
            validHitPoint = m_raycastManager.Raycast(ray, out var hitInfo);

            m_hitPoint = hitInfo.point;
        }

        if (!validHitPoint)
        {
            UpdatePlacementIndicatorVisibility(false);
            return;
        }

        float distanceToSurface = Vector3.Distance(raycastOrigin.position, m_hitPoint);

        if (distanceToSurface >= hoverDistance && distanceToSurface < placementDistance)
        {
            UpdatePlacementIndicatorVisibility(true);
        }
        else
        {
            UpdatePlacementIndicatorVisibility(false);
        }
    }


    private void UpdateLineRenderer()
    {
        if (m_lineRenderer == null)
        {
            return;
        }

        var bottomPosition = raycastOrigin.position;

        m_laggedHitPoint = Vector3.Lerp(m_laggedHitPoint, m_hitPoint, Time.deltaTime / HIT_POINT_LAG_SMOOTH_TIME);

        m_lineRenderer.SetPosition(0, m_laggedHitPoint);
        m_lineRenderer.SetPosition(1, bottomPosition);
    }

    private void UpdatePlacementIndicatorVisibility(bool isVisible)
    {
        if (m_lineRenderer)
        {
            m_lineRenderer.enabled = isVisible;
        }
    }

    #endregion
}
