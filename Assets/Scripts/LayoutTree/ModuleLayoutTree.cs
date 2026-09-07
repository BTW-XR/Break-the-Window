using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public partial class ModuleLayoutTree
{
    // Vertical split means first child is top, second child is bottom.
    // Horizontal split means first child is left, second child is right.
    public enum SplitDirection
    {
        Vertical,
        Horizontal,
    }

    [SerializeReference]
    private Node root = Node.CreateLeaf("root");

    // When a subtree is popped from a split parent, the parent level is simplified away
    // in the remaining tree. We still preserve that split metadata here so callers can
    // reason about where the popped subtree came from.
    [SerializeField]
    private bool hasPreservedSplitMetadata = false;

    [SerializeField]
    private SplitDirection preservedSplitDirection = SplitDirection.Vertical;

    [SerializeField]
    private float preservedSplitRatio = 0.5f;

    public Node Root => root;
    public bool HasPreservedSplitMetadata => hasPreservedSplitMetadata;
    public SplitDirection PreservedSplitDirection => preservedSplitDirection;
    public float PreservedSplitRatio => preservedSplitRatio;


    private readonly HashSet<string> splittableContentIdLookup = new HashSet<string>(
        StringComparer.Ordinal
    );

    public static ModuleLayoutTree CreateDefault()
    {
        return new ModuleLayoutTree();
    }

    public static ModuleLayoutTree CreateWithRoot(Node rootNode)
    {
        ModuleLayoutTree tree = new ModuleLayoutTree();
        tree.root = rootNode ?? Node.CreateLeaf("root");
        tree.Validate();
        return tree;
    }

    public ModuleLayoutTree DeepClone()
    {
        ModuleLayoutTree clone = CreateWithRoot(root?.DeepClone() ?? Node.CreateLeaf("root"));
        if (hasPreservedSplitMetadata)
        {
            clone.SetPreservedSplitMetadata(preservedSplitDirection, preservedSplitRatio);
        }
        return clone;
    }

    public void Validate()
    {
        if (root == null)
        {
            root = Node.CreateLeaf("root");
        }

        root.ValidateRecursive();
        if (hasPreservedSplitMetadata)
        {
            preservedSplitRatio = Mathf.Clamp(preservedSplitRatio, 0.001f, 0.999f);
        }

        RebuildSplittableContentIdIndex();
    }

    public List<string> CollectLeafContentIds()
    {
        List<string> contentIds = new List<string>();
        CollectLeafContentIdsRecursive(root, contentIds);
        return contentIds;
    }

    private static void CollectLeafContentIdsRecursive(Node node, List<string> contentIds)
    {
        if (node == null)
        {
            return;
        }

        if (node.IsLeaf)
        {
            contentIds.Add(node.ContentId);
        }
        else
        {
            CollectLeafContentIdsRecursive(node.FirstChild, contentIds);
            CollectLeafContentIdsRecursive(node.SecondChild, contentIds);
        }
    }

    public void ClearPreservedSplitMetadata()
    {
        hasPreservedSplitMetadata = false;
    }

    private void SetPreservedSplitMetadata(SplitDirection direction, float ratio)
    {
        hasPreservedSplitMetadata = true;
        preservedSplitDirection = direction;
        preservedSplitRatio = Mathf.Clamp(ratio, 0.001f, 0.999f);
    }

}
