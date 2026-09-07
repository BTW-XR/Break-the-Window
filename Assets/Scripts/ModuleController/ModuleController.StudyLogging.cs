using UnityEngine;

public partial class ModuleController
{
    private void LogMoveStarted()
    {
        if (DataCollector.Active == null)
        {
            return;
        }

        if (DataCollector.Active.TryGetTrackedModuleObjectId(this, out string objectId))
        {
            DataCollector.Active.LogInteraction("move_started", objectId, "Module");
        }
    }

    private void LogMoveEnded()
    {
        if (DataCollector.Active == null)
        {
            return;
        }

        if (DataCollector.Active.TryGetTrackedModuleObjectId(this, out string objectId))
        {
            DataCollector.Active.LogInteraction("move_ended", objectId, "Module");
        }
    }

    private void BeginResizeInteraction()
    {
        if (isResizing)
        {
            return;
        }

        isResizing = true;

        if (DataCollector.Active == null)
        {
            return;
        }

        if (DataCollector.Active.TryGetTrackedModuleObjectId(this, out string objectId))
        {
            DataCollector.Active.LogInteraction("resize_started", objectId, "Module");
        }
    }

    private void EndResizeInteraction()
    {
        if (!isResizing)
        {
            return;
        }

        isResizing = false;

        if (DataCollector.Active == null)
        {
            return;
        }

        if (DataCollector.Active.TryGetTrackedModuleObjectId(this, out string objectId))
        {
            DataCollector.Active.LogInteraction("resize_ended", objectId, "Module");
        }
    }
}
