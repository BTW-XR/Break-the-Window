using NUnit.Framework;
using UnityEngine;

public partial class ModuleController
{
    #region Split
    public ModuleLayoutTree GetLayoutTreeCloneOrDefault()
    {
        if (layoutTree == null)
        {
            layoutTree = ModuleLayoutTree.CreateDefault();
        }
        layoutTree.Validate();
        return layoutTree.DeepClone();
    }

    public void SetLayoutTree(ModuleLayoutTree newLayoutTree)
    {
        layoutTree = newLayoutTree ?? ModuleLayoutTree.CreateDefault();
        layoutTree.Validate();
    }

    public void RegenerateLayout()
    {
        if (layoutTree == null)
        {
            layoutTree = ModuleLayoutTree.CreateDefault();
        }
        layoutTree.Validate();
        ModuleLayoutGenerator generator = GetLayoutGenerator(true);
        if (generator != null)
        {
            generator.Regenerate(layoutTree);
        }

        if (isGrabbed)
        {
            OnGrabberGrab();
        }
    }


    /// <summary>
    /// Test helper: creates a new module using a cloned subtree
    /// identified by <see cref="debugLookupContentId"/>.
    /// The spawned module is reset to default dimensions and then regenerated
    /// from the copied layout subtree, then moved to a relative offset from this module's content.
    /// </summary>
    public ModuleController CreateModuleFromDebugContentIdCopy()
    {
        if (layoutTree == null)
        {
            layoutTree = ModuleLayoutTree.CreateDefault();
        }
        layoutTree.Validate();
        if (!layoutTree.ContainsSplittableContentId(debugLookupContentId))
        {
            Debug.LogError(
                $"Cannot create module: content id '{debugLookupContentId}' is not in the splittable content-id list."
            );
            return null;
        }

        if (
            !layoutTree.TryGetSubtreeCloneByContentId(
                debugLookupContentId,
                out ModuleLayoutTree copy
            )
        )
        {
            Debug.LogError(
                $"Cannot create module: content id '{debugLookupContentId}' was not found in this layout tree."
            );
            return null;
        }

        Vector3 desiredSpawnAnchor = GetPreferredSpawnAnchorPosition();

        GameObject spawnedObject = Instantiate(gameObject, transform.position, transform.rotation);
        spawnedObject.name = $"{name} (Copy:{debugLookupContentId})";

        ModuleController spawnedController = spawnedObject.GetComponent<ModuleController>();

        spawnedController.TransformCorrection();

        if (spawnedController == null)
        {
            if (Application.isPlaying)
            {
                Destroy(spawnedObject);
            }
            else
            {
                DestroyImmediate(spawnedObject);
            }
            Debug.LogError(
                "Cannot create module copy: spawned object is missing ModuleController."
            );
            return null;
        }

        spawnedController.SetLayoutTree(copy);
        spawnedController.ResetPositions();
        spawnedController.RegenerateLayout();
        spawnedController.MoveModuleContentAnchorTo(desiredSpawnAnchor);
        return spawnedController;
    }

    private void MoveModuleContentAnchorTo(Vector3 desiredAnchorPosition)
    {
        Vector3 currentAnchor = GetModuleContentAnchorPosition();
        Vector3 delta = desiredAnchorPosition - currentAnchor;
        transform.position += delta;
    }

    /// <summary>
    /// Test helper: creates a new module by popping the first subtree
    /// matching <see cref="debugLookupContentId"/> from this module's layout tree.
    /// The source tree is simplified at the removal point while preserving all other
    /// split directions/ratios in unaffected branches.
    /// </summary>
    public void CreateModuleFromDebugContentIdPop()
    {
        CreateModuleFromDebugContentIdPop(debugLookupContentId);
    }

    /// <summary>
    /// Test helper: creates a new module by popping the first subtree
    /// matching the provided content id from this module's layout tree.
    /// The source tree is simplified at the removal point while preserving all other
    /// split directions/ratios in unaffected branches.
    /// </summary>
    public void CreateModuleFromDebugContentIdPop(string contentId)
    {
        if (isResizing || isGrabbed)
        {
            Debug.LogError(
                "Cannot pop module subtree while this module is being resized or grabbed."
            );
            return;
        }

        if (string.IsNullOrWhiteSpace(contentId))
        {
            Debug.LogError("Cannot pop module subtree: content id cannot be null or empty.");
            return;
        }

        if (layoutTree == null)
        {
            layoutTree = ModuleLayoutTree.CreateDefault();
        }
        layoutTree.Validate();
        if (!layoutTree.ContainsSplittableContentId(contentId))
        {
            Debug.LogError(
                $"Cannot pop module subtree: content id '{contentId}' is not in the splittable content-id list."
            );
            return;
        }
        Vector3 desiredSpawnAnchor = GetPreferredSpawnAnchorPosition();

        ModuleLayoutTree sourceBackup = layoutTree.DeepClone();
        if (!layoutTree.TryPopSubtreeByContentId(contentId, out ModuleLayoutTree popped))
        {
            Debug.LogError(
                $"Cannot pop module subtree: content id '{contentId}' was not found in this layout tree."
            );
            return;
        }

        GameObject spawnedObject = Instantiate(gameObject, planeReference.position, planeReference.rotation);
        spawnedObject.name = $"{name} (Pop:{contentId})";

        ModuleController spawnedController = spawnedObject.GetComponent<ModuleController>();

        spawnedController.TransformCorrection();

        if (spawnedController == null)
        {
            if (Application.isPlaying)
            {
                Destroy(spawnedObject);
            }
            else
            {
                DestroyImmediate(spawnedObject);
            }

            // Roll back source tree if spawning fails so this operation remains atomic.
            SetLayoutTree(sourceBackup);
            RegenerateLayout();

            Debug.LogError(
                "Cannot create popped module: spawned object is missing ModuleController."
            );
            return;
        }

        spawnedController.SetLayoutTree(popped);
        spawnedController.ResetPositions();
        spawnedController.RegenerateLayout();
        spawnedController.MoveModuleContentAnchorTo(desiredSpawnAnchor);

        GetComponent<ContentListManager>()?.Reset();
        spawnedObject.GetComponent<ContentListManager>()?.Reset();

        // Refresh source visuals to reflect the simplified source tree post-pop.
        RegenerateLayout();

        if (
            DataCollector.Active != null
            && DataCollector.Active.TryGetTrackedModuleObjectId(this, out string sourceModuleId)
            && DataCollector.Active.TryGetTrackedModuleObjectId(spawnedController, out string spawnedModuleId)
        )
        {
            DataCollector.Active.LogInteraction(
                "split_created",
                sourceModuleId,
                "Module",
                relatedObjectId: spawnedModuleId,
                relatedObjectType: "Module",
                contentId: contentId);
        }
    }

    private Vector3 GetModuleContentAnchorPosition()
    {
        if (
            TryGetCornerWorldPositions(
                out Vector3 bl,
                out Vector3 br,
                out Vector3 tr,
                out Vector3 tl
            )
        )
        {
            return (bl + br + tr + tl) / 4f;
        }

        if (planeReference != null)
        {
            return planeReference.position;
        }

        return transform.position;
    }

    private Vector3 GetDebugSpawnOffsetWorld()
    {
        if (planeReference == null)
        {
            return transform.rotation * debugSpawnOffsetInModulePlane;
        }

        return planeReference.TransformVector(debugSpawnOffsetInModulePlane);
    }

    private Vector3 GetPreferredSpawnAnchorPosition()
    {
        if (PanelsManager.Instance != null
            && PanelsManager.Instance.GetOptimalPanelPos(out Vector3 panelPosition, out _))
        {
            return panelPosition;
        }

        Vector3 sourceAnchor = GetModuleContentAnchorPosition();
        return sourceAnchor + GetDebugSpawnOffsetWorld();
    }

    private ModuleLayoutGenerator GetLayoutGenerator(bool createIfMissing)
    {
        ModuleLayoutGenerator generator = GetComponent<ModuleLayoutGenerator>();
        if (generator != null || !createIfMissing)
        {
            return generator;
        }

        return gameObject.AddComponent<ModuleLayoutGenerator>();
    }
    #endregion
}
