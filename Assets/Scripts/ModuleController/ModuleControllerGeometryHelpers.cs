using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Centralized geometry helpers used by <see cref="ModuleController"/>.
/// Keeping these calculations in one place reduces duplicated math paths,
/// which keeps merge detection and preview orientation behavior consistent.
/// </summary>
internal static class ModuleControllerGeometryHelpers
{
    /// <summary>
    /// Returns edge midpoint positions for a module in clockwise order:
    /// bottom, right, top, left.
    /// This ordering matches ModuleController edge index conventions.
    /// </summary>
    public static Vector3[] BuildEdgeMidpoints(
        Transform bottomLeft,
        Transform bottomRight,
        Transform topRight,
        Transform topLeft
    )
    {
        return new Vector3[]
        {
            (bottomRight.position + bottomLeft.position) / 2f,
            (topRight.position + bottomRight.position) / 2f,
            (topLeft.position + topRight.position) / 2f,
            (bottomLeft.position + topLeft.position) / 2f,
        };
    }

    /// <summary>
    /// Finds which edge pair (source edge i vs. target edge i+2) has the shortest
    /// midpoint distance. The key is the source edge index and the value is distance.
    /// </summary>
    public static KeyValuePair<int, float> FindShortestOppositeEdgeDistance(
        Vector3[] sourceEdgeMidpoints,
        Vector3[] targetEdgeMidpoints
    )
    {
        float minDistance = float.MaxValue;
        int minIndex = -1;

        for (int i = 0; i < 4; i++)
        {
            Vector3 edge1 = sourceEdgeMidpoints[i];
            Vector3 edge2 = targetEdgeMidpoints[(i + 2) % 4];
            float distance = Vector3.Distance(edge1, edge2);
            if (distance < minDistance)
            {
                minDistance = distance;
                minIndex = i;
            }
        }

        return new KeyValuePair<int, float>(minIndex, minDistance);
    }

    /// <summary>
    /// Builds an orthonormal basis for a quad from BL/BR/TL corners and returns
    /// width and height. Returns false when the input is degenerate.
    /// </summary>
    public static bool TryBuildQuadBasis(
        Vector3 bottomLeft,
        Vector3 bottomRight,
        Vector3 topLeft,
        out Vector3 right,
        out Vector3 up,
        out Vector3 forward,
        out float width,
        out float height
    )
    {
        right = bottomRight - bottomLeft;
        up = topLeft - bottomLeft;
        width = right.magnitude;
        height = up.magnitude;

        forward = Vector3.zero;
        if (width < 0.0001f || height < 0.0001f)
        {
            return false;
        }

        right /= width;
        up /= height;
        forward = Vector3.Cross(right, up).normalized;
        return forward.sqrMagnitude >= 0.0001f;
    }
}
