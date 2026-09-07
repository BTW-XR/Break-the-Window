using System;
using UnityEngine;

public partial class ModuleController
{
    public void ResetPositions()
    {
        if (!HasRequiredReferences())
        {
            Debug.LogError("Cannot reset positions: missing references");
            return;
        }
        OnGrabberRelease();

        planeReference.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        bottomLeft.SetLocalPositionAndRotation(new Vector3(-0.2f, -0.2f, 0f), Quaternion.identity);
        bottomRight.SetLocalPositionAndRotation(new Vector3(0.2f, -0.2f, 0f), Quaternion.identity);
        topRight.SetLocalPositionAndRotation(new Vector3(0.2f, 0.2f, 0f), Quaternion.identity);
        topLeft.SetLocalPositionAndRotation(new Vector3(-0.2f, 0.2f, 0f), Quaternion.identity);
        bottomLeft.localScale = Vector3.one * markerScale;
        bottomRight.localScale = Vector3.one * markerScale;
        topRight.localScale = Vector3.one * markerScale;
        topLeft.localScale = Vector3.one * markerScale;

        grabber.rotation = Quaternion.identity;
        quad.rotation = Quaternion.identity;

        HandleResize();
        SavePrevPositions();

        RegenerateLayout();
        UpdateAnchoredElements();
    }


}