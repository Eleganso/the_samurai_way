using UnityEngine;
using System.Collections.Generic;
using System.Text;

namespace Enemies.Navigation
{
    /// <summary>
    /// Enhanced debugging tool for path decisions with detailed cost breakdowns
    /// Shows exactly why paths are chosen or rejected, with numeric comparisons
    /// </summary>
    [RequireComponent(typeof(EnemyNavigationController))]
    public class EnhancedPathDebugger : MonoBehaviour
    {
        [Header("General Settings")]
        [SerializeField] private bool enableDebugger = true;
        [SerializeField] private bool logToConsole = true;
        [SerializeField] private bool showDetailedCosts = true;
        [SerializeField] private KeyCode toggleKey = KeyCode.F3;
        
        [Header("Cost Thresholds")]
        [SerializeField] private float pathPreferenceThreshold = 0.8f; // Cost ratio to prefer alternate route
        
        [Header("Visualization")]
        [SerializeField] private bool showDirectRoute = true;
        [SerializeField] private bool showAlternateRoute = true;
        [SerializeField] private bool showCostBreakdown = true;
        [SerializeField] private bool highlightLadders = true;
        
        [Header("Colors")]
        [SerializeField] private Color directPathColor = Color.green;
        [SerializeField] private Color alternateLadderPathColor = Color.blue;
        [SerializeField] private Color alternateJumpPathColor = Color.cyan;
        [SerializeField] private Color avoidedPathColor = Color.red;
        [SerializeField] private Color ladderHighlightColor = new Color(1f, 1f, 0f, 0.3f); // Yellow semi-transparent
        
        // Component references
        private EnemyNavigationController navController;
        private WaypointSystem waypointSystem;
        private ImprovedPathPlanning pathPlanner;
        private ClimbController climbController;
        
        // Path data
        private bool isUsingAlternatePath = false;
        private bool wasUsingAlternatePath = false;
        private List<Vector2> currentAlternatePath = new List<Vector2>();
        private List<WaypointType> waypointTypes = new List<WaypointType>();
        private bool isDirectPathBlocked = false;
        private bool isDirectPathThroughAvoidedLayer = false;
        private float directPathCost = 0f;
        private float alternatePathCost = 0f;
        private string lastRouteChangeReason = "None";
        
        // Cost breakdown
        private float directPathLength = 0f;
        private float alternatePathLength = 0f;
        private int directPathObstacles = 0;
        private int alternatePathObstacles = 0;
        private float directPathHeight = 0f;
        private float alternatePathHeight = 0f;
        
        // Decision factors
        private bool containsLadder = false;
        private bool containsJump = false;
        private float ladderDistance = 999f;
        private float heightDifference = 0f;
        
        // Cache for visualization
        private Vector2 playerPosition = Vector2.zero;
        private Vector2 intermediateTargetPosition = Vector2.zero;
        private int currentPathIndex = 0;
        private bool showDebugWindow = false;
        
        private void Start()
        {
            navController = GetComponent<EnemyNavigationController>();
            waypointSystem = FindObjectOfType<WaypointSystem>();
            pathPlanner = GetComponent<ImprovedPathPlanning>();
            climbController = GetComponent<ClimbController>();
            
            if (navController == null)
            {
                Debug.LogError("EnhancedPathDebugger requires an EnemyNavigationController component!");
                enabled = false;
                return;
            }
            
            if (enableDebugger)
            {
                Debug.Log("Enhanced Path Debugger initialized");
            }
        }
        
        private void Update()
        {
            if (!enableDebugger) return;
            
            // Toggle debug window with key press
            if (Input.GetKeyDown(toggleKey))
            {
                showDebugWindow = !showDebugWindow;
            }
            
            UpdateCachedValues();
            CheckForRouteChanges();
        }
        
        private void UpdateCachedValues()
        {
            // Get target
            Transform target = GetTarget();
            if (target != null)
            {
                playerPosition = target.position;
            }
            
            // Get navigation data from path planner if available
            if (pathPlanner != null)
            {
                // Get values using reflection since properties are private
                isDirectPathBlocked = GetPrivateFieldValue<bool>(pathPlanner, "isDirectPathBlocked");
                isDirectPathThroughAvoidedLayer = GetPrivateFieldValue<bool>(pathPlanner, "isDirectPathCrossingAvoidedLayers");
                directPathCost = GetPrivateFieldValue<float>(pathPlanner, "directPathCost");
                alternatePathCost = GetPrivateFieldValue<float>(pathPlanner, "alternatePathCost");
                isUsingAlternatePath = GetPrivateFieldValue<bool>(pathPlanner, "isUsingAlternatePath");
                currentAlternatePath = GetPrivateFieldValue<List<Vector2>>(pathPlanner, "currentPath");
                currentPathIndex = GetPrivateFieldValue<int>(pathPlanner, "currentPathIndex");
                
                // Calculate detail metrics for paths
                CalculatePathMetrics();
            }
            else
            {
                // Fall back to navigation controller
                isDirectPathThroughAvoidedLayer = GetPrivateFieldValue<bool>(navController, "isDirectPathThroughAvoidedLayer");
                isUsingAlternatePath = GetPrivateFieldValue<bool>(navController, "isUsingAlternatePath");
                currentAlternatePath = GetPrivateFieldValue<List<Vector2>>(navController, "alternatePath");
                currentPathIndex = GetPrivateFieldValue<int>(navController, "currentPathIndex");
                
                // Try to get intermediate target
                Transform intermediateTarget = GetPrivateFieldValue<Transform>(navController, "intermediateTarget");
                if (intermediateTarget != null)
                {
                    intermediateTargetPosition = intermediateTarget.position;
                }
                
                // Calculate detail metrics for paths
                CalculatePathMetrics();
            }
            
            // Calculate height difference to target
            heightDifference = playerPosition.y - transform.position.y;
        }
        
        private void CalculatePathMetrics()
        {
            // Reset values
            directPathLength = 0f;
            alternatePathLength = 0f;
            directPathObstacles = 0;
            alternatePathObstacles = 0;
            directPathHeight = 0f;
            alternatePathHeight = 0f;
            containsLadder = false;
            containsJump = false;
            ladderDistance = 999f;
            waypointTypes.Clear();
            
            // Direct path metrics
            if (playerPosition != Vector2.zero)
            {
                // Direct path is just the straight line distance
                directPathLength = Vector2.Distance(transform.position, playerPosition);
                
                // Estimate obstacles by ray casting
                RaycastHit2D hit = Physics2D.Linecast(transform.position, playerPosition, 
                    GetPrivateFieldValue<LayerMask>(navController, "obstacleLayer"));
                directPathObstacles = hit.collider != null ? 1 : 0;
                
                // Height difference is absolute vertical distance
                directPathHeight = Mathf.Abs(heightDifference);
            }
            
            // Alternate path metrics
            if (currentAlternatePath != null && currentAlternatePath.Count > 1)
            {
                // Length is sum of all segments
                for (int i = 0; i < currentAlternatePath.Count - 1; i++)
                {
                    alternatePathLength += Vector2.Distance(currentAlternatePath[i], currentAlternatePath[i + 1]);
                    
                    // Calculate height changes
                    alternatePathHeight += Mathf.Abs(currentAlternatePath[i+1].y - currentAlternatePath[i].y);
                    
                    // Check for obstacles in each segment
                    RaycastHit2D hit = Physics2D.Linecast(currentAlternatePath[i], currentAlternatePath[i + 1], 
                        GetPrivateFieldValue<LayerMask>(navController, "obstacleLayer"));
                    if (hit.collider != null)
                    {
                        alternatePathObstacles++;
                    }
                }
                
                // Identify waypoint types using the WaypointSystem
                IdentifyWaypointTypes();
            }
        }
        
        private void IdentifyWaypointTypes()
        {
            if (waypointSystem == null || currentAlternatePath == null || currentAlternatePath.Count <= 1)
                return;
                
            for (int i = 0; i < currentAlternatePath.Count; i++)
            {
                // Find nearest waypoint to this path position
                Waypoint waypoint = FindNearestWaypoint(currentAlternatePath[i]);
                
                if (waypoint != null)
                {
                    waypointTypes.Add(waypoint.Type);
                    
                    // Check for ladder waypoints
                    if (waypoint.Type == WaypointType.LadderBottom || waypoint.Type == WaypointType.LadderTop)
                    {
                        containsLadder = true;
                        
                        // Calculate distance to this ladder waypoint
                        float distToLadder = Vector2.Distance(transform.position, currentAlternatePath[i]);
                        ladderDistance = Mathf.Min(ladderDistance, distToLadder);
                    }
                    
                    // Check for jump/landing points
                    if (waypoint.Type == WaypointType.JumpPoint || waypoint.Type == WaypointType.LandingPoint ||
                        waypoint.Type == WaypointType.EdgeTop || waypoint.Type == WaypointType.EdgeBottom)
                    {
                        containsJump = true;
                    }
                }
                else
                {
                    waypointTypes.Add(WaypointType.Standard);
                }
            }
        }
        
        private Waypoint FindNearestWaypoint(Vector2 position)
        {
            // Use reflection to call FindNearestWaypoint method on WaypointSystem
            var method = waypointSystem.GetType().GetMethod("FindNearestWaypoint", 
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                
            if (method != null)
            {
                return method.Invoke(waypointSystem, new object[] { position }) as Waypoint;
            }
            
            return null;
        }
        
        private void CheckForRouteChanges()
        {
            // Check if route has changed since last frame
            if (wasUsingAlternatePath != isUsingAlternatePath)
            {
                if (isUsingAlternatePath)
                {
                    // Analyze why we switched to alternate path
                    DetermineRouteChangeReason();
                    LogMessage("ROUTE CHANGE: Switched to ALTERNATE path");
                    LogMessage($"  Reason: {lastRouteChangeReason}");
                    LogMessage($"  Direct path cost: {directPathCost:F2}, Alternate path cost: {alternatePathCost:F2}");
                    LogMessage($"  Route contains ladder: {containsLadder}, Height difference: {heightDifference:F2}m");
                    LogMessage($"  Distance to ladder: {ladderDistance:F2}m");
                    if (containsLadder)
                    {
                        LogMessage($"  Cost ratio: {alternatePathCost/directPathCost:F2} (Lower is better for alternate)");
                    }
                }
                else
                {
                    LogMessage("ROUTE CHANGE: Switched to DIRECT path");
                    LogMessage($"  Direct path cost: {directPathCost:F2}, Alternate path cost: {alternatePathCost:F2}");
                    LogMessage($"  Cost ratio: {alternatePathCost/directPathCost:F2} (Needs to be below {pathPreferenceThreshold} to prefer alternate)");
                    
                    if (containsLadder)
                    {
                        LogMessage($"  No longer using ladder. Distance to ladder: {ladderDistance:F2}m");
                    }
                }
                
                wasUsingAlternatePath = isUsingAlternatePath;
            }
        }
        
        private void DetermineRouteChangeReason()
        {
            StringBuilder reason = new StringBuilder();
            
            // Check primary factors in order of importance
            if (isDirectPathThroughAvoidedLayer)
            {
                reason.Append("Direct path crosses avoided layer. ");
                
                // Try to get the specific layer name from path planner
                var avoidedLayers = GetPrivateFieldValue<LayerMask>(pathPlanner != null ? pathPlanner : navController, "avoidedLayers");
                if (avoidedLayers.value != 0)
                {
                    reason.Append($"[Avoided layers: {LayerMaskToString(avoidedLayers)}] ");
                }
            }
            
            if (isDirectPathBlocked)
            {
                reason.Append("Direct path blocked by obstacle. ");
            }
            
            // Check for significant height difference
            if (Mathf.Abs(heightDifference) > 1.5f)
            {
                reason.Append($"Significant height difference ({heightDifference:F2}m). ");
                
                // If there's a ladder in the path and a height difference, that's likely why
                if (containsLadder)
                {
                    float costRatio = alternatePathCost / directPathCost;
                    reason.Append($"Using ladder to climb {(heightDifference > 0 ? "up" : "down")}. ");
                    reason.Append($"Distance to ladder: {ladderDistance:F2}m, Cost ratio: {costRatio:F2}. ");
                }
            }
            
            // Check cost difference
            if (alternatePathCost < directPathCost * pathPreferenceThreshold)
            {
                float costRatio = alternatePathCost / directPathCost;
                reason.Append($"Alternate path more efficient (cost: {alternatePathCost:F2} vs {directPathCost:F2}, ratio: {costRatio:F2}). ");
            }
            
            lastRouteChangeReason = reason.ToString();
            if (string.IsNullOrEmpty(lastRouteChangeReason))
            {
                lastRouteChangeReason = $"Unknown reason. Cost ratio: {(alternatePathCost/directPathCost):F2}";
            }
        }
        
        private Transform GetTarget()
        {
            // Try to get target using reflection since it's private
            Transform target = GetPrivateFieldValue<Transform>(navController, "target");
            if (target != null) return target;
            
            // Fallback - look for player
            return GameObject.FindGameObjectWithTag("Player")?.transform;
        }
        
        private T GetPrivateFieldValue<T>(object obj, string fieldName)
        {
            if (obj == null) return default(T);
            
            var field = obj.GetType().GetField(fieldName, 
                System.Reflection.BindingFlags.NonPublic | 
                System.Reflection.BindingFlags.Instance);
                
            if (field != null)
            {
                try
                {
                    return (T)field.GetValue(obj);
                }
                catch
                {
                    return default(T);
                }
            }
            return default(T);
        }
        
        private string LayerMaskToString(LayerMask mask)
        {
            var names = new System.Text.StringBuilder();
            for (int i = 0; i < 32; i++)
            {
                if (((1 << i) & mask.value) != 0)
                {
                    if (names.Length > 0) names.Append(", ");
                    names.Append(LayerMask.LayerToName(i));
                }
            }
            return names.Length > 0 ? names.ToString() : "None";
        }
        
        private void LogMessage(string message)
        {
            if (logToConsole)
            {
                Debug.Log($"[PathDebug:{gameObject.name}] {message}");
            }
        }
        
        private void OnGUI()
        {
            if (!enableDebugger || !showDebugWindow) return;
            
            GUILayout.BeginArea(new Rect(10, 40, 400, 500));
            GUILayout.BeginVertical("box");
            
            GUILayout.Label($"Enhanced Path Debugger - {gameObject.name}");
            GUILayout.Label($"State: {navController.GetCurrentState()}");
            GUILayout.Label($"Current Route: {(isUsingAlternatePath ? "ALTERNATE" : "DIRECT")}");
            
            GUILayout.Label("Path Characteristics:", GUI.skin.box);
            GUILayout.Label($"Height difference to target: {heightDifference:F2}m");
            GUILayout.Label($"Using ladder: {containsLadder}");
            if (containsLadder)
            {
                GUILayout.Label($"Distance to ladder: {ladderDistance:F2}m");
            }
            
            GUILayout.Label("Cost Comparison:", GUI.skin.box);
            GUILayout.Label($"Direct path cost: {directPathCost:F2}");
            GUILayout.Label($"Alternate path cost: {alternatePathCost:F2}");
            if (directPathCost > 0)
            {
                float ratio = alternatePathCost / directPathCost;
                GUILayout.Label($"Cost ratio: {ratio:F2} (Needs < {pathPreferenceThreshold} to prefer alternate)");
                GUILayout.Label($"Decision: {(ratio < pathPreferenceThreshold ? "Use Alternate" : "Use Direct")}");
            }
            
            if (showCostBreakdown)
            {
                GUILayout.Label("Detailed Metrics:", GUI.skin.box);
                GUILayout.Label($"Direct path: Length={directPathLength:F2}m, Obstacles={directPathObstacles}, Height change={directPathHeight:F2}m");
                GUILayout.Label($"Alternate path: Length={alternatePathLength:F2}m, Obstacles={alternatePathObstacles}, Height change={alternatePathHeight:F2}m");
            }
            
            if (!string.IsNullOrEmpty(lastRouteChangeReason))
            {
                GUILayout.Label("Last Route Change Reason:", GUI.skin.box);
                GUILayout.Label(lastRouteChangeReason, new GUIStyle() { wordWrap = true });
            }
            
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }
        
        private void OnDrawGizmos()
        {
            if (!enableDebugger || !Application.isPlaying) return;
            
            // Draw current state text
            if (navController != null)
            {
                string navInfo = $"{navController.GetCurrentState()} - {(isUsingAlternatePath ? "ALTERNATE" : "DIRECT")}";
                if (containsLadder)
                {
                    navInfo += $"\nUsing Ladder (dist: {ladderDistance:F1}m)";
                }
                
                DrawLabel(transform.position + new Vector3(0, 1.5f, 0), navInfo);
            }
            
            // Draw direct path to target
            if (showDirectRoute && playerPosition != Vector2.zero)
            {
                // Choose color based on path status
                Color pathColor = directPathColor;
                if (isDirectPathThroughAvoidedLayer)
                    pathColor = avoidedPathColor;
                    
                Gizmos.color = pathColor;
                Gizmos.DrawLine(transform.position, playerPosition);
                
                // Draw cost if enabled
                if (showDetailedCosts)
                {
                    Vector3 midPoint = (transform.position + (Vector3)playerPosition) * 0.5f;
                    DrawLabel(midPoint, $"Cost: {directPathCost:F2}\nLength: {directPathLength:F2}m");
                }
                
                // Show reason if path is avoided
                if (isDirectPathThroughAvoidedLayer)
                {
                    Vector3 midPoint = (transform.position + (Vector3)playerPosition) * 0.5f;
                    DrawLabel(midPoint + new Vector3(0, 0.3f, 0), "Avoided Layer");
                }
            }
            
            // Draw alternate path with special coloring for ladder segments
            if (showAlternateRoute && isUsingAlternatePath && currentAlternatePath != null && currentAlternatePath.Count > 1)
            {
                // Draw lines between waypoints with special coloring for ladder sections
                for (int i = 0; i < currentAlternatePath.Count - 1; i++)
                {
                    // Choose color based on waypoint type
                    Color segmentColor = alternateJumpPathColor;
                    
                    // If we have waypoint types, use them to color the path
                    if (i < waypointTypes.Count)
                    {
                        if (waypointTypes[i] == WaypointType.LadderBottom || waypointTypes[i] == WaypointType.LadderTop)
                        {
                            segmentColor = alternateLadderPathColor;
                        }
                    }
                    
                    Gizmos.color = segmentColor;
                    Gizmos.DrawLine(currentAlternatePath[i], currentAlternatePath[i + 1]);
                    Gizmos.DrawWireSphere(currentAlternatePath[i], 0.2f);
                    
                    // Label waypoint type if we have it
                    if (i < waypointTypes.Count && waypointTypes[i] != WaypointType.Standard)
                    {
                        Vector3 labelPos = new Vector3(
                            currentAlternatePath[i].x,
                            currentAlternatePath[i].y + 0.3f,
                            0f);
                        DrawLabel(labelPos, waypointTypes[i].ToString());
                    }
                }
                
                // Draw final waypoint
                Gizmos.DrawWireSphere(currentAlternatePath[currentAlternatePath.Count - 1], 0.2f);
                
                // Draw current waypoint index
                if (currentPathIndex >= 0 && currentPathIndex < currentAlternatePath.Count)
                {
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawWireSphere(currentAlternatePath[currentPathIndex], 0.3f);
                    
                    // Convert Vector2 to Vector3 explicitly to avoid ambiguous operator error
                    Vector3 labelPos = new Vector3(
                        currentAlternatePath[currentPathIndex].x, 
                        currentAlternatePath[currentPathIndex].y + 0.4f, 
                        0f);
                    DrawLabel(labelPos, $"Waypoint #{currentPathIndex}");
                }
                
                // Draw intermediate target
                if (intermediateTargetPosition != Vector2.zero)
                {
                    Gizmos.color = Color.magenta;
                    Gizmos.DrawWireSphere(intermediateTargetPosition, 0.4f);
                    
                    // Convert Vector2 to Vector3 explicitly to avoid ambiguous operator error
                    Vector3 labelPos = new Vector3(
                        intermediateTargetPosition.x, 
                        intermediateTargetPosition.y + 0.5f, 
                        0f);
                    DrawLabel(labelPos, "Target");
                }
                
                // Draw cost if enabled
                if (showDetailedCosts)
                {
                    // Initialize midPos as Vector3 to avoid ambiguous operator issues
                    Vector3 midPos = Vector3.zero;
                    if (currentAlternatePath.Count > 2)
                    {
                        int midIndex = currentAlternatePath.Count / 2;
                        midPos = new Vector3(
                            currentAlternatePath[midIndex].x,
                            currentAlternatePath[midIndex].y,
                            0f);
                    }
                    else if (currentAlternatePath.Count > 0)
                    {
                        midPos = new Vector3(
                            currentAlternatePath[0].x,
                            currentAlternatePath[0].y,
                            0f);
                    }
                    
                    if (midPos != Vector3.zero)
                    {
                        string costInfo = $"Cost: {alternatePathCost:F2}";
                        if (directPathCost > 0)
                        {
                            costInfo += $"\nRatio: {alternatePathCost/directPathCost:F2}";
                        }
                        costInfo += $"\nLength: {alternatePathLength:F2}m";
                        DrawLabel(midPos + new Vector3(0, 0.4f, 0), costInfo);
                    }
                }
                
                // Highlight ladder in the scene
                if (highlightLadders && containsLadder)
                {
                    // Find ladder waypoints
                    for (int i = 0; i < currentAlternatePath.Count; i++)
                    {
                        if (i < waypointTypes.Count && 
                            (waypointTypes[i] == WaypointType.LadderBottom || waypointTypes[i] == WaypointType.LadderTop))
                        {
                            // Draw highlight around ladder
                            Gizmos.color = ladderHighlightColor;
                            
                            // Find the ladder collider if possible
                            Collider2D[] colliders = Physics2D.OverlapCircleAll(
                                currentAlternatePath[i], 
                                1.0f, 
                                GetPrivateFieldValue<LayerMask>(navController, "ladderLayer"));
                                
                            foreach (var collider in colliders)
                            {
                                // Highlight the ladder collider
                                if (collider is BoxCollider2D box)
                                {
                                    Gizmos.DrawCube(box.bounds.center, box.bounds.size);
                                }
                                else
                                {
                                    Gizmos.DrawSphere(collider.bounds.center, 0.5f);
                                }
                                
                                // Label the ladder
                                DrawLabel(collider.bounds.center, $"LADDER\nDistance: {ladderDistance:F2}m");
                            }
                        }
                    }
                }
            }
            
            // Draw avoided layers visualization if directly crossing them
            if (isDirectPathThroughAvoidedLayer)
            {
                LayerMask avoidedLayers = GetPrivateFieldValue<LayerMask>(
                    pathPlanner != null ? pathPlanner : navController, 
                    "avoidedLayers");
                
                if (avoidedLayers.value != 0)
                {
                    // Try to determine which objects are causing the avoidance
                    Vector2 dir = (playerPosition - (Vector2)transform.position).normalized;
                    float dist = Vector2.Distance(transform.position, playerPosition);
                    
                    RaycastHit2D hit = Physics2D.Raycast(transform.position, dir, dist, avoidedLayers);
                    if (hit.collider != null)
                    {
                        // Highlight the specific object causing avoidance
                        Gizmos.color = new Color(1f, 0f, 1f, 0.3f); // Purple semi-transparent
                        
                        if (hit.collider is BoxCollider2D box)
                        {
                            Gizmos.DrawCube(box.bounds.center, box.bounds.size);
                        }
                        else
                        {
                            Gizmos.DrawSphere(hit.collider.bounds.center, 0.5f);
                        }
                        
                        DrawLabel(hit.collider.bounds.center, 
                            $"Avoided: {LayerMask.LayerToName(hit.collider.gameObject.layer)}");
                    }
                }
            }
        }
        
        private void DrawLabel(Vector3 position, string text)
        {
            #if UNITY_EDITOR
            UnityEditor.Handles.Label(position, text);
            #endif
        }
    }
}