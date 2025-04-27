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
        LandingPoint,   // Point where enemies can safely land
        EdgeTop,        // Top of a platform edge where enemies can jump down
        EdgeBottom      // Landing point at the bottom of a drop
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
        [SerializeField] private Waypoint linkedWaypoint; // For ladder top/bottom pairs or edge top/bottom pairs
        [SerializeField] private float costModifier = 1f; // Higher means less desirable path
        [SerializeField] private bool allowDownwardOnly = false; // For EdgeTop waypoints, only allow downward movement

        // Public properties
        public WaypointType Type => type;
        public List<Waypoint> Connections => connections;
        public Waypoint LinkedWaypoint => linkedWaypoint;
        public float CostModifier => costModifier;
        public bool AllowDownwardOnly => allowDownwardOnly;
        
        // Auto-connections in editor
        private void OnValidate()
        {
            // Auto-set allowDownwardOnly for EdgeTop waypoints
            if (type == WaypointType.EdgeTop && !allowDownwardOnly)
                allowDownwardOnly = true;

            // Set default color based on type
            switch (type)
            {
                case WaypointType.Standard:
                    gizmoColor = Color.cyan;
                    break;
                case WaypointType.LadderBottom:
                    gizmoColor = Color.blue;
                    break;
                case WaypointType.LadderTop:
                    gizmoColor = Color.red;
                    break;
                case WaypointType.JumpPoint:
                    gizmoColor = Color.green;
                    break;
                case WaypointType.LandingPoint:
                    gizmoColor = Color.yellow;
                    break;
                case WaypointType.EdgeTop:
                    gizmoColor = new Color(1f, 0.5f, 0f); // Orange
                    break;
                case WaypointType.EdgeBottom:
                    gizmoColor = new Color(0.5f, 0f, 1f); // Purple
                    break;
            }

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
                        // Special case for EdgeTop - only connect to EdgeBottom or same-level waypoints
                        if (type == WaypointType.EdgeTop)
                        {
                            // Allow connections to EdgeBottom below or same-level waypoints
                            if (waypoint.Type == WaypointType.EdgeBottom && 
                                waypoint.transform.position.y < transform.position.y)
                            {
                                connections.Add(waypoint);
                            }
                            else if (waypoint.Type != WaypointType.EdgeBottom && 
                                    Mathf.Abs(waypoint.transform.position.y - transform.position.y) < 0.5f)
                            {
                                connections.Add(waypoint);
                            }
                        }
                        // Special case for EdgeBottom - don't connect to EdgeTop above
                        else if (type == WaypointType.EdgeBottom)
                        {
                            if (waypoint.Type != WaypointType.EdgeTop || 
                                waypoint.transform.position.y <= transform.position.y)
                            {
                                connections.Add(waypoint);
                            }
                        }
                        else
                        {
                            // Standard connection for other waypoint types
                            connections.Add(waypoint);
                        }
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
                    // Different line styles for different connection types
                    if (type == WaypointType.EdgeTop && connection.Type == WaypointType.EdgeBottom)
                    {
                        // Draw dashed line for EdgeTop to EdgeBottom (one-way connection)
                        DrawDottedLine(transform.position, connection.transform.position, 0.2f);
                    }
                    else
                    {
                        Gizmos.DrawLine(transform.position, connection.transform.position);
                    }
                }
            }
            
            // Draw special link (e.g., ladder connection or edge connection)
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
        
        // Helper method to draw dotted lines in the editor
        private void DrawDottedLine(Vector3 start, Vector3 end, float segmentSize)
        {
            Vector3 direction = (end - start).normalized;
            float distance = Vector3.Distance(start, end);
            int segments = Mathf.FloorToInt(distance / segmentSize);
            
            for (int i = 0; i < segments; i += 2)
            {
                float startDistance = i * segmentSize;
                float endDistance = Mathf.Min((i + 1) * segmentSize, distance);
                
                Gizmos.DrawLine(
                    start + direction * startDistance,
                    start + direction * endDistance
                );
            }
        }
    }

    // Main waypoint manager class
    [DefaultExecutionOrder(-100)] // Make sure this initializes early
    public class WaypointSystem : MonoBehaviour
    {
        private static WaypointSystem instance;
        
        [SerializeField] private bool drawGizmos = true;
        [SerializeField] private bool logPathfinding = false;
        
        // Special movement settings
        [Header("Vertical Movement Costs")]
        [SerializeField] private float climbUpCost = 1.5f; // Cost multiplier for climbing up
        [SerializeField] private float jumpDownCost = 0.8f; // Cost multiplier for jumping down (lower = preferred)
        
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
                    var path = ReconstructPath(cameFrom, current);
                    if (logPathfinding)
                        LogPath(path);
                    return path;
                }
                
                openSet.Remove(current);
                closedSet.Add(current);
                
                foreach (Waypoint neighbor in current.Connections)
                {
                    if (neighbor == null || closedSet.Contains(neighbor))
                        continue;
                        
                    // Check for special cases of vertical movement
                    if (current.Type == WaypointType.EdgeTop && neighbor.Type == WaypointType.EdgeBottom)
                    {
                        // Edge top to edge bottom - jumping down
                        if (current.transform.position.y <= neighbor.transform.position.y)
                        {
                            // Invalid downward connection - EdgeBottom should be below EdgeTop
                            continue;
                        }
                        
                        // Calculate cost with jump down modifier
                        float moveCost = Vector2.Distance(current.transform.position, neighbor.transform.position) * 
                                       jumpDownCost * neighbor.CostModifier;
                        float tentativeGScore = gScore[current] + moveCost;
                        
                        ProcessNeighbor(current, neighbor, tentativeGScore, gScore, fScore, cameFrom, openSet, goal);
                    }
                    else if (current.Type == WaypointType.EdgeBottom && neighbor.Type == WaypointType.EdgeTop)
                    {
                        // Cannot go from edge bottom to edge top (one-way connection)
                        continue;
                    }
                    else if ((current.Type == WaypointType.LadderBottom && neighbor.Type == WaypointType.LadderTop) ||
                            (neighbor.transform.position.y > current.transform.position.y + 1.0f)) // Any significant climb up
                    {
                        // Climbing up - higher cost
                        float moveCost = Vector2.Distance(current.transform.position, neighbor.transform.position) * 
                                       climbUpCost * neighbor.CostModifier;
                        float tentativeGScore = gScore[current] + moveCost;
                        
                        ProcessNeighbor(current, neighbor, tentativeGScore, gScore, fScore, cameFrom, openSet, goal);
                    }
                    else
                    {
                        // Standard movement
                        float moveCost = Vector2.Distance(current.transform.position, neighbor.transform.position) * 
                                       neighbor.CostModifier;
                        float tentativeGScore = gScore[current] + moveCost;
                        
                        ProcessNeighbor(current, neighbor, tentativeGScore, gScore, fScore, cameFrom, openSet, goal);
                    }
                }
            }
            
            // No path found
            if (logPathfinding)
                Debug.LogWarning("No path found between waypoints");
            return new List<Waypoint>();
        }
        
        // Helper method to process a neighbor in A* pathfinding
        private void ProcessNeighbor(
            Waypoint current, 
            Waypoint neighbor, 
            float tentativeGScore, 
            Dictionary<Waypoint, float> gScore, 
            Dictionary<Waypoint, float> fScore,
            Dictionary<Waypoint, Waypoint> cameFrom,
            List<Waypoint> openSet,
            Waypoint goal)
        {
            if (!openSet.Contains(neighbor))
            {
                openSet.Add(neighbor);
            }
            else if (tentativeGScore >= gScore[neighbor])
            {
                return; // Not a better path
            }
            
            // This is a better path
            cameFrom[neighbor] = current;
            gScore[neighbor] = tentativeGScore;
            fScore[neighbor] = gScore[neighbor] + 
                Vector2.Distance(neighbor.transform.position, goal.transform.position);
        }
        
        // Helper to log a path for debugging
        private void LogPath(List<Waypoint> path)
        {
            string pathDesc = "Path: ";
            foreach (var waypoint in path)
            {
                pathDesc += $"{waypoint.Type}({waypoint.name}) -> ";
            }
            Debug.Log(pathDesc.TrimEnd('-', ' ', '>'));
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
        
        // Find path with special consideration for vertical movement
        public List<Vector2> FindPathWithVerticalMovement(Vector2 start, Vector2 goal, bool canClimbUp, bool canJumpDown, float waypointSearchRadius = 10f)
        {
            // Find nearest appropriate waypoints based on capabilities
            Waypoint startWaypoint = FindAppropriateWaypoint(start, waypointSearchRadius);
            Waypoint goalWaypoint = FindAppropriateWaypoint(goal, waypointSearchRadius);
            
            if (startWaypoint == null || goalWaypoint == null)
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
        
        // Find appropriate waypoint based on position and vertical capabilities
        private Waypoint FindAppropriateWaypoint(Vector2 position, float searchRadius)
        {
            // First try to find exact matches by type
            List<Waypoint> nearbyWaypoints = FindWaypointsInRadius(position, searchRadius);
            
            if (nearbyWaypoints.Count == 0)
                return null;
                
            // Find the closest waypoint that is appropriate
            float closestDistance = float.MaxValue;
            Waypoint bestWaypoint = null;
            
            foreach (Waypoint waypoint in nearbyWaypoints)
            {
                float distance = Vector2.Distance(position, waypoint.transform.position);
                
                // Prioritize waypoints at similar height
                if (Mathf.Abs(waypoint.transform.position.y - position.y) < 1.0f)
                {
                    distance *= 0.8f; // Give preference to waypoints at similar height
                }
                
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    bestWaypoint = waypoint;
                }
            }
            
            return bestWaypoint;
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