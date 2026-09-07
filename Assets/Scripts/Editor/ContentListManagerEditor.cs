using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ContentListManager))]
public class ContentListManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        ContentListManager contentListManager = (ContentListManager)target;
        if (GUILayout.Button("Reset View"))
        {
            contentListManager.Reset();
        }
    }
}