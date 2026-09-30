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

        Vector3[] cornersLocalPositions = new Vector3[4];
        for (int i = 0; i < 4; i++)
        {
            cornersLocalPositions[i] = ToPlaneSpace(cornerTransforms[i]);
        }

        int movedIndex = activeResizeCorner;
        if (movedIndex < 0)
        {
            // No corner handle is being dragged: detect which corner moved.
            float maxSqr = moveThreshold * moveThreshold;
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
        }

        BeginResizeInteraction();

        int fixedIndex = (movedIndex + 2) % 4;
        int idxNext = (movedIndex + 1) % 4;
        int idxPrev = (movedIndex + 3) % 4;

        Vector3 fixedLocal = cornersLocalPositions[fixedIndex];

        // Content corner: while a handle is dragged, ease toward the grid-snapped position
        // derived from the smoothed handle proxy, so the panel dimension glides to the
        // nearest grid step instead of snapping instantly.
        Vector3 movedLocal;
        if (activeResizeCorner >= 0 && cornerProxy != null)
        {
            Vector3 rawMovedLocal = ToPlaneSpace(cornerProxy.position);
            Vector3 snappedLocal = cornerTargetingGrid
                ? cornerGridTargetLocal
                : SnapMovedCorner(rawMovedLocal, fixedLocal);
            float t = 1f - Mathf.Exp(-contentSnapRate * Time.deltaTime);
            contentMovedLocal = Vector3.Lerp(contentMovedLocal, snappedLocal, t);
            movedLocal = contentMovedLocal;
        }
        else
        {
            movedLocal = cornersLocalPositions[movedIndex];
        }

        float sizeX = Mathf.Abs(movedLocal.x - fixedLocal.x);
        float sizeY = Mathf.Abs(movedLocal.y - fixedLocal.y);
        if (sizeX < 0.0001f || sizeY < 0.0001f)
        {
            if (activeResizeCorner < 0)
            {
                cornerTransforms[movedIndex].position = prevPositions[movedIndex];
            }
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
            (cornerTransforms[fixedIndex].position + planeReference.TransformPoint(movedLocal)) / 2f;

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

    private Vector3 ToPlaneSpace(Vector3 worldPosition)
    {
        return planeReference.InverseTransformPoint(worldPosition);
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
