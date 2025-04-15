using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(DebugManager))]
public class DebugManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        DebugManager debugManager = (DebugManager)target;

        if (GUILayout.Button("Add 10000 Honor"))
        {
            debugManager.Add10000Honor();
        }

        if (GUILayout.Button("Reset Honor"))
        {
            debugManager.ResetHonor();
        }

        if (GUILayout.Button("Reset All"))
        {
            debugManager.ResetAll();
        }
    }
}
