using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Enemies.Navigation
{
#if UNITY_EDITOR
    // Editor tool for creating and managing waypoints
    [CustomEditor(typeof(WaypointSystem))]
    public class WaypointSystemEditor : Editor
    {
        private enum WaypointCreationMode
        {
            Standard,
            LadderBottom,
            LadderTop,
            JumpPoint,
            LandingPoint
        }
        
        private WaypointCreationMode creationMode = WaypointCreationMode.Standard;
        private Waypoint selectedWaypoint = null;
        private bool autoConnectNewWaypoints = true;
        private float autoConnectRadius = 5f;
        
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            
            WaypointSystem waypointSystem = (WaypointSystem)target;
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Waypoint Creation", EditorStyles.boldLabel);
            
            // Waypoint creation settings
            creationMode = (WaypointCreationMode)EditorGUILayout.EnumPopup("Waypoint Type", creationMode);
            autoConnectNewWaypoints = EditorGUILayout.Toggle("Auto-Connect", autoConnectNewWaypoints);
            
            if (autoConnectNewWaypoints)
            {
                autoConnectRadius = EditorGUILayout.FloatField("Connection Radius", autoConnectRadius);
            }
            
            // Create waypoint button
            if (GUILayout.Button("Create Waypoint at Scene View Position"))
            {
                CreateWaypointAtSceneViewPosition();
            }
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Ladder Connections", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Select two waypoints (one LadderBottom and one LadderTop) and link them.", MessageType.Info);
            
            // Link ladder waypoints
            if (GUILayout.Button("Link Selected Ladder Waypoints"))
            {
                LinkSelectedLadderWaypoints();
            }
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Waypoint Management", EditorStyles.boldLabel);
            
            // Refresh waypoints
            if (GUILayout.Button("Refresh Waypoints"))
            {
                waypointSystem.RefreshWaypoints();
            }
            
            // Show waypoint count
            Waypoint[] allWaypoints = FindObjectsOfType<Waypoint>();
            EditorGUILayout.LabelField($"Total Waypoints: {allWaypoints.Length}");
        }
        
        private void CreateWaypointAtSceneViewPosition()
        {
            // Get scene view position
            Vector3 position = SceneView.lastActiveSceneView.camera.transform.position;
            position.z = 0; // Ensure it's in 2D space
            
            // Create new waypoint
            GameObject waypointObj = new GameObject($"Waypoint_{creationMode}");
            waypointObj.transform.position = position;
            
            // Add waypoint component
            Waypoint waypoint = waypointObj.AddComponent<Waypoint>();
            
            // Set waypoint type
            SerializedObject serializedWaypoint = new SerializedObject(waypoint);
            SerializedProperty typeProp = serializedWaypoint.FindProperty("type");
            typeProp.enumValueIndex = (int)System.Enum.Parse(typeof(WaypointType), creationMode.ToString());
            
            // Set auto-connect properties
            SerializedProperty autoConnectProp = serializedWaypoint.FindProperty("autoConnectInRadius");
            SerializedProperty radiusProp = serializedWaypoint.FindProperty("connectionRadius");
            
            autoConnectProp.boolValue = autoConnectNewWaypoints;
            radiusProp.floatValue = autoConnectRadius;
            
            // Apply changes
            serializedWaypoint.ApplyModifiedProperties();
            
            // Set waypoint color based on type
            SerializedProperty colorProp = serializedWaypoint.FindProperty("gizmoColor");
            switch (creationMode)
            {
                case WaypointCreationMode.Standard:
                    colorProp.colorValue = Color.cyan;
                    break;
                case WaypointCreationMode.LadderBottom:
                    colorProp.colorValue = Color.blue;
                    break;
                case WaypointCreationMode.LadderTop:
                    colorProp.colorValue = Color.red;
                    break;
                case WaypointCreationMode.JumpPoint:
                    colorProp.colorValue = Color.green;
                    break;
                case WaypointCreationMode.LandingPoint:
                    colorProp.colorValue = Color.yellow;
                    break;
            }
            
            serializedWaypoint.ApplyModifiedProperties();
            
            // Auto-connect to nearby waypoints
            if (autoConnectNewWaypoints)
            {
                Waypoint[] allWaypoints = FindObjectsOfType<Waypoint>();
                List<Waypoint> connections = new List<Waypoint>();
                
                foreach (Waypoint other in allWaypoints)
                {
                    if (other != waypoint)
                    {
                        float distance = Vector2.Distance(waypoint.transform.position, other.transform.position);
                        if (distance <= autoConnectRadius)
                        {
                            connections.Add(other);
                        }
                    }
                }
                
                // Set connections
                SerializedProperty connectionsProp = serializedWaypoint.FindProperty("connections");
                connectionsProp.ClearArray();
                
                for (int i = 0; i < connections.Count; i++)
                {
                    connectionsProp.arraySize++;
                    connectionsProp.GetArrayElementAtIndex(i).objectReferenceValue = connections[i];
                }
                
                serializedWaypoint.ApplyModifiedProperties();
            }
            
            // Select the new waypoint
            Selection.activeGameObject = waypointObj;
            
            Debug.Log($"Created waypoint of type {creationMode} at {position}");
        }
        
        private void LinkSelectedLadderWaypoints()
        {
            GameObject[] selection = Selection.gameObjects;
            if (selection.Length != 2)
            {
                EditorUtility.DisplayDialog("Link Error", "You must select exactly 2 waypoints to link them.", "OK");
                return;
            }
            
            Waypoint waypointA = selection[0].GetComponent<Waypoint>();
            Waypoint waypointB = selection[1].GetComponent<Waypoint>();
            
            if (waypointA == null || waypointB == null)
            {
                EditorUtility.DisplayDialog("Link Error", "Both selected objects must have Waypoint components.", "OK");
                return;
            }
            
            // Get waypoint types
            SerializedObject serializedA = new SerializedObject(waypointA);
            SerializedObject serializedB = new SerializedObject(waypointB);
            
            SerializedProperty typeA = serializedA.FindProperty("type");
            SerializedProperty typeB = serializedB.FindProperty("type");
            
            // Check if they are ladder top/bottom pairs
            bool isValidPair = (typeA.enumValueIndex == (int)WaypointType.LadderBottom && typeB.enumValueIndex == (int)WaypointType.LadderTop) ||
                              (typeA.enumValueIndex == (int)WaypointType.LadderTop && typeB.enumValueIndex == (int)WaypointType.LadderBottom);
                              
            if (!isValidPair)
            {
                EditorUtility.DisplayDialog("Link Error", "You must select one LadderBottom and one LadderTop waypoint to link them.", "OK");
                return;
            }
            
            // Link them
            SerializedProperty linkedA = serializedA.FindProperty("linkedWaypoint");
            SerializedProperty linkedB = serializedB.FindProperty("linkedWaypoint");
            
            linkedA.objectReferenceValue = waypointB;
            linkedB.objectReferenceValue = waypointA;
            
            serializedA.ApplyModifiedProperties();
            serializedB.ApplyModifiedProperties();
            
            Debug.Log($"Linked waypoints: {waypointA.name} and {waypointB.name}");
        }
    }
    
    // Editor for individual waypoints
    [CustomEditor(typeof(Waypoint))]
    public class WaypointEditor : Editor
    {
        private bool showConnections = true;
        
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            
            Waypoint waypoint = (Waypoint)target;
            
            EditorGUILayout.Space();
            showConnections = EditorGUILayout.Foldout(showConnections, "Connections");
            
            if (showConnections)
            {
                EditorGUI.indentLevel++;
                
                // Show all connections
                SerializedProperty connectionsProp = serializedObject.FindProperty("connections");
                for (int i = 0; i < connectionsProp.arraySize; i++)
                {
                    EditorGUILayout.PropertyField(connectionsProp.GetArrayElementAtIndex(i), new GUIContent($"Connection {i+1}"));
                }
                
                // Add connection button
                if (GUILayout.Button("Add Connection"))
                {
                    connectionsProp.arraySize++;
                }
                
                // Clear connections button
                if (GUILayout.Button("Clear Connections"))
                {
                    if (EditorUtility.DisplayDialog("Clear Connections", "Are you sure you want to clear all connections?", "Yes", "No"))
                    {
                        connectionsProp.ClearArray();
                    }
                }
                
                EditorGUI.indentLevel--;
            }
            
            // Auto-connection utilities
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Connection Utilities", EditorStyles.boldLabel);
            
            SerializedProperty radiusProp = serializedObject.FindProperty("connectionRadius");
            
            if (GUILayout.Button("Connect Nearby Waypoints"))
            {
                ConnectNearbyWaypoints(waypoint, radiusProp.floatValue);
            }
            
            if (GUILayout.Button("Two-Way Connect Selected Waypoints"))
            {
                ConnectSelectedWaypoints(waypoint);
            }
            
            // Apply changes
            serializedObject.ApplyModifiedProperties();
        }
        
        private void ConnectNearbyWaypoints(Waypoint waypoint, float radius)
        {
            // Find all waypoints in radius
            Waypoint[] allWaypoints = FindObjectsOfType<Waypoint>();
            List<Waypoint> nearbyWaypoints = new List<Waypoint>();
            
            foreach (Waypoint other in allWaypoints)
            {
                if (other != waypoint)
                {
                    float distance = Vector2.Distance(waypoint.transform.position, other.transform.position);
                    if (distance <= radius)
                    {
                        nearbyWaypoints.Add(other);
                    }
                }
            }
            
            // Get current connections
            SerializedProperty connectionsProp = serializedObject.FindProperty("connections");
            List<Waypoint> currentConnections = new List<Waypoint>();
            
            for (int i = 0; i < connectionsProp.arraySize; i++)
            {
                Waypoint connection = connectionsProp.GetArrayElementAtIndex(i).objectReferenceValue as Waypoint;
                if (connection != null)
                {
                    currentConnections.Add(connection);
                }
            }
            
            // Add new connections
            bool changed = false;
            foreach (Waypoint nearby in nearbyWaypoints)
            {
                if (!currentConnections.Contains(nearby))
                {
                    connectionsProp.arraySize++;
                    connectionsProp.GetArrayElementAtIndex(connectionsProp.arraySize - 1).objectReferenceValue = nearby;
                    changed = true;
                }
            }
            
            if (changed)
            {
                serializedObject.ApplyModifiedProperties();
                Debug.Log($"Added {nearbyWaypoints.Count - currentConnections.Count} new connections to {waypoint.name}");
            }
            else
            {
                Debug.Log("No new connections added.");
            }
        }
        
        private void ConnectSelectedWaypoints(Waypoint waypoint)
        {
            GameObject[] selection = Selection.gameObjects;
            List<Waypoint> selectedWaypoints = new List<Waypoint>();
            
            // Get all selected waypoints
            foreach (GameObject obj in selection)
            {
                Waypoint selectedWaypoint = obj.GetComponent<Waypoint>();
                if (selectedWaypoint != null && selectedWaypoint != waypoint)
                {
                    selectedWaypoints.Add(selectedWaypoint);
                }
            }
            
            if (selectedWaypoints.Count == 0)
            {
                EditorUtility.DisplayDialog("Connect Error", "No other waypoints selected.", "OK");
                return;
            }
            
            // Add two-way connections
            SerializedProperty connectionsProp = serializedObject.FindProperty("connections");
            List<Waypoint> currentConnections = new List<Waypoint>();
            
            for (int i = 0; i < connectionsProp.arraySize; i++)
            {
                Waypoint connection = connectionsProp.GetArrayElementAtIndex(i).objectReferenceValue as Waypoint;
                if (connection != null)
                {
                    currentConnections.Add(connection);
                }
            }
            
            // Add connections from this waypoint to selected
            bool changed = false;
            foreach (Waypoint selected in selectedWaypoints)
            {
                if (!currentConnections.Contains(selected))
                {
                    connectionsProp.arraySize++;
                    connectionsProp.GetArrayElementAtIndex(connectionsProp.arraySize - 1).objectReferenceValue = selected;
                    changed = true;
                }
                
                // Add connection from selected to this (requires changing the other waypoint)
                SerializedObject selectedSerialized = new SerializedObject(selected);
                SerializedProperty selectedConnectionsProp = selectedSerialized.FindProperty("connections");
                
                bool selectedChanged = false;
                bool alreadyConnected = false;
                
                for (int i = 0; i < selectedConnectionsProp.arraySize; i++)
                {
                    Waypoint connection = selectedConnectionsProp.GetArrayElementAtIndex(i).objectReferenceValue as Waypoint;
                    if (connection == waypoint)
                    {
                        alreadyConnected = true;
                        break;
                    }
                }
                
                if (!alreadyConnected)
                {
                    selectedConnectionsProp.arraySize++;
                    selectedConnectionsProp.GetArrayElementAtIndex(selectedConnectionsProp.arraySize - 1).objectReferenceValue = waypoint;
                    selectedChanged = true;
                }
                
                if (selectedChanged)
                {
                    selectedSerialized.ApplyModifiedProperties();
                }
            }
            
            if (changed)
            {
                serializedObject.ApplyModifiedProperties();
                Debug.Log($"Added two-way connections between {waypoint.name} and {selectedWaypoints.Count} other waypoints");
            }
            else
            {
                Debug.Log("No new connections added.");
            }
        }
    }
#endif
}