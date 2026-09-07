using System;

public partial class ModuleLayoutTree
{
    // Returns the first node whose ContentId exactly matches targetContentId.
    // The returned node is a deep clone so callers can mutate it safely.
    private bool TryGetNodeCloneByContentId(string targetContentId, out Node clonedNode)
    {
        clonedNode = null;
        if (string.IsNullOrWhiteSpace(targetContentId))
        {
            return false;
        }
        Validate();
        if (!ContainsSplittableContentId(targetContentId))
        {
            return false;
        }

        Node foundNode = FindFirstNodeByContentId(root, targetContentId);
        if (foundNode == null)
        {
            return false;
        }

        clonedNode = foundNode.DeepClone();
        return true;
    }

    // Finds the root node and wrap it into a substree.
    public bool TryGetSubtreeCloneByContentId(
        string targetContentId,
        out ModuleLayoutTree clonedSubtree
    )
    {
        clonedSubtree = null;
        if (!TryGetNodeCloneByContentId(targetContentId, out Node clonedNode))
        {
            return false;
        }

        clonedSubtree = CreateWithRoot(clonedNode);
        return true;
    }

    private static Node FindFirstNodeByContentId(Node node, string targetContentId)
    {
        if (node == null)
        {
            return null;
        }

        if (string.Equals(node.ContentId, targetContentId, StringComparison.Ordinal))
        {
            return node;
        }

        Node firstMatch = FindFirstNodeByContentId(node.FirstChild, targetContentId);
        if (firstMatch != null)
        {
            return firstMatch;
        }

        return FindFirstNodeByContentId(node.SecondChild, targetContentId);
    }
}