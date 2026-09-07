using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CanvasManager))]
public class CanvasManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        CanvasManager canvasManager = (CanvasManager)target;
        if (GUILayout.Button("Find Target"))
        {
            canvasManager.FindTargetLeafTransformByContentId();
        }

        if (GUILayout.Button("Set Debug URL"))
        {
            canvasManager.SetURL();
        }
    }
}
