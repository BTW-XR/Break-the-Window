using System;
using System.Collections.Generic;
using UnityEngine;

public partial class ModuleLayoutTree
{
    // Pops the first subtree (leaf or branch) matching content id and simplifies
    // the remaining source tree by removing the parent split level at the pop site.
    public bool TryPopSubtreeByContentId(string targetContentId, out ModuleLayoutTree poppedTree)
    {
        poppedTree = null;
        if (string.IsNullOrWhiteSpace(targetContentId) || root == null)
        {
            return false;
        }
        Validate();
        if (!ContainsSplittableContentId(targetContentId))
        {
            return false;
        }

        // Special-case popping root: don't allow it.
        if (string.Equals(root.ContentId, targetContentId, StringComparison.Ordinal))
        {
            return false;
        }

        List<int> path = new List<int>(8);
        if (!TryFindNodePathByContentId(root, targetContentId, path) || path.Count == 0)
        {
            return false;
        }

        return TryPopSubtreeAtPath(path, out poppedTree);
    }

    // Pops the subtree at the given path and simplifies the source tree.
    // Path entries are 0 for first child and 1 for second child.
    // Example: [0, 1] means root.firstChild.secondChild.
    private bool TryPopSubtreeAtPath(IReadOnlyList<int> path, out ModuleLayoutTree poppedTree)
    {
        poppedTree = null;
        if (path == null || path.Count == 0 || root == null)
        {
            return false;
        }

        Node current = root;
        Node parent = null;
        Node grandParent = null;
        bool targetIsFirstChild = false;
        bool parentIsFirstChild = false;

        for (int i = 0; i < path.Count; i++)
        {
            int step = path[i];
            if ((step != 0 && step != 1) || current == null || current.IsLeaf)
            {
                return false;
            }

            grandParent = parent;
            parent = current;
            parentIsFirstChild = targetIsFirstChild;
            targetIsFirstChild = step == 0;
            current = current.GetChild(targetIsFirstChild);
        }

        if (current == null || parent == null)
        {
            return false;
        }

        Node poppedRoot = current.DeepClone();
        Node sibling = parent.GetChild(!targetIsFirstChild);
        Node replacement = sibling?.DeepClone() ?? Node.CreateLeaf("remaining-region");

        SplitDirection removedSplitDirection = parent.Direction;
        float removedSplitRatio = parent.Ratio;

        if (grandParent == null)
        {
            root = replacement;
        }
        else if (parentIsFirstChild)
        {
            grandParent.SetChild(true, replacement);
        }
        else
        {
            grandParent.SetChild(false, replacement);
        }

        SetPreservedSplitMetadata(removedSplitDirection, removedSplitRatio);

        poppedTree = CreateWithRoot(poppedRoot);
        poppedTree.SetPreservedSplitMetadata(removedSplitDirection, removedSplitRatio);

        Validate();
        return true;
    }

    private static bool TryFindNodePathByContentId(Node node, string targetContentId, List<int> path)
    {
        if (node == null)
        {
            return false;
        }

        if (string.Equals(node.ContentId, targetContentId, StringComparison.Ordinal))
        {
            return true;
        }

        path.Add(0);
        if (TryFindNodePathByContentId(node.FirstChild, targetContentId, path))
        {
            return true;
        }
        path.RemoveAt(path.Count - 1);

        path.Add(1);
        if (TryFindNodePathByContentId(node.SecondChild, targetContentId, path))
        {
            return true;
        }
        path.RemoveAt(path.Count - 1);

        return false;
    }
}