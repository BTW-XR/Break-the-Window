using UnityEngine;

public partial class ModuleController
{
    private void OnEnable()
    {
        if (!instances.Contains(this))
        {
            instances.Add(this);
            Debug.Log($"ModuleController enabled. Total instances: {instances.Count}");
        }

        if (layoutTree == null)
        {
            layoutTree = ModuleLayoutTree.CreateDefault();
        }
        else
        {
            layoutTree.Validate();
        }

        edges = new Edge[]
        {
            new Edge(bottomLeft, bottomRight, 0),
            new Edge(bottomRight, topRight, 1),
            new Edge(topRight, topLeft, 2),
            new Edge(topLeft, bottomLeft, 3),
        };

        if (placementInteractable != null)
        {
            placementInteractable.selectEntered.AddListener(OnSurfacePlacementSelect);
            placementInteractable.selectExited.AddListener(OnSurfacePlacementUnselect);
        }
        else if (Application.isPlaying)
        {
            Debug.LogError($"{name}: 'Placement Interactable' is not assigned.", this);
        }


        RegenerateLayout();
        RefreshAnchoredElements();
    }

    private void OnDisable()
    {
        if (placementInteractable != null)
        {
            placementInteractable.selectEntered.RemoveListener(OnSurfacePlacementSelect);
            placementInteractable.selectExited.RemoveListener(OnSurfacePlacementUnselect);
        }

        ClearMergePreview();
        instances.Remove(this);
    }
}
