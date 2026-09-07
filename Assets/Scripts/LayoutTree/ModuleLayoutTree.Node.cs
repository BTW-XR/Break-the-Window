using System;
using System.Collections.Generic;
using UnityEngine;

public partial class ModuleLayoutTree
{
    [Serializable]
    public class Node
    {
        [SerializeField]
        private bool isLeaf = true;

        [SerializeField]
        private string contentId = "content";

        [SerializeField]
        private SplitDirection splitDirection = SplitDirection.Vertical;

        // Ratio maps to first child span:
        // - Vertical: first child is Top.
        // - Horizontal: first child is Left.
        [SerializeField]
        private float splitRatio = 0.5f;

        [SerializeReference]
        private Node firstChild;

        [SerializeReference]
        private Node secondChild;

        public bool IsLeaf => isLeaf;
        public string ContentId => contentId;
        public SplitDirection Direction => splitDirection;
        public float Ratio => splitRatio;
        public Node FirstChild => firstChild;
        public Node SecondChild => secondChild;

        public Node GetChild(bool isFirstChild)
        {
            return isFirstChild ? firstChild : secondChild;
        }

        public void SetChild(bool isFirstChild, Node child)
        {
            if (isFirstChild)
            {
                firstChild = child;
            }
            else
            {
                secondChild = child;
            }
        }

        public static Node CreateLeaf(string newContentId)
        {
            return new Node
            {
                isLeaf = true,
                contentId = string.IsNullOrWhiteSpace(newContentId) ? "content" : newContentId,
                splitRatio = 0.5f,
                firstChild = null,
                secondChild = null,
            };
        }

        public static Node CreateFromTwo(
            SplitDirection direction,
            float ratio,
            Node newFirstChild,
            Node newSecondChild
        )
        {
            Node first = newFirstChild ?? CreateLeaf("first-region");
            Node second = newSecondChild ?? CreateLeaf("second-region");

            string[] contentIdArray = (first.contentId + "_" + second.contentId).Split('_');
            Array.Sort(contentIdArray, StringComparer.Ordinal);
            string newContentId = string.Join("_", contentIdArray);

            return new Node
            {
                isLeaf = false,
                splitDirection = direction,
                splitRatio = Mathf.Clamp(ratio, 0.001f, 0.999f),
                firstChild = first,
                secondChild = second,
                contentId = newContentId,
            };
        }

        public Node DeepClone()
        {
            if (isLeaf)
            {
                return CreateLeaf(contentId);
            }

            return CreateFromTwo(
                splitDirection,
                splitRatio,
                firstChild?.DeepClone(),
                secondChild?.DeepClone()
            );
        }

        // For a leaf, it ensures contentId isnon-empty (defaults to "content"), and forces firstChild/secondChild to null. 
        // For a split node, it clamps splitRatioto [0.001, 0.999], 
        // ensures both children exist (creating placeholder leaves if missing), 
        // and then recurses into bothchildren to do the same.
        public void ValidateRecursive()
        {
            if (isLeaf)
            {
                if (string.IsNullOrWhiteSpace(contentId))
                {
                    contentId = "content";
                }
                firstChild = null;
                secondChild = null;
                return;
            }

            splitRatio = Mathf.Clamp(splitRatio, 0.001f, 0.999f);
            if (firstChild == null)
            {
                firstChild = CreateLeaf("first-region");
            }
            if (secondChild == null)
            {
                secondChild = CreateLeaf("second-region");
            }

            firstChild.ValidateRecursive();
            secondChild.ValidateRecursive();
        }
    }
}