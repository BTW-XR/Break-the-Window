using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public partial class ModuleController
{
    #region Corner Resize Smoothing

    [Header("Corner Resize Smoothing")]
    [Tooltip("Smooth the corner handles and snap the panel size to the grid while resizing.")]
    [SerializeField]
    private bool smoothCornerHandles = true;

    [Tooltip("Grid step (meters) the panel dimensions snap to while resizing.")]
    [SerializeField]
    private float gridStep = 0.05f;

    [Tooltip("Minimum panel dimension (meters) enforced while snapping to the grid.")]
    [SerializeField]
    private float cornerMinSize = 0.1f;

    [Tooltip("How quickly the panel content eases to the snapped grid size (1/s).")]
    [SerializeField]
    private float contentSnapRate = 6f;

    [Tooltip("Maximum time (seconds) the corner handle may take to glide back to the grid after release.")]
    [SerializeField]
    private float cornerReleaseMaxTime = 1.5f;

    [Tooltip("How close (meters) the corner handle must be to the grid before detaching after release.")]
    [SerializeField]
    private float cornerReleaseConvergence = 0.005f;

    // Index of the corner handle currently being dragged (0=BL, 1=BR, 2=TR, 3=TL), -1 when idle.
    private int activeResizeCorner = -1;

    // Smoothed proxy that carries the visible handle while a corner is dragged. The raw
    // corner marker stays XRI-driven (hand position); this proxy follows it with a spring
    // + One Euro filter, and it carries a cloned visual so the handle is smoothed too.
    private Transform cornerProxy;
    private Transform cornerProxyVisual;
    private OneEuroFilter3 cornerFilter;
    private Vector3 cornerVelocity;

    // Eased content corner position (plane-local). HandleResize uses this for the dragged
    // corner so the panel dimension glides toward the grid-snapped target.
    private Vector3 contentMovedLocal;

    // During the release glide the proxy eases to the frozen grid position instead of the marker.
    private bool cornerTargetingGrid;
    private Vector3 cornerGridTargetLocal;
    private Coroutine cornerReleaseCoroutine;

    private void WireCornerResizeHandlers()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        Transform[] corners = { bottomLeft, bottomRight, topRight, topLeft };
        for (int i = 0; i < corners.Length; i++)
        {
            if (corners[i] == null)
            {
                continue;
            }

            XRGrabInteractable interactable = corners[i].GetComponent<XRGrabInteractable>();
            if (interactable == null)
            {
                continue;
            }

            int index = i;
            interactable.selectEntered.AddListener(args => OnCornerSelectEntered(index));
            interactable.selectExited.AddListener(args => OnCornerSelectExited(index));
        }
    }

    private void OnCornerSelectEntered(int index)
    {
        if (!Application.isPlaying || !smoothCornerHandles || isGrabbed || !HasRequiredReferences())
        {
            return;
        }

        CancelCornerResize();
        activeResizeCorner = index;

        Transform corner = GetCornerTransform(index);
        if (corner == null)
        {
            activeResizeCorner = -1;
            return;
        }

        MeshRenderer renderer = corner.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.enabled = false;
        }

        cornerProxy = new GameObject("CornerProxy").transform;
        cornerProxy.hideFlags = HideFlags.HideAndDontSave;
        cornerProxy.SetParent(originalParent, false);
        cornerProxy.SetPositionAndRotation(corner.position, corner.rotation);

        cornerProxyVisual = CreateCornerVisualClone(corner, cornerProxy);

        cornerFilter = new OneEuroFilter3(cornerProxy.position, oneEuroMinCutoff, oneEuroBeta);
        cornerVelocity = Vector3.zero;
        cornerTargetingGrid = false;
        cornerGridTargetLocal = ToPlaneSpace(cornerProxy.position);
        contentMovedLocal = cornerGridTargetLocal;
    }

    private void OnCornerSelectExited(int index)
    {
        if (index != activeResizeCorner || cornerProxy == null)
        {
            return;
        }

        Transform fixedCorner = GetCornerTransform((index + 2) % 4);
        Vector3 fixedLocal = fixedCorner != null ? ToPlaneSpace(fixedCorner) : Vector3.zero;
        cornerGridTargetLocal = SnapMovedCorner(ToPlaneSpace(cornerProxy.position), fixedLocal);

        cornerTargetingGrid = true;
        cornerFilter = new OneEuroFilter3(cornerProxy.position, oneEuroMinCutoff, oneEuroBeta);
        cornerReleaseCoroutine = StartCoroutine(ReleaseCornerToGrid(index));
    }

    private IEnumerator ReleaseCornerToGrid(int index)
    {
        float elapsed = 0f;
        while (elapsed < cornerReleaseMaxTime && !(CornerReleaseConverged() && ContentConvergedToGrid()))
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        FinalizeCornerResize(index);
    }

    private bool CornerReleaseConverged()
    {
        if (cornerProxy == null)
        {
            return true;
        }

        Vector3 target = planeReference.TransformPoint(cornerGridTargetLocal);
        float tolerance = cornerReleaseConvergence * cornerReleaseConvergence;
        return (cornerProxy.position - target).sqrMagnitude <= tolerance
            && cornerVelocity.sqrMagnitude <= tolerance;
    }

    // The panel content (contentMovedLocal) must also reach the grid, or it would be cut
    // short if the handle glides back faster than the content can ease.
    private bool ContentConvergedToGrid()
    {
        float tolerance = cornerReleaseConvergence * cornerReleaseConvergence;
        return (contentMovedLocal - cornerGridTargetLocal).sqrMagnitude <= tolerance;
    }

    private void FinalizeCornerResize(int index)
    {
        if (cornerReleaseCoroutine != null)
        {
            StopCoroutine(cornerReleaseCoroutine);
            cornerReleaseCoroutine = null;
        }

        Transform corner = GetCornerTransform(index);
        if (corner != null)
        {
            corner.position = planeReference.TransformPoint(cornerGridTargetLocal);
            MeshRenderer renderer = corner.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.enabled = true;
            }
        }

        if (cornerProxyVisual != null)
        {
            Destroy(cornerProxyVisual.gameObject);
            cornerProxyVisual = null;
        }
        if (cornerProxy != null)
        {
            Destroy(cornerProxy.gameObject);
            cornerProxy = null;
        }

        cornerVelocity = Vector3.zero;
        activeResizeCorner = -1;
        cornerTargetingGrid = false;
        contentMovedLocal = Vector3.zero;

        SavePrevPositions();
    }

    private void CancelCornerResize()
    {
        if (activeResizeCorner < 0)
        {
            return;
        }

        if (cornerReleaseCoroutine != null)
        {
            StopCoroutine(cornerReleaseCoroutine);
            cornerReleaseCoroutine = null;
        }

        Transform corner = GetCornerTransform(activeResizeCorner);
        if (corner != null)
        {
            MeshRenderer renderer = corner.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.enabled = true;
            }
        }

        if (cornerProxyVisual != null)
        {
            Destroy(cornerProxyVisual.gameObject);
            cornerProxyVisual = null;
        }
        if (cornerProxy != null)
        {
            Destroy(cornerProxy.gameObject);
            cornerProxy = null;
        }

        cornerVelocity = Vector3.zero;
        activeResizeCorner = -1;
        cornerTargetingGrid = false;
        contentMovedLocal = Vector3.zero;
        SavePrevPositions();
    }

    // Steps the corner handle proxy toward the raw marker (or the frozen grid target during
    // the release glide) with a spring + One Euro filter, keeping it projected on the plane.
    private void StepCornerSmoothing(float deltaTime)
    {
        if (activeResizeCorner < 0 || cornerProxy == null)
        {
            return;
        }

        Transform corner = GetCornerTransform(activeResizeCorner);
        if (corner == null)
        {
            return;
        }

        Vector3 rawTarget = cornerTargetingGrid
            ? planeReference.TransformPoint(cornerGridTargetLocal)
            : planeReference.TransformPoint(
                new Vector3(ToPlaneSpace(corner).x, ToPlaneSpace(corner).y, 0f)
            );

        Vector3 filtered = cornerFilter.Filter(rawTarget, deltaTime);

        float damping = 2f * Mathf.Sqrt(followStiffness);
        Vector3 acceleration =
            (filtered - cornerProxy.position) * followStiffness - cornerVelocity * damping;
        cornerVelocity += acceleration * deltaTime;
        cornerProxy.position += cornerVelocity * deltaTime;

        Vector3 proxyLocal = ToPlaneSpace(cornerProxy);
        cornerProxy.position = planeReference.TransformPoint(
            new Vector3(proxyLocal.x, proxyLocal.y, 0f)
        );
        cornerProxy.rotation = corner.rotation;
    }

    // Snaps a corner's plane-local position to the grid, keeping the size at least cornerMinSize.
    private Vector3 SnapMovedCorner(Vector3 movedLocal, Vector3 fixedLocal)
    {
        Vector3 offset = movedLocal - fixedLocal;
        Vector3 snapped = new Vector3(
            Mathf.Round(offset.x / gridStep) * gridStep,
            Mathf.Round(offset.y / gridStep) * gridStep,
            0f
        );

        if (Mathf.Abs(snapped.x) < cornerMinSize)
        {
            snapped.x = Mathf.Sign(offset.x) * cornerMinSize;
        }
        if (Mathf.Abs(snapped.y) < cornerMinSize)
        {
            snapped.y = Mathf.Sign(offset.y) * cornerMinSize;
        }

        return fixedLocal + snapped;
    }

    private Transform GetCornerTransform(int index)
    {
        switch (index)
        {
            case 0:
                return bottomLeft;
            case 1:
                return bottomRight;
            case 2:
                return topRight;
            case 3:
                return topLeft;
            default:
                return null;
        }
    }

    private Transform CreateCornerVisualClone(Transform source, Transform parent)
    {
        GameObject clone = new GameObject("CornerHandleVisual");
        clone.transform.SetParent(parent, false);
        clone.transform.localPosition = Vector3.zero;
        clone.transform.localRotation = Quaternion.identity;
        clone.transform.localScale = source.localScale;

        MeshFilter sourceFilter = source.GetComponent<MeshFilter>();
        MeshRenderer sourceRenderer = source.GetComponent<MeshRenderer>();
        if (sourceFilter != null)
        {
            clone.AddComponent<MeshFilter>().sharedMesh = sourceFilter.sharedMesh;
        }
        if (sourceRenderer != null)
        {
            clone.AddComponent<MeshRenderer>().sharedMaterial = sourceRenderer.sharedMaterial;
        }

        return clone.transform;
    }

    #endregion
}
