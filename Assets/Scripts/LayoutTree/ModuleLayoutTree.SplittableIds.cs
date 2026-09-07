using System.Collections.Generic;
using UnityEngine;

public partial class ModuleLayoutTree
{
    [SerializeField]
    private List<string> splittableContentIds = new List<string>();

    public IReadOnlyList<string> SplittableContentIds => splittableContentIds;

    public bool ContainsSplittableContentId(string contentId)
    {
        if (string.IsNullOrWhiteSpace(contentId))
        {
            return false;
        }

        RebuildSplittableContentIdIndex();
        return splittableContentIdLookup.Contains(contentId);
    }

    private void RebuildSplittableContentIdIndex()
    {
        splittableContentIdLookup.Clear();
        if (splittableContentIds == null)
        {
            splittableContentIds = new List<string>();
        }
        else
        {
            splittableContentIds.Clear();
        }

        // Root represents the whole module and should never be split out directly.
        if (root == null || root.IsLeaf)
        {
            return;
        }

        CollectSplittableIdsRecursive(root.FirstChild);
        CollectSplittableIdsRecursive(root.SecondChild);
    }

    private void CollectSplittableIdsRecursive(Node node)
    {
        if (node == null || string.IsNullOrWhiteSpace(node.ContentId))
        {
            return;
        }

        if (splittableContentIdLookup.Add(node.ContentId))
        {
            splittableContentIds.Add(node.ContentId);
        }

        if (node.IsLeaf)
        {
            return;
        }

        CollectSplittableIdsRecursive(node.FirstChild);
        CollectSplittableIdsRecursive(node.SecondChild);
    }

}