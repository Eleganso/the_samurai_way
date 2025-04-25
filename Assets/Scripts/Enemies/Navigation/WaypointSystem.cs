using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace Enemies.Navigation
{
    // Types of waypoints for different navigation purposes
    public enum WaypointType
    {
        Standard,       // General navigation point
        LadderBottom,   // Bottom of a ladder
        LadderTop,      // Top of a ladder
        JumpPoint,      // Point where enemies can jump from
        LandingPoint    // Point where enemies can safely land
    }

    // Waypoint class that can be placed in the level
    public class Waypoint : MonoBehaviour
    {
        [Header("Waypoint Settings")]
        [SerializeField] private WaypointType type = WaypointType.Standard;
        [SerializeField] private List<Waypoint> connections = new List<Waypoint>();
        [SerializeField] private float connectionRadius = 5f;
        [SerializeField] private bool autoConnectInRadius = true;
        [SerializeField] private Color gizmoColor = Color.cyan;
        
        [Header("Special Properties")]
        [SerializeField] private Waypoint linkedWaypoint; // For ladder top/bottom pairs
        [SerializeField] private float costModifier = 1f; // Higher means less desirable path

        // Public properties
        public WaypointType Type => type;
        public List<Waypoint> Connections => connections;
        public Waypoint LinkedWaypoint => linkedWaypoint;
        public float CostModifier => costModifier;
        
        // Auto-connections in editor
        private void OnValidate()
        {
            if (autoConnectInRadius && Application.isEditor && !Application.isPlaying)
            {
                AutoConnectNearbyWaypoints();
            }
        }
        
        // Connect to nearby waypoints based on radius
        private void AutoConnectNearbyWaypoints()
        {
            Waypoint[] allWaypoints = FindObjectsOfType<Waypoint>();
            connections.Clear();
            
            foreach (Waypoint waypoint in allWaypoints)
            {
                if (waypoint != this)
                {
                    float distance = Vector2.Distance(transform.position, waypoint.transform.position);
                    if (distance <= connectionRadius)
                    {
                        connections.Add(waypoint);
                    }
                }
            }
        }
        
        // Draw the waypoint and its connections in the editor
        private void OnDrawGizmos()
        {
            // Draw the waypoint
            Gizmos.color = gizmoColor;
            Gizmos.DrawSphere(transform.position, 0.3f);
            
            // Draw connection radius if auto-connect is enabled
            if (autoConnectInRadius)
            {
                Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.1f);
                Gizmos.DrawWireSphere(transform.position, connectionRadius);
            }
            
            // Draw connections to other waypoints
            Gizmos.color = gizmoColor;
            foreach (Waypoint connection in connections)
            {
                if (connection != null)
                {
                    Gizmos.DrawLine(transform.position, connection.transform.position);
                }
            }
            
            // Draw special link (e.g., ladder connection)
            if (linkedWaypoint != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(transform.position, linkedWaypoint.transform.position);
            }
            
            // Draw waypoint type label
            #if UNITY_EDITOR
            UnityEditor.Handles.Label(transform.position + Vector3.up * 0.5f, type.ToString());
            #endif
        }
    }

    // Main waypoint manager class
    [DefaultExecutionOrder(-100)] // Make sure this initializes early
    public class WaypointSystem : MonoBehaviour
    {
        private static WaypointSystem instance;
        
        [SerializeField] private bool drawGizmos = true;
        
        private Waypoint[] allWaypoints;
        private Dictionary<WaypointType, List<Waypoint>> waypointsByType;
        
        // Singleton access
        public static WaypointSystem Instance => instance;

        private void Awake()
        {
            // Singleton pattern
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            instance = this;
            
            // Initialize waypoints
            RefreshWaypoints();
        }
        
        // Refresh the waypoint collections
        public void RefreshWaypoints()
        {
            allWaypoints = FindObjectsOfType<Waypoint>();
            
            // Group waypoints by type
            waypointsByType = new Dictionary<WaypointType, List<Waypoint>>();
            foreach (WaypointType type in System.Enum.GetValues(typeof(WaypointType)))
            {
                waypointsByType[type] = new List<Waypoint>();
            }
            
            foreach (Waypoint waypoint in allWaypoints)
            {
                waypointsByType[waypoint.Type].Add(waypoint);
            }
            
            Debug.Log($"WaypointSystem initialized with {allWaypoints.Length} waypoints");
        }
        
        // Find the nearest waypoint of any type
        public Waypoint FindNearestWaypoint(Vector2 position)
        {
            return FindNearestWaypoint(position, null);
        }
        
        // Find the nearest waypoint of a specific type
        public Waypoint FindNearestWaypoint(Vector2 position, WaypointType? type)
        {
            IEnumerable<Waypoint> waypointsToSearch = type.HasValue ? 
                waypointsByType[type.Value] : allWaypoints;
                
            Waypoint nearest = null;
            float minDistance = float.MaxValue;
            
            foreach (Waypoint waypoint in waypointsToSearch)
            {
                float distance = Vector2.Distance(position, waypoint.transform.position);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearest = waypoint;
                }
            }
            
            return nearest;
        }
        
        // Find all waypoints of a specific type within a radius
        public List<Waypoint> FindWaypointsInRadius(Vector2 position, float radius, WaypointType? type = null)
        {
            IEnumerable<Waypoint> waypointsToSearch = type.HasValue ? 
                waypointsByType[type.Value] : allWaypoints;
                
            return waypointsToSearch
                .Where(w => Vector2.Distance(position, w.transform.position) <= radius)
                .ToList();
        }
        
        // Find path between two waypoints using A* algorithm
        public List<Waypoint> FindPath(Waypoint start, Waypoint goal)
        {
            if (start == null || goal == null)
                return new List<Waypoint>();
                
            // A* pathfinding
            var openSet = new List<Waypoint> { start };
            var closedSet = new HashSet<Waypoint>();
            
            // Track costs and previous nodes
            var gScore = new Dictionary<Waypoint, float>();
            var fScore = new Dictionary<Waypoint, float>();
            var cameFrom = new Dictionary<Waypoint, Waypoint>();
            
            foreach (Waypoint waypoint in allWaypoints)
            {
                gScore[waypoint] = float.MaxValue;
                fScore[waypoint] = float.MaxValue;
            }
            
            gScore[start] = 0;
            fScore[start] = Vector2.Distance(start.transform.position, goal.transform.position);
            
            while (openSet.Count > 0)
            {
                // Find node with lowest fScore
                Waypoint current = openSet.OrderBy(w => fScore[w]).First();
                
                // Goal reached
                if (current == goal)
                {
                    return ReconstructPath(cameFrom, current);
                }
                
                openSet.Remove(current);
                closedSet.Add(current);
                
                foreach (Waypoint neighbor in current.Connections)
                {
                    if (neighbor == null || closedSet.Contains(neighbor))
                        continue;
                        
                    float tentativeGScore = gScore[current] + 
                        Vector2.Distance(current.transform.position, neighbor.transform.position) * 
                        neighbor.CostModifier;
                        
                    if (!openSet.Contains(neighbor))
                    {
                        openSet.Add(neighbor);
                    }
                    else if (tentativeGScore >= gScore[neighbor])
                    {
                        continue;
                    }
                    
                    // This is a better path
                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentativeGScore;
                    fScore[neighbor] = gScore[neighbor] + 
                        Vector2.Distance(neighbor.transform.position, goal.transform.position);
                }
            }
            
            // No path found
            return new List<Waypoint>();
        }
        
        // Find path from position to position using nearest waypoints
        public List<Vector2> FindPathFromPositions(Vector2 start, Vector2 goal, float waypointSearchRadius = 10f)
        {
            // Find nearest waypoints
            Waypoint startWaypoint = FindNearestWaypoint(start);
            Waypoint goalWaypoint = FindNearestWaypoint(goal);
            
            if (startWaypoint == null || goalWaypoint == null || 
                Vector2.Distance(start, startWaypoint.transform.position) > waypointSearchRadius ||
                Vector2.Distance(goal, goalWaypoint.transform.position) > waypointSearchRadius)
            {
                return new List<Vector2>();
            }
            
            // Find waypoint path
            List<Waypoint> waypointPath = FindPath(startWaypoint, goalWaypoint);
            
            // Convert to positions
            List<Vector2> path = new List<Vector2> { start };
            foreach (Waypoint waypoint in waypointPath)
            {
                path.Add((Vector2)waypoint.transform.position);
            }
            path.Add(goal);
            
            return path;
        }
        
        // Reconstruct path from A* result
        private List<Waypoint> ReconstructPath(Dictionary<Waypoint, Waypoint> cameFrom, Waypoint current)
        {
            var path = new List<Waypoint> { current };
            
            while (cameFrom.ContainsKey(current))
            {
                current = cameFrom[current];
                path.Insert(0, current);
            }
            
            return path;
        }
        
        // Draw gizmos in the editor
        private void OnDrawGizmos()
        {
            if (!drawGizmos || !Application.isPlaying)
                return;
                
            // Draw waypoint gizmos handled by Waypoint class
        }
    }
}