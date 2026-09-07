using System.Collections.Generic;
using UnityEngine;

public enum ModuleEdge { Bottom = 0, Right = 1, Top = 2, Left = 3, Center = 4 }

[System.Serializable]
public class ModuleElement
{
    public Transform transform;
    public ModuleEdge edge;
    [Range(0f, 1f)] public float positionRatio;
    public Vector3 planeLocalOffset;
    public bool alignRotationToPlane = true;
}

public partial class ModuleController
{
    [Header("Anchored Elements")]
    [SerializeField]
    private List<ModuleElement> anchoredElements = new List<ModuleElement>();

    private void GetEdgeCorners(ModuleEdge edge, out Transform from, out Transform to)
    {
        switch (edge)
        {
            case ModuleEdge.Bottom: from = bottomLeft;  to = bottomRight; break;
            case ModuleEdge.Right:  from = bottomRight; to = topRight;    break;
            case ModuleEdge.Top:    from = topRight;    to = topLeft;     break;
            case ModuleEdge.Left:   from = topLeft;     to = bottomLeft;  break;
            default:                from = null;        to = null;        break;
        }
    }

    private IEnumerable<ModuleElement> GetAllAnchoredElements()
    {
        return anchoredElements;
    }

    private void UpdateAnchoredElements()
    {
        if (!HasRequiredReferences())
        {
            return;
        }

        foreach (ModuleElement element in GetAllAnchoredElements())
        {
            if (element?.transform == null) continue;

            Vector3 anchorWorldPosition;
            if (element.edge == ModuleEdge.Center)
            {
                anchorWorldPosition = planeReference.position;
            }
            else
            {
                GetEdgeCorners(element.edge, out Transform from, out Transform to);
                if (from == null || to == null) continue;

                anchorWorldPosition = ComputeAnchoredWorldPosition(element, from, to);
            }

            element.transform.position = anchorWorldPosition + planeReference.TransformVector(element.planeLocalOffset);

            if (element.alignRotationToPlane)
                element.transform.rotation = planeReference.rotation;
        }
    }

    public void RefreshAnchoredElements()
    {
        UpdateAnchoredElements();
    }

    private Vector3 ComputeAnchoredWorldPosition(ModuleElement element, Transform from, Transform to)
    {
        Vector3 edgeWorld = Vector3.Lerp(from.position, to.position, element.positionRatio);
        return planeReference.TransformPoint(
            new Vector3(
                planeReference.InverseTransformPoint(edgeWorld).x,
                planeReference.InverseTransformPoint(edgeWorld).y,
                0f
            )
        );
    }
}
