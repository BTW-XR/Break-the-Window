using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ModuleController))]
public class ModuleControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        ModuleController controller = (ModuleController)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Debug Actions", EditorStyles.boldLabel);

        if (GUILayout.Button("Attach Children"))
        {
            controller.OnGrabberGrab();
        }

        if (GUILayout.Button("Detach Children"))
        {
            controller.OnGrabberRelease();
        }

        if (GUILayout.Button("Reset Positions"))
        {
            controller.ResetPositions();
        }

        if (GUILayout.Button("Regenerate Layout"))
        {
            controller.RegenerateLayout();
            EditorUtility.SetDirty(controller);
        }

        if (GUILayout.Button("Confirm Merge"))
        {
            // Creates a merged module from preview corners and disables the two source modules.
            ModuleController mergedController = controller.ConfirmMerge();
            if (mergedController != null)
            {
                Undo.RegisterCreatedObjectUndo(mergedController.gameObject, "Confirm Module Merge");
                EditorUtility.SetDirty(controller);
                EditorUtility.SetDirty(mergedController);
                Selection.activeGameObject = mergedController.gameObject;
            }
        }

        if (GUILayout.Button("Create Module From ContentId Copy"))
        {
            ModuleController copiedController = controller.CreateModuleFromDebugContentIdCopy();
            if (copiedController != null)
            {
                Undo.RegisterCreatedObjectUndo(
                    copiedController.gameObject,
                    "Create Module From ContentId Copy"
                );
                EditorUtility.SetDirty(controller);
                EditorUtility.SetDirty(copiedController);
                Selection.activeGameObject = copiedController.gameObject;
            }
        }

        if (GUILayout.Button("Create Module From ContentId Pop"))
        {
            Undo.RecordObject(controller, "Create Module From ContentId Pop");
            controller.CreateModuleFromDebugContentIdPop();
            EditorUtility.SetDirty(controller);
        }
    }
}
