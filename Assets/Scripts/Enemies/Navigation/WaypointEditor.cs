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
            LandingPoint,
            EdgeTop,      // NEW: Edge top for jumping down
            EdgeBottom    // NEW: Edge bottom landing point
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
            EditorGUILayout.LabelField("Waypoint Connections", EditorStyles.boldLabel);
            
            // Ladder connections section
            EditorGUILayout.LabelField("Ladder Connections", EditorStyles.miniBoldLabel);
            EditorGUILayout.HelpBox("Select two waypoints (one LadderBottom and one LadderTop) and link them.", MessageType.Info);
            
            // Link ladder waypoints
            if (GUILayout.Button("Link Selected Ladder Waypoints"))
            {
                LinkSelectedWaypoints(WaypointType.LadderBottom, WaypointType.LadderTop);
            }
            
            // Edge connections section (NEW)
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Edge Connections", EditorStyles.miniBoldLabel);
            EditorGUILayout.HelpBox("Select two waypoints (one EdgeTop and one EdgeBottom) and link them.", MessageType.Info);
            
            // Link edge waypoints (NEW)
            if (GUILayout.Button("Link Selected Edge Waypoints"))
            {
                LinkSelectedWaypoints(WaypointType.EdgeTop, WaypointType.EdgeBottom);
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
            
            // Special property for EdgeTop waypoints - NEW
            if (creationMode == WaypointCreationMode.EdgeTop)
            {
                SerializedProperty allowDownwardOnlyProp = serializedWaypoint.FindProperty("allowDownwardOnly");
                if (allowDownwardOnlyProp != null)
                {
                    allowDownwardOnlyProp.boolValue = true;
                }
            }
            
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
                case WaypointCreationMode.EdgeTop:    // NEW
                    colorProp.colorValue = new Color(1f, 0.5f, 0f); // Orange
                    break;
                case WaypointCreationMode.EdgeBottom: // NEW
                    colorProp.colorValue = new Color(0.5f, 0f, 1f); // Purple
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
                            // Special handling for EdgeTop waypoints (only connect to same level or EdgeBottom below) - NEW
                            if (creationMode == WaypointCreationMode.EdgeTop)
                            {
                                WaypointType otherType = (WaypointType)serializedWaypoint.FindProperty("type").enumValueIndex;
                                if (otherType == WaypointType.EdgeBottom && other.transform.position.y < waypoint.transform.position.y)
                                {
                                    connections.Add(other); // Connect to EdgeBottom below
                                }
                                else if (otherType != WaypointType.EdgeBottom && 
                                       Mathf.Abs(other.transform.position.y - waypoint.transform.position.y) < 1.0f)
                                {
                                    connections.Add(other); // Connect to same-level waypoints
                                }
                            }
                            // Special handling for EdgeBottom waypoints (don't connect to EdgeTop above) - NEW
                            else if (creationMode == WaypointCreationMode.EdgeBottom)
                            {
                                SerializedObject otherSerialized = new SerializedObject(other);
                                int otherTypeIndex = otherSerialized.FindProperty("type").enumValueIndex;
                                
                                if (otherTypeIndex == (int)WaypointType.EdgeTop && 
                                    other.transform.position.y > waypoint.transform.position.y)
                                {
                                    // Don't connect to EdgeTop above
                                }
                                else
                                {
                                    connections.Add(other);
                                }
                            }
                            else
                            {
                                connections.Add(other);
                            }
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
        
        // Modified to accept any pair of waypoint types - NEW
        private void LinkSelectedWaypoints(WaypointType typeA, WaypointType typeB)
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
            
            SerializedProperty typeAProp = serializedA.FindProperty("type");
            SerializedProperty typeBProp = serializedB.FindProperty("type");
            
            WaypointType typeAValue = (WaypointType)typeAProp.enumValueIndex;
            WaypointType typeBValue = (WaypointType)typeBProp.enumValueIndex;
            
            // Check if they are a valid pair
            bool isValidPair = (typeAValue == typeA && typeBValue == typeB) || 
                               (typeAValue == typeB && typeBValue == typeA);
                              
            if (!isValidPair)
            {
                string expectedPair = $"one {typeA} and one {typeB}";
                EditorUtility.DisplayDialog("Link Error", $"You must select {expectedPair} waypoint to link them.", "OK");
                return;
            }
            
            // Link them
            SerializedProperty linkedA = serializedA.FindProperty("linkedWaypoint");
            SerializedProperty linkedB = serializedB.FindProperty("linkedWaypoint");
            
            linkedA.objectReferenceValue = waypointB;
            linkedB.objectReferenceValue = waypointA;
            
            serializedA.ApplyModifiedProperties();
            serializedB.ApplyModifiedProperties();
            
            // For EdgeTop and EdgeBottom, ensure vertical connection
            if ((typeAValue == WaypointType.EdgeTop && typeBValue == WaypointType.EdgeBottom) ||
                (typeAValue == WaypointType.EdgeBottom && typeBValue == WaypointType.EdgeTop))
            {
                // Add one-way connection from EdgeTop to EdgeBottom
                Waypoint edgeTop = typeAValue == WaypointType.EdgeTop ? waypointA : waypointB;
                Waypoint edgeBottom = typeAValue == WaypointType.EdgeBottom ? waypointA : waypointB;
                
                // Use serialized objects to add connection
                SerializedObject edgeTopObj = new SerializedObject(edgeTop);
                SerializedProperty connectionsArr = edgeTopObj.FindProperty("connections");
                
                // Check if connection already exists
                bool connectionExists = false;
                for (int i = 0; i < connectionsArr.arraySize; i++)
                {
                    SerializedProperty connection = connectionsArr.GetArrayElementAtIndex(i);
                    if (connection.objectReferenceValue == edgeBottom)
                    {
                        connectionExists = true;
                        break;
                    }
                }
                
                // Add connection if it doesn't exist
                if (!connectionExists)
                {
                    connectionsArr.arraySize++;
                    connectionsArr.GetArrayElementAtIndex(connectionsArr.arraySize - 1).objectReferenceValue = edgeBottom;
                    edgeTopObj.ApplyModifiedProperties();
                }
                
                Debug.Log($"Created one-way connection from {edgeTop.name} (EdgeTop) to {edgeBottom.name} (EdgeBottom)");
            }
            
            Debug.Log($"Linked waypoints: {waypointA.name} and {waypointB.name}");
        }
        
        // Legacy method kept for compatibility
        private void LinkSelectedLadderWaypoints()
        {
            LinkSelectedWaypoints(WaypointType.LadderBottom, WaypointType.LadderTop);
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
            
            // Edge-specific connections (NEW)
            EditorGUILayout.Space();
            SerializedProperty typeProp = serializedObject.FindProperty("type");
            if (typeProp != null)
            {
                WaypointType waypointType = (WaypointType)typeProp.enumValueIndex;
                
                if (waypointType == WaypointType.EdgeTop)
                {
                    EditorGUILayout.LabelField("EdgeTop Utilities", EditorStyles.miniBoldLabel);
                    if (GUILayout.Button("Find EdgeBottom Below"))
                    {
                        FindEdgeBottomBelow(waypoint);
                    }
                }
            }
            
            // Apply changes
            serializedObject.ApplyModifiedProperties();
        }
        
        // NEW: Helper to find and connect to EdgeBottom waypoints below this EdgeTop
        private void FindEdgeBottomBelow(Waypoint waypoint)
        {
            // Only works for EdgeTop waypoints
            SerializedProperty typeProp = serializedObject.FindProperty("type");
            if ((WaypointType)typeProp.enumValueIndex != WaypointType.EdgeTop)
            {
                EditorUtility.DisplayDialog("Not EdgeTop", "This utility only works for EdgeTop waypoints.", "OK");
                return;
            }
            
            // Find all EdgeBottom waypoints
            Waypoint[] allWaypoints = FindObjectsOfType<Waypoint>();
            List<Waypoint> edgeBottomsBelow = new List<Waypoint>();
            
            foreach (Waypoint other in allWaypoints)
            {
                SerializedObject otherObject = new SerializedObject(other);
                SerializedProperty otherTypeProp = otherObject.FindProperty("type");
                
                if (otherTypeProp != null && (WaypointType)otherTypeProp.enumValueIndex == WaypointType.EdgeBottom)
                {
                    // Check if EdgeBottom is below and within reasonable horizontal distance
                    if (other.transform.position.y < waypoint.transform.position.y &&
                        Mathf.Abs(other.transform.position.x - waypoint.transform.position.x) < 3.0f)
                    {
                        edgeBottomsBelow.Add(other);
                    }
                }
            }
            
            if (edgeBottomsBelow.Count == 0)
            {
                EditorUtility.DisplayDialog("No EdgeBottom Found", 
                    "No EdgeBottom waypoints found below this EdgeTop. Create an EdgeBottom waypoint below this one first.", 
                    "OK");
                return;
            }
            
            // Sort by distance
            edgeBottomsBelow.Sort((a, b) => 
                Vector2.Distance(waypoint.transform.position, a.transform.position)
                    .CompareTo(Vector2.Distance(waypoint.transform.position, b.transform.position)));
            
            // Connect to the closest EdgeBottom
            Waypoint closestEdgeBottom = edgeBottomsBelow[0];
            
            // Add the connection
            SerializedProperty connectionsProp = serializedObject.FindProperty("connections");
            bool alreadyConnected = false;
            
            for (int i = 0; i < connectionsProp.arraySize; i++)
            {
                SerializedProperty connProp = connectionsProp.GetArrayElementAtIndex(i);
                if (connProp.objectReferenceValue == closestEdgeBottom)
                {
                    alreadyConnected = true;
                    break;
                }
            }
            
            if (!alreadyConnected)
            {
                connectionsProp.arraySize++;
                connectionsProp.GetArrayElementAtIndex(connectionsProp.arraySize - 1).objectReferenceValue = closestEdgeBottom;
                serializedObject.ApplyModifiedProperties();
                
                Debug.Log($"Connected EdgeTop {waypoint.name} to EdgeBottom {closestEdgeBottom.name}");
            }
            else
            {
                Debug.Log($"EdgeTop {waypoint.name} is already connected to EdgeBottom {closestEdgeBottom.name}");
            }
            
            // Link the waypoints
            SerializedProperty linkedProp = serializedObject.FindProperty("linkedWaypoint");
            linkedProp.objectReferenceValue = closestEdgeBottom;
            serializedObject.ApplyModifiedProperties();
            
            // Link the other way too
            SerializedObject edgeBottomObj = new SerializedObject(closestEdgeBottom);
            SerializedProperty ebLinkedProp = edgeBottomObj.FindProperty("linkedWaypoint");
            ebLinkedProp.objectReferenceValue = waypoint;
            edgeBottomObj.ApplyModifiedProperties();
            
            Debug.Log($"Linked EdgeTop {waypoint.name} with EdgeBottom {closestEdgeBottom.name}");
        }
        
        private void ConnectNearbyWaypoints(Waypoint waypoint, float radius)
        {
            // Find all waypoints in radius
            Waypoint[] allWaypoints = FindObjectsOfType<Waypoint>();
            List<Waypoint> nearbyWaypoints = new List<Waypoint>();
            
            // Get this waypoint type
            SerializedProperty typeProp = serializedObject.FindProperty("type");
            WaypointType waypointType = (WaypointType)typeProp.enumValueIndex;
            
            foreach (Waypoint other in allWaypoints)
            {
                if (other != waypoint)
                {
                    float distance = Vector2.Distance(waypoint.transform.position, other.transform.position);
                    if (distance <= radius)
                    {
                        // Special handling for EdgeTop waypoints (NEW)
                        if (waypointType == WaypointType.EdgeTop)
                        {
                            SerializedObject otherObj = new SerializedObject(other);
                            SerializedProperty otherTypeProp = otherObj.FindProperty("type");
                            WaypointType otherType = (WaypointType)otherTypeProp.enumValueIndex;
                            
                            if (otherType == WaypointType.EdgeBottom && 
                                other.transform.position.y < waypoint.transform.position.y)
                            {
                                nearbyWaypoints.Add(other); // Connect to EdgeBottom below
                            }
                            else if (otherType != WaypointType.EdgeBottom && 
                                    Mathf.Abs(other.transform.position.y - waypoint.transform.position.y) < 1.0f)
                            {
                                nearbyWaypoints.Add(other); // Connect to same-level waypoints
                            }
                        }
                        // Special handling for EdgeBottom waypoints (NEW)
                        else if (waypointType == WaypointType.EdgeBottom)
                        {
                            SerializedObject otherObj = new SerializedObject(other);
                            SerializedProperty otherTypeProp = otherObj.FindProperty("type");
                            WaypointType otherType = (WaypointType)otherTypeProp.enumValueIndex;
                            
                            if (otherType != WaypointType.EdgeTop || 
                                other.transform.position.y <= waypoint.transform.position.y)
                            {
                                nearbyWaypoints.Add(other);
                            }
                        }
                        else
                        {
                            nearbyWaypoints.Add(other);
                        }
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
    
    // Get this waypoint type
    SerializedProperty typeProp = serializedObject.FindProperty("type");
    WaypointType waypointType = (WaypointType)typeProp.enumValueIndex;
    
    // Gather selected waypoints
    foreach (GameObject obj in selection)
    {
        Waypoint sel = obj.GetComponent<Waypoint>();
        if (sel != null && sel != waypoint)
            selectedWaypoints.Add(sel);
    }
    
    if (selectedWaypoints.Count == 0)
    {
        EditorUtility.DisplayDialog("Connect Error", "No other waypoints selected.", "OK");
        return;
    }
    
    // Existing connections on this waypoint
    SerializedProperty connectionsProp = serializedObject.FindProperty("connections");
    List<Waypoint> currentConnections = new List<Waypoint>();
    for (int i = 0; i < connectionsProp.arraySize; i++)
    {
        var conn = connectionsProp.GetArrayElementAtIndex(i).objectReferenceValue as Waypoint;
        if (conn != null) currentConnections.Add(conn);
    }
    
    bool changed = false;
    foreach (Waypoint selected in selectedWaypoints)
    {
        // --- from this to selected ---
        if (!currentConnections.Contains(selected))
        {
            // EdgeTop-specific check
            if (waypointType == WaypointType.EdgeTop)
            {
                var so = new SerializedObject(selected);
                var edgeTypeProp = so.FindProperty("type");
                var edgeType = (WaypointType)edgeTypeProp.enumValueIndex;
                if (edgeType == WaypointType.EdgeBottom && selected.transform.position.y >= waypoint.transform.position.y)
                {
                    Debug.LogWarning($"EdgeBottom {selected.name} is not below EdgeTop {waypoint.name}. Connection not created.");
                    continue;
                }
            }
            connectionsProp.arraySize++;
            connectionsProp.GetArrayElementAtIndex(connectionsProp.arraySize - 1).objectReferenceValue = selected;
            changed = true;
        }
        
        // --- from selected back to this ---
        var selSO = new SerializedObject(selected);
        var otherTypeProp = selSO.FindProperty("type");
        var otherType = (WaypointType)otherTypeProp.enumValueIndex;
        
        // Skip two-way if EdgeBottom -> EdgeTop
        if ((waypointType == WaypointType.EdgeTop && otherType == WaypointType.EdgeBottom) ||
            (waypointType == WaypointType.EdgeBottom && otherType == WaypointType.EdgeTop))
        {
            if (waypointType == WaypointType.EdgeBottom && otherType == WaypointType.EdgeTop)
            {
                Debug.Log($"Skipped creating connection from EdgeBottom {waypoint.name} to EdgeTop {selected.name} (one-way only)");
                continue;
            }
        }
        
        var selConns = selSO.FindProperty("connections");
        bool hasBack = false;
        for (int i = 0; i < selConns.arraySize; i++)
        {
            if (selConns.GetArrayElementAtIndex(i).objectReferenceValue as Waypoint == waypoint)
            {
                hasBack = true;
                break;
            }
        }
        if (!hasBack)
        {
            selConns.arraySize++;
            selConns.GetArrayElementAtIndex(selConns.arraySize - 1).objectReferenceValue = waypoint;
            selSO.ApplyModifiedProperties();
        }
    }
    
    if (changed)
    {
        serializedObject.ApplyModifiedProperties();
        Debug.Log($"Added connections between {waypoint.name} and {selectedWaypoints.Count} other waypoints");
    }
    else
    {
        Debug.Log("No new connections added.");
    }
}

#endif
}}