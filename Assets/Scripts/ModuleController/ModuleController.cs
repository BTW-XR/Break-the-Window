using UnityEngine;

/// <summary>
/// Core update loop. Functional behavior is grouped into partials by feature:
/// Move, Resize, Merge, and Split.
/// </summary>
[ExecuteAlways]
public partial class ModuleController : MonoBehaviour
{
    private void OnValidate()
    {
        if (!HasRequiredReferences())
        {
            return;
        }

        UpdateAnchoredElements();
    }

    private void Start()
    {
        SavePrevPositions();
        PlacementStart();
    }

    private void Update()
    {

        if (!HasRequiredReferences())
        {
            Debug.LogError("Cannot update ModuleController: missing references.");
            ClearMergePreview();
            return;
        }

        if (doResize && !isGrabbed)
        {
            HandleResize();
        }

        if (doMerge && (isGrabbed || isResizing))
        {
            UpdateMergePreview();
        }
        else
        {
            ClearMergePreview();
        }

        UpdateSurfacePlacementVisuals();

        SavePrevPositions();
    }

    private void LateUpdate()
    {
        TransformCorrection();
    }
}
