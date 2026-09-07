using UnityEngine;

public partial class ModuleController
{
    public void ConfigureReferences(
        Transform newOriginalParent,
        Transform newGrabber,
        Transform[] newMarkers
    )
    {
        originalParent = newOriginalParent;
        grabber = newGrabber;
        markers = newMarkers;
    }

    public void ConfigureReferences(
        Transform newBottomLeft,
        Transform newBottomRight,
        Transform newTopRight,
        Transform newTopLeft,
        Transform newGrabber,
        Vector3 newGrabberOffset,
        Transform newPlaneReference,
        Transform newQuad,
        float newMarkerScale
    )
    {
        bottomLeft = newBottomLeft;
        bottomRight = newBottomRight;
        topRight = newTopRight;
        topLeft = newTopLeft;
        grabber = newGrabber;
        grabberOffset = newGrabberOffset;
        planeReference = newPlaneReference;
        quad = newQuad;
        markerScale = Mathf.Max(0.001f, newMarkerScale);
        SavePrevPositions();
    }

    public bool TryGetCornerMarkers(
        out Transform outBottomLeft,
        out Transform outBottomRight,
        out Transform outTopRight,
        out Transform outTopLeft
    )
    {
        outBottomLeft = bottomLeft;
        outBottomRight = bottomRight;
        outTopRight = topRight;
        outTopLeft = topLeft;
        return outBottomLeft != null
            && outBottomRight != null
            && outTopRight != null
            && outTopLeft != null;
    }

    public bool TryGetCornerWorldPositions(
        out Vector3 outBottomLeft,
        out Vector3 outBottomRight,
        out Vector3 outTopRight,
        out Vector3 outTopLeft
    )
    {
        outBottomLeft = Vector3.zero;
        outBottomRight = Vector3.zero;
        outTopRight = Vector3.zero;
        outTopLeft = Vector3.zero;
        if (
            !TryGetCornerMarkers(
                out Transform cornerBottomLeft,
                out Transform cornerBottomRight,
                out Transform cornerTopRight,
                out Transform cornerTopLeft
            )
        )
        {
            return false;
        }

        outBottomLeft = cornerBottomLeft.position;
        outBottomRight = cornerBottomRight.position;
        outTopRight = cornerTopRight.position;
        outTopLeft = cornerTopLeft.position;
        return true;
    }

    private bool HasRequiredReferences()
    {
        return bottomLeft != null
            && bottomRight != null
            && topRight != null
            && topLeft != null
            && grabber != null
            && planeReference != null
            && quad != null;
    }
}
