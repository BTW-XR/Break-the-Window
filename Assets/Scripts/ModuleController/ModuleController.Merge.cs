using System.Collections.Generic;
using UnityEngine;

public partial class ModuleController
{
    #region Merge
    public void VoidConfirmMerge()
    {
        TryConfirmMerge();
    }

    /// <summary>
    /// Confirms a merge only when a valid merge preview actually exists.
    /// Returns true if a merge was completed, false otherwise (no candidate / no preview).
    /// Safe to call on every release without spamming "cannot confirm merge" errors.
    /// </summary>
    public bool TryConfirmMerge()
    {
        if (activeMergeTarget == null || !TryGetMergePreviewCornerPositions(out _))
        {
            return false;
        }

        return ConfirmMerge() != null;
    }

    public ModuleController ConfirmMerge()
    {
        // Confirm can only happen while a valid preview (and partner) exists.
        if (!TryGetMergePreviewCornerPositions(out Vector3[] orderedPreviewCorners))
        {
            Debug.LogError("Cannot confirm merge: merge preview corners are not available.");
            return null;
        }
        if (activeMergeTarget == null)
        {
            Debug.LogWarning("Cannot confirm merge: active merge target is missing.");
            return null;
        }

        // Clone from the merge target template (not the grabbed source module),
        // so we do not inherit source-object transform quirks during merge confirm.
        GameObject mergedObject = Instantiate(
            activeMergeTarget.gameObject,
            activeMergeTarget.transform.parent
        );
        mergedObject.name = $"{activeMergeTarget.name} + {this.name}";

        ModuleController mergedController = mergedObject.GetComponent<ModuleController>();
        if (mergedController == null)
        {
            if (Application.isPlaying)
            {
                Destroy(mergedObject);
            }
            else
            {
                DestroyImmediate(mergedObject);
            }
            Debug.LogError("Cannot confirm merge: merged module is missing ModuleController.");
            return null;
        }

        // Remove cloned preview/runtime references, then place the merged geometry from preview corners.
        mergedController.ResetMergePreviewReferences();
        mergedController.OnGrabberRelease();
        ModuleLayoutGenerator mergedGenerator = mergedController.GetLayoutGenerator(true);
        if (mergedGenerator != null)
        {
            mergedGenerator.ClearAllGeneratedLayoutVisuals();
        }

        ModuleLayoutTree mergedLayoutTree = ComposeMergedLayoutTree(
            activeMergeTarget,
            activeMergeEdgeIndex
        );
        if (!mergedController.ApplyMergedCorners(orderedPreviewCorners))
        {
            if (Application.isPlaying)
            {
                Destroy(mergedObject);
            }
            else
            {
                DestroyImmediate(mergedObject);
            }
            Debug.LogError(
                "Cannot confirm merge: failed to apply preview corners to merged module."
            );
            return null;
        }
        mergedController.SetLayoutTree(mergedLayoutTree);
        mergedController.RegenerateLayout();

        // Finalize: clear local preview and disable the two source modules.
        ModuleController sourceA = this;
        ModuleController sourceB = activeMergeTarget;

        sourceA.GetLayoutGenerator(true)?.ClearAllGeneratedLayoutVisuals();
        sourceB.GetLayoutGenerator(true)?.ClearAllGeneratedLayoutVisuals();

        if (
            DataCollector.Active != null
            && DataCollector.Active.TryGetTrackedModuleObjectId(sourceA, out string sourceAId)
            && sourceB != null
            && DataCollector.Active.TryGetTrackedModuleObjectId(sourceB, out string sourceBId)
            && DataCollector.Active.TryGetTrackedModuleObjectId(mergedController, out string mergedId)
        )
        {
            DataCollector.Active.LogInteraction(
                "merge_confirmed",
                sourceAId,
                "Module",
                relatedObjectId: sourceBId,
                relatedObjectType: "Module",
                secondaryObjectId: mergedId,
                secondaryObjectType: "Module");
        }

        doMerge = false;
        ClearMergePreview();
        sourceA.gameObject.SetActive(false);
        if (sourceB != null && sourceB != sourceA)
        {
            sourceB.doMerge = false;
            sourceB.ClearMergePreview();
            sourceB.gameObject.SetActive(false);
        }

        mergedObject.GetComponent<ContentListManager>()?.FindDependencies();
        mergedObject.GetComponent<ContentListManager>()?.RegenerateView();

        return mergedController;
    }

    private void UpdateMergePreview()
    {
        MergeCandidate? candidate = FindMergeCandidate();
        ModuleController other = candidate?.other;
        int otherEdgeIndex = candidate?.otherEdgeIndex ?? -1;
        if (candidate == null)
        {
            ClearMergePreview();
            return;
        }
        if (markerPreviewPrefab == null)
        {
            Debug.LogError("Cannot show merge preview: markerPreviewPrefab is missing.");
            return;
        }
        // Persist which module this preview is pairing against, so Confirm can disable both sources.
        activeMergeTarget = other;
        activeMergeEdgeIndex = candidate.Value.myEdgeIndex;
        Debug.Log(
            $"Merging with candidate: {candidate}, activeMergeEdgeIndex: {activeMergeEdgeIndex}"
        );
        int myEdgeIndex = candidate.Value.myEdgeIndex;

        float myOtherEdgeLength = edges[(myEdgeIndex + 1) % 4].Length;

        Debug.Log($"My edge index: {myEdgeIndex}, My other edge length: {myOtherEdgeLength}");

        // Set the corners of this module to match the corresponding corners of the other module, so the two edges overlap.
        Transform[] otherCorners = candidate.Value.other.corners;
        Transform otherCorner1 = other.edges[otherEdgeIndex].corner1;
        Transform otherCorner2 = other.edges[otherEdgeIndex].corner2;

        Quaternion otherRotation = otherCorners[0].rotation;
        Debug.Log($"edgeNormals[myEdgeIndex]: {edgeNormals[myEdgeIndex]}");

        Vector3 otherCorner1ExtendedPos =
            otherCorner1.position
            + other.planeReference.TransformDirection(
                other.edges[myEdgeIndex].normal * myOtherEdgeLength
            );

        Vector3 otherCorner2ExtendedPos =
            otherCorner2.position
            + other.planeReference.TransformDirection(
                other.edges[myEdgeIndex].normal * myOtherEdgeLength
            );

        List<Corner> newCorners = new List<Corner>
        {
            new Corner(otherCorner1ExtendedPos, other.edges[otherEdgeIndex].corner1Index),
            new Corner(otherCorner2ExtendedPos, other.edges[otherEdgeIndex].corner2Index),
            new Corner(
                other.edges[myEdgeIndex].corner1Position,
                other.edges[myEdgeIndex].corner1Index
            ),
            new Corner(
                other.edges[myEdgeIndex].corner2Position,
                other.edges[myEdgeIndex].corner2Index
            ),
        };

        newCorners.Sort((a, b) => a.index.CompareTo(b.index));

        for (int i = 0; i < mergePreviewMarkers.Length; i++)
        {
            if (mergePreviewMarkers[i] == null)
            {
                mergePreviewMarkers[i] = Instantiate(
                    markerPreviewPrefab,
                    newCorners[i].position,
                    otherRotation
                ).transform;
            }
            else
            {
                mergePreviewMarkers[i]
                    .SetPositionAndRotation(newCorners[i].position, otherRotation);
            }
        }

        UpdateMergePreviewQuad(newCorners);
    }

    private KeyValuePair<int, float> FindShortestDistance(ModuleController other)
    {
        // Keep edge midpoint and distance logic centralized so merge detection
        // and any future edge-comparison features use the exact same math.
        Vector3[] myEdges = ModuleControllerGeometryHelpers.BuildEdgeMidpoints(
            bottomLeft,
            bottomRight,
            topRight,
            topLeft
        );
        Vector3[] otherEdges = ModuleControllerGeometryHelpers.BuildEdgeMidpoints(
            other.bottomLeft,
            other.bottomRight,
            other.topRight,
            other.topLeft
        );

        return ModuleControllerGeometryHelpers.FindShortestOppositeEdgeDistance(
            myEdges,
            otherEdges
        );
    }

    private MergeCandidate? FindMergeCandidate()
    {
        MergeCandidate? bestCandidate = null;
        for (int i = 0; i < instances.Count; i++)
        {
            if (instances[i] == this)
            {
                continue;
            }
            KeyValuePair<int, float> result = FindShortestDistance(instances[i]);
            Debug.Log(
                $"Shortest distance to instance {i}: {result.Value} at edge index {result.Key}"
            );

            if (bestCandidate == null || result.Value < bestCandidate?.distance)
            {
                // Could be moved out of for loop for efficiency, but left here for clarity.
                bestCandidate = new MergeCandidate(instances[i], result.Key, result.Value);
            }
        }

        if (bestCandidate != null && bestCandidate?.distance <= mergeDistanceThreshold)
        {
            Debug.Log($"Best candidate for merging: {bestCandidate}");
            return bestCandidate;
        }

        return null;
    }

    private bool ApplyMergedCorners(Vector3[] orderedCorners)
    {
        if (orderedCorners == null || orderedCorners.Length != 4 || !HasRequiredReferences())
        {
            return false;
        }

        Vector3 bl = orderedCorners[0];
        Vector3 br = orderedCorners[1];
        Vector3 tr = orderedCorners[2];
        Vector3 tl = orderedCorners[3];

        // Rebuild a stable local basis from merged corner positions.
        // This keeps plane orientation and preview orientation behavior aligned.
        if (
            !ModuleControllerGeometryHelpers.TryBuildQuadBasis(
                bl,
                br,
                tl,
                out Vector3 _,
                out Vector3 up,
                out Vector3 forward,
                out float _,
                out float _
            )
        )
        {
            return false;
        }

        Vector3 center = (bl + br + tr + tl) / 4f;
        // PlaneReference is normalized to a unit transform and rotated to the merged plane basis.
        planeReference.SetPositionAndRotation(center, Quaternion.LookRotation(forward, up));
        planeReference.localScale = Vector3.one;

        bottomLeft.position = bl;
        bottomRight.position = br;
        topRight.position = tr;
        topLeft.position = tl;

        HandleResize();
        return true;
    }

    private bool TryGetMergePreviewCornerPositions(out Vector3[] orderedCorners)
    {
        orderedCorners = null;
        // We only allow confirm when all four ordered preview corners currently exist.
        for (int i = 0; i < mergePreviewMarkers.Length; i++)
        {
            if (mergePreviewMarkers[i] == null)
            {
                return false;
            }
        }

        orderedCorners = new Vector3[]
        {
            mergePreviewMarkers[0].position,
            mergePreviewMarkers[1].position,
            mergePreviewMarkers[2].position,
            mergePreviewMarkers[3].position,
        };
        return true;
    }

    private void ResetMergePreviewReferences()
    {
        for (int i = 0; i < mergePreviewMarkers.Length; i++)
        {
            mergePreviewMarkers[i] = null;
        }
        mergePreviewQuad = null;
        activeMergeTarget = null;
        activeMergeEdgeIndex = -1;
    }

    private void ClearMergePreview()
    {
        // Destroy runtime preview markers.
        for (int i = 0; i < mergePreviewMarkers.Length; i++)
        {
            if (mergePreviewMarkers[i] == null)
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Destroy(mergePreviewMarkers[i].gameObject);
            }
            else
            {
                DestroyImmediate(mergePreviewMarkers[i].gameObject);
            }

            mergePreviewMarkers[i] = null;
        }

        // Destroy runtime thick preview body.
        if (mergePreviewQuad != null)
        {
            if (Application.isPlaying)
            {
                Destroy(mergePreviewQuad.gameObject);
            }
            else
            {
                DestroyImmediate(mergePreviewQuad.gameObject);
            }

            mergePreviewQuad = null;
        }

        activeMergeTarget = null;
        activeMergeEdgeIndex = -1;
    }

    private void UpdateMergePreviewQuad(List<Corner> sortedCorners)
    {
        if (sortedCorners == null || sortedCorners.Count != 4)
        {
            return;
        }

        Vector3 bl = sortedCorners[0].position;
        Vector3 br = sortedCorners[1].position;
        Vector3 tr = sortedCorners[2].position;
        Vector3 tl = sortedCorners[3].position;

        // Build preview orientation and dimensions from the same helper used
        // by merged-plane reconstruction to avoid drift between preview and final result.
        if (
            !ModuleControllerGeometryHelpers.TryBuildQuadBasis(
                bl,
                br,
                tl,
                out Vector3 _,
                out Vector3 up,
                out Vector3 forward,
                out float width,
                out float height
            )
        )
        {
            return;
        }

        if (mergePreviewQuad == null)
        {
            // Cube is used as an extruded quad so we can visualize thickness.
            GameObject previewQuadObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            previewQuadObj.name = $"{name}_MergePreviewQuad";
            Collider previewCollider = previewQuadObj.GetComponent<Collider>();
            if (previewCollider != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(previewCollider);
                }
                else
                {
                    DestroyImmediate(previewCollider);
                }
            }

            if (mergePreviewMaterial != null)
            {
                MeshRenderer previewRenderer = previewQuadObj.GetComponent<MeshRenderer>();
                if (previewRenderer != null)
                {
                    previewRenderer.sharedMaterial = mergePreviewMaterial;
                }
            }

            mergePreviewQuad = previewQuadObj.transform;
        }

        Vector3 center = (bl + br + tr + tl) / 4f;
        Quaternion rotation = Quaternion.LookRotation(forward, up);
        float thickness = Mathf.Max(0.001f, mergePreviewThickness);

        mergePreviewQuad.SetPositionAndRotation(center, rotation);
        mergePreviewQuad.localScale = new Vector3(width, height, thickness);
    }

    private ModuleLayoutTree ComposeMergedLayoutTree(ModuleController target, int mergeEdgeIndex)
    {
        ModuleLayoutTree sourceTree = GetLayoutTreeCloneOrDefault();
        ModuleLayoutTree targetTree =
            target != null
                ? target.GetLayoutTreeCloneOrDefault()
                : ModuleLayoutTree.CreateDefault();

        // 0/2 Means the bottom and top edges are merged, so the split is vertical.
        // 1/3 means left and right edges are merged, so the split is horizontal.
        ModuleLayoutTree.SplitDirection direction =
            (mergeEdgeIndex == 0 || mergeEdgeIndex == 2)
                ? ModuleLayoutTree.SplitDirection.Vertical
                : ModuleLayoutTree.SplitDirection.Horizontal;

        // Split ratio should represent span along the split axis:
        // - Vertical split uses height (top/bottom merge case).
        // - Horizontal split uses width (left/right merge case).
        float sourceSpan = GetModuleSplitSpan(direction);
        float targetSpan = target != null ? target.GetModuleSplitSpan(direction) : 0f;
        float totalSpan = sourceSpan + targetSpan;

        // Child ordering must represent spatial order, not caller order:
        // - Vertical split: first=Top, second=Bottom.
        // - Horizontal split: first=Left, second=Right.
        // Edge index is from the source (this module):
        // 0 (bottom) => source is Top
        // 1 (right)  => source is Left
        // 2 (top)    => source is Bottom
        // 3 (left)   => source is Right
        bool sourceIsFirstChild = mergeEdgeIndex == 0 || mergeEdgeIndex == 1;
        ModuleLayoutTree.Node firstRoot = sourceIsFirstChild
            ? sourceTree.Root?.DeepClone()
            : targetTree.Root?.DeepClone();
        ModuleLayoutTree.Node secondRoot = sourceIsFirstChild
            ? targetTree.Root?.DeepClone()
            : sourceTree.Root?.DeepClone();
        float firstSpan = sourceIsFirstChild ? sourceSpan : targetSpan;
        float ratio = totalSpan > 0.0001f ? firstSpan / totalSpan : 0.5f;

        ModuleLayoutTree.Node mergedRoot = ModuleLayoutTree.Node.CreateFromTwo(
            direction,
            ratio,
            firstRoot,
            secondRoot
        );
        return ModuleLayoutTree.CreateWithRoot(mergedRoot);
    }

    private float GetModuleSplitSpan(ModuleLayoutTree.SplitDirection direction)
    {
        if (
            !TryGetCornerWorldPositions(
                out Vector3 bl,
                out Vector3 br,
                out Vector3 _,
                out Vector3 tl
            )
        )
        {
            return 0f;
        }

        float width = Vector3.Distance(bl, br);
        float height = Vector3.Distance(bl, tl);
        return direction == ModuleLayoutTree.SplitDirection.Vertical ? height : width;
    }
    #endregion
}
