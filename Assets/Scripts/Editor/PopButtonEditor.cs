using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PopButton))]
public class PopButtonEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        PopButton popButton = (PopButton)target;
        if (GUILayout.Button("Pop"))
        {
            popButton.OnInteract();
        }
    }
}
