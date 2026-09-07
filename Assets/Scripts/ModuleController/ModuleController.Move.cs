using System.Collections.Generic;
using UnityEngine;

public partial class ModuleController
{
    #region Move
    public void OnGrabberGrab()
    {
        bool wasGrabbed = isGrabbed;
        isGrabbed = true;
        EndResizeInteraction();
        markers = GetMovableChildren();
        if (grabber == null || markers == null)
        {
            return;
        }

        if (!wasGrabbed)
        {
            LogMoveStarted();
        }

        for (int i = 0; i < markers.Length; i++)
        {
            if (markers[i] != null)
            {
                markers[i].SetParent(grabber, true);
            }
        }

        ModuleLayoutGenerator generator = GetLayoutGenerator(false);
        if (generator == null)
        {
            Debug.LogError("ModuleLayoutGenerator not found.");
            return;
        }

        generator.ReparentGeneratedQuads(grabber);
    }

    public void OnGrabberRelease()
    {
        bool wasGrabbed = isGrabbed;
        if (originalParent == null)
        {
            Debug.LogError("Cannot detach children: missing original parent");
            return;
        }

        if (markers == null || markers.Length == 0)
        {
            markers = GetMovableChildren();
        }

        for (int i = 0; i < markers.Length; i++)
        {
            if (markers[i] != null)
            {
                markers[i].SetParent(originalParent, true);
            }
        }
        isGrabbed = false;

        if (wasGrabbed)
        {
            LogMoveEnded();
        }

        ModuleLayoutGenerator generator = GetLayoutGenerator(false);
        if (generator == null)
        {
            Debug.LogError("ModuleLayoutGenerator not found.");
            return;
        }

        generator.ReparentGeneratedQuads();
    }

    // Ensures the module's transform is at the origin, and all children are parented to it, to prevent coordinate space issues.
    public void TransformCorrection()
    {
        if (transform.position != Vector3.zero)
        {
            GameObject[] children = new GameObject[transform.childCount];
            for (int i = 0; i < transform.childCount; i++)
            {
                children[i] = transform.GetChild(i).gameObject;
            }

            for (int i = 0; i < children.Length; i++)
            {
                children[i].transform.SetParent(null, true);
            }

            transform.position = Vector3.zero;
            transform.rotation = Quaternion.identity;

            for (int i = 0; i < children.Length; i++)
            {
                children[i].transform.SetParent(transform, true);
            }
        }
    }

    private Transform[] GetMovableChildren()
    {
        var list = new List<Transform>
        {
            bottomLeft,
            bottomRight,
            topRight,
            topLeft,
            planeReference,
            quad,
        };

        foreach (ModuleElement element in GetAllAnchoredElements())
        {
            if (element.transform != null)
                list.Add(element.transform);
        }

        return list.ToArray();
    }

    #endregion
}
