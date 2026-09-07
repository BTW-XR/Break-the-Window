using System;
using UnityEngine;

public partial class ModuleController
{
    #region Resize

    private void HandleResize()
    {
        if (!HasRequiredReferences())
        {
            return;
        }

        Transform[] cornerTransforms = new Transform[]
        {
            bottomLeft,
            bottomRight,
            topRight,
            topLeft,
        };

        int movedIndex = -1;
        float maxSqr = moveThreshold * moveThreshold;

        Vector3[] cornersLocalPositions = new Vector3[4];
        for (int i = 0; i < 4; i++)
        {
            cornersLocalPositions[i] = ToPlaneSpace(cornerTransforms[i]);
        }

        // Snap corners to the plane and find the corner with the largest movement that exceeds the threshold.
        for (int i = 0; i < 4; i++)
        {
            cornerTransforms[i].position = planeReference.TransformPoint(
                new Vector3(cornersLocalPositions[i].x, cornersLocalPositions[i].y, 0f)
            );
            float d = (cornerTransforms[i].position - prevPositions[i]).sqrMagnitude;
            if (d > maxSqr)
            {
                maxSqr = d;
                movedIndex = i;
            }
        }

        if (movedIndex == -1)
        {
            EndResizeInteraction();
            for (int i = 0; i < 4; i++)
            {
                prevPositions[i] = cornerTransforms[i].position;
            }
            return;
        }

        BeginResizeInteraction();

        int fixedIndex = (movedIndex + 2) % 4;
        Debug.Log(
            $"Moved marker: {indexToName[movedIndex]}, fixed marker: {indexToName[fixedIndex]}"
        );
        int idxNext = (movedIndex + 1) % 4;
        int idxPrev = (movedIndex + 3) % 4;

        Vector3 movedLocal = cornersLocalPositions[movedIndex];
        Vector3 fixedLocal = cornersLocalPositions[fixedIndex];

        Debug.Log($"Moved local: {movedLocal}, Fixed local: {fixedLocal}");

        // Calculate new size based on the moved and fixed corners.
        // The size is determined by the distance between the moved and fixed corners in the plane's local space.
        float sizeX =
            cornersLocalPositions[Math.Max(fixedIndex, movedIndex)].x
            - cornersLocalPositions[Math.Min(fixedIndex, movedIndex)].x;
        float sizeY =
            cornersLocalPositions[Math.Max(fixedIndex, movedIndex)].y
            - cornersLocalPositions[Math.Min(fixedIndex, movedIndex)].y;

        sizeX *= (movedIndex == 0 || movedIndex == 2) ? 1f : -1f;

        if (sizeX < 0.1f || sizeY < 0.1f)
        {
            cornerTransforms[movedIndex].position = prevPositions[movedIndex];
            return;
        }

        Vector3 yChangeLocal = new Vector3(fixedLocal.x, movedLocal.y, 0f);
        Vector3 xChangeLocal = new Vector3(movedLocal.x, fixedLocal.y, 0f);
        if (movedIndex == 0 || movedIndex == 2)
        {
            cornerTransforms[idxNext].position = planeReference.TransformPoint(yChangeLocal);
            cornerTransforms[idxPrev].position = planeReference.TransformPoint(xChangeLocal);
        }
        else
        {
            cornerTransforms[idxNext].position = planeReference.TransformPoint(xChangeLocal);
            cornerTransforms[idxPrev].position = planeReference.TransformPoint(yChangeLocal);
        }

        Vector3 center =
            (cornerTransforms[fixedIndex].position + cornerTransforms[movedIndex].position) / 2f;

        Debug.Log($"Center: {center}, SizeX: {sizeX}, SizeY: {sizeY}");

        ModuleLayoutGenerator generator = GetLayoutGenerator(false);
        bool leafQuadsAttachedToQuad = generator != null && quad != null;
        if (leafQuadsAttachedToQuad)
        {
            // During resize, temporarily parent leaf quads to quad so they inherit
            // the exact scale transform being applied to the quad reference.
            generator.ReparentGeneratedQuads(quad);
        }

        try
        {
            SetPlaneRefTransform(center);
            SetQuadTransform(center, sizeX, sizeY);
            SetGrabberTransform();
        }
        finally
        {
            if (leafQuadsAttachedToQuad)
            {
                // Return generated quads to LayoutRoot after resize so hierarchy stays clean.
                generator.ReparentGeneratedQuads();
            }
        }

        SavePrevPositions();
        UpdateAnchoredElements();
    }

    private void SavePrevPositions()
    {
        if (bottomLeft != null)
        {
            prevPositions[0] = bottomLeft.position;
        }
        if (bottomRight != null)
        {
            prevPositions[1] = bottomRight.position;
        }
        if (topRight != null)
        {
            prevPositions[2] = topRight.position;
        }
        if (topLeft != null)
        {
            prevPositions[3] = topLeft.position;
        }
    }

    private Vector3 ToPlaneSpace(Transform t)
    {
        return planeReference.InverseTransformPoint(t.position);
    }

    private void SetQuadTransform(Vector3 center, float sizeX, float sizeY)
    {
        quad.position = center;
        quad.localScale = new Vector3(sizeX, sizeY, 1f);
    }

    private void SetGrabberTransform()
    {
        grabber.position = planeReference.TransformPoint(
            (ToPlaneSpace(bottomLeft) + ToPlaneSpace(bottomRight)) / 2f + grabberOffset
        );
    }

    private void SetPlaneRefTransform(Vector3 center)
    {
        planeReference.position = center;
    }
    #endregion
}
