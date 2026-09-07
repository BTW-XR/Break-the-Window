using System;
using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class ModuleLayoutGenerator : MonoBehaviour
{
    [SerializeField]
    private ModuleController moduleController;

    [SerializeField]
    private Transform layoutRoot;

    [SerializeField]
    private Material layoutQuadMaterial;

    private readonly List<GameObject> generatedQuads = new List<GameObject>();

    private void Awake()
    {
        EnsureReferences();
    }

    // Reparent generated quads to a new parent. This is used when the module is grabbed to move quads under the grabber.
    public void ReparentGeneratedQuads(Transform newParent)
    {
        if (newParent == null)
        {
            return;
        }

        for (int i = 0; i < generatedQuads.Count; i++)
        {
            if (generatedQuads[i] != null)
            {
                generatedQuads[i].transform.SetParent(newParent, true);
            }
        }
    }

    // Reparents generated quads back to layout root. This is used after merging modules to move quads from the grabber back to the merged module's layout root.
    public void ReparentGeneratedQuads()
    {
        for (int i = 0; i < generatedQuads.Count; i++)
        {
            if (generatedQuads[i] != null)
            {
                generatedQuads[i].transform.SetParent(layoutRoot, true);
            }
        }
    }

    public void Regenerate(ModuleLayoutTree tree)
    {
        EnsureReferences();
        ClearAllGeneratedLayoutVisuals();

        if (moduleController == null || tree == null || tree.Root == null)
        {
            return;
        }

        if (
            !moduleController.TryGetCornerWorldPositions(
                out Vector3 bl,
                out Vector3 br,
                out Vector3 tr,
                out Vector3 tl
            )
        )
        {
            return;
        }

        tree.Validate();
        BuildRecursive(tree.Root, bl, br, tr, tl, "root");
    }

    public void ClearGeneratedLayout()
    {
        // First clear everything tracked by generatedQuads. This covers quads even if they were
        // temporarily reparented away from LayoutRoot (for example while the module is grabbed).
        for (int i = 0; i < generatedQuads.Count; i++)
        {
            if (generatedQuads[i] == null)
            {
                continue;
            }

            DestroyObjectByMode(generatedQuads[i]);
        }
        generatedQuads.Clear();

        // Then clear any existing LayoutRoot children that were not tracked in generatedQuads.
        // This is required for cloned modules where LayoutRoot content may be duplicated by
        // hierarchy instantiation before Regenerate() runs.
        if (layoutRoot == null)
        {
            return;
        }

        List<GameObject> rootChildren = new List<GameObject>(layoutRoot.childCount);
        for (int i = 0; i < layoutRoot.childCount; i++)
        {
            Transform child = layoutRoot.GetChild(i);
            if (child != null)
            {
                rootChildren.Add(child.gameObject);
            }
        }

        for (int i = 0; i < rootChildren.Count; i++)
        {
            DestroyObjectByMode(rootChildren[i]);
        }
    }

    // Clears generated visuals from both tracking containers and hierarchy fallbacks.
    // This is used for cloned modules where quads may have been copied while attached
    // outside LayoutRoot (for example under a grabber during merge).
    public void ClearAllGeneratedLayoutVisuals()
    {
        ClearGeneratedLayout();
        ClearUntrackedLeafQuads();
    }

    private void EnsureReferences()
    {
        if (moduleController == null)
        {
            moduleController = GetComponent<ModuleController>();
        }

        if (layoutRoot == null)
        {
            Transform existing = transform.Find("LayoutRoot");
            if (existing != null)
            {
                layoutRoot = existing;
            }
            else
            {
                GameObject root = new GameObject("LayoutRoot");
                root.transform.SetParent(transform, false);
                layoutRoot = root.transform;
            }
        }
    }

    private void BuildRecursive(
        ModuleLayoutTree.Node node,
        Vector3 bl,
        Vector3 br,
        Vector3 tr,
        Vector3 tl,
        string path
    )
    {
        if (node == null)
        {
            return;
        }

        if (node.IsLeaf)
        {
            // Path suffixes (T/B/L/R) encode traversal history and help with debugging.
            CreateLeafQuad(node.ContentId, bl, br, tr, tl, path);
            return;
        }

        float ratio = Mathf.Clamp(node.Ratio, 0.001f, 0.999f);
        if (node.Direction == ModuleLayoutTree.SplitDirection.Vertical)
        {
            // Vertical split => stacked regions (Top / Bottom).
            // First child = Top, second child = Bottom.
            float bottomFraction = 1f - ratio;
            Vector3 splitLeft = Vector3.Lerp(bl, tl, bottomFraction);
            Vector3 splitRight = Vector3.Lerp(br, tr, bottomFraction);

            BuildRecursive(node.FirstChild, splitLeft, splitRight, tr, tl, $"{path}_T");
            BuildRecursive(node.SecondChild, bl, br, splitRight, splitLeft, $"{path}_B");
            return;
        }

        // Horizontal split => side-by-side regions (Left / Right).
        // First child = Left, second child = Right.
        Vector3 splitBottom = Vector3.Lerp(bl, br, ratio);
        Vector3 splitTop = Vector3.Lerp(tl, tr, ratio);

        BuildRecursive(node.FirstChild, bl, splitBottom, splitTop, tl, $"{path}_L");
        BuildRecursive(node.SecondChild, splitBottom, br, tr, splitTop, $"{path}_R");
    }

    private void CreateLeafQuad(
        string contentId,
        Vector3 bl,
        Vector3 br,
        Vector3 tr,
        Vector3 tl,
        string path
    )
    {
        Vector3 right = br - bl;
        Vector3 up = tl - bl;
        float width = right.magnitude;
        float height = up.magnitude;
        if (width < 0.0001f || height < 0.0001f)
        {
            return;
        }

        right /= width;
        up /= height;
        Vector3 forward = Vector3.Cross(right, up).normalized;
        if (forward.sqrMagnitude < 0.0001f)
        {
            return;
        }

        GameObject quadObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quadObj.name = string.IsNullOrWhiteSpace(contentId)
            ? $"Leaf_{path}"
            : $"Leaf_{contentId}_{path}";
        quadObj.transform.SetParent(layoutRoot, true);

        Collider quadCollider = quadObj.GetComponent<Collider>();
        if (quadCollider != null)
        {
            if (Application.isPlaying)
            {
                Destroy(quadCollider);
            }
            else
            {
                DestroyImmediate(quadCollider);
            }
        }

        MeshRenderer renderer = quadObj.GetComponent<MeshRenderer>();
        if (renderer != null && layoutQuadMaterial != null)
        {
            renderer.sharedMaterial = layoutQuadMaterial;
        }

        renderer.enabled = false; // Start disabled to avoid rendering during setup.

        Vector3 center = (bl + br + tr + tl) / 4f;
        // Orient each quad to the local layout plane basis derived from corners.
        quadObj.transform.SetPositionAndRotation(center, Quaternion.LookRotation(forward, up));
        quadObj.transform.localScale = new Vector3(width, height, 1f);

        generatedQuads.Add(quadObj);
    }

    private static void DestroyObjectByMode(UnityEngine.Object target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }

    private void ClearUntrackedLeafQuads()
    {
        Transform[] descendants = GetComponentsInChildren<Transform>(true);
        List<GameObject> staleLeafQuads = new List<GameObject>(descendants.Length);
        for (int i = 0; i < descendants.Length; i++)
        {
            Transform current = descendants[i];
            if (current == null || current == transform)
            {
                continue;
            }

            // Layout quads created by this generator follow Leaf_* naming.
            // Restricting to mesh-rendered objects reduces false positives.
            if (!current.name.StartsWith("Leaf_", StringComparison.Ordinal))
            {
                continue;
            }
            if (
                current.GetComponent<MeshRenderer>() == null
                || current.GetComponent<MeshFilter>() == null
            )
            {
                continue;
            }

            staleLeafQuads.Add(current.gameObject);
        }

        for (int i = 0; i < staleLeafQuads.Count; i++)
        {
            DestroyObjectByMode(staleLeafQuads[i]);
        }
    }
}
