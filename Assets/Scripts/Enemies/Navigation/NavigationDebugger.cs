using UnityEngine;
using System.Collections.Generic;

namespace Enemies.Navigation
{
    /// <summary>
    /// Advanced debugging tool for visualizing and logging enemy navigation decisions
    /// Attach to enemies alongside EnemyNavigationController to see detailed route info
    /// </summary>
    [RequireComponent(typeof(EnemyNavigationController))]
    public class NavigationDebugger : MonoBehaviour
    {
        [Header("General Settings")]
        [SerializeField] private bool enableDebugger = true;
        [SerializeField] private bool logToConsole = true;
        
        [Header("Visualization")]
        [SerializeField] private bool showDirectRoute = true;
        [SerializeField] private bool showAlternateRoute = true;
        [SerializeField] private bool showAvoidedLayers = true;
        [SerializeField] private bool showPathCosts = true;
        
        [Header("Colors")]
        [SerializeField] private Color directPathColor = Color.green;
        [SerializeField] private Color alternatePathColor = Color.cyan;
        [SerializeField] private Color avoidedPathColor = Color.red;
        [SerializeField] private Color blockedPathColor = Color.yellow;
        
        // Component references
        private EnemyNavigationController navController;
        private ImprovedPathPlanning pathPlanner;
        private ObstacleDetection obstacleDetector;
        
        // Route tracking
        private bool isUsingAlternatePath = false;
        private bool wasUsingAlternatePath = false;
        private List<Vector2> currentAlternatePath = new List<Vector2>();
        private bool isDirectPathBlocked = false;
        private bool isDirectPathThroughAvoidedLayer = false;
        private float directPathCost = 0f;
        private float alternatePathCost = 0f;
        private string lastRouteChangeReason = "None";
        
        // Cache for visualization
        private Vector2 playerPosition = Vector2.zero;
        private Vector2 intermediateTargetPosition = Vector2.zero;
        private int currentPathIndex = 0;
        
        private void Start()
        {
            navController = GetComponent<EnemyNavigationController>();
            pathPlanner = GetComponent<ImprovedPathPlanning>();
            obstacleDetector = GetComponent<ObstacleDetection>();
            
            if (navController == null)
            {
                Debug.LogError("NavigationDebugger requires an EnemyNavigationController component!");
                enabled = false;
                return;
            }
            
            if (enableDebugger)
            {
                Debug.Log("Navigation Debugger initialized");
            }
        }
        
        private void Update()
        {
            if (!enableDebugger) return;
            
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
                isUsingAlternatePath = ShouldUseAlternatePath(pathPlanner);
                currentAlternatePath = GetPrivateFieldValue<List<Vector2>>(pathPlanner, "currentPath");
                currentPathIndex = GetPrivateFieldValue<int>(pathPlanner, "currentPathIndex");
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
            }
        }
        
        private bool ShouldUseAlternatePath(ImprovedPathPlanning planner)
        {
            // Try to call the ShouldUseAlternatePath method via reflection
            var method = planner.GetType().GetMethod("ShouldUseAlternatePath", 
                System.Reflection.BindingFlags.NonPublic | 
                System.Reflection.BindingFlags.Instance);
                
            if (method != null)
            {
                return (bool)method.Invoke(planner, null);
            }
            return false;
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
                    LogMessage($"ROUTE CHANGE: Switched to ALTERNATE path - {lastRouteChangeReason}");
                    LogMessage($"Direct path cost: {directPathCost:F2}, Alternate path cost: {alternatePathCost:F2}");
                }
                else
                {
                    LogMessage("ROUTE CHANGE: Switched to DIRECT path");
                }
                
                wasUsingAlternatePath = isUsingAlternatePath;
            }
        }
        
        private void DetermineRouteChangeReason()
        {
            System.Text.StringBuilder reason = new System.Text.StringBuilder();
            
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
            
            if (alternatePathCost < directPathCost && directPathCost > 0)
            {
                float costRatio = alternatePathCost / directPathCost;
                reason.Append($"Alternate path more efficient (cost ratio: {costRatio:F2}). ");
            }
            
            // Check vertical difference
            float heightDifference = playerPosition.y - transform.position.y;
            if (Mathf.Abs(heightDifference) > 1.5f)
            {
                reason.Append($"Significant height difference ({heightDifference:F2}m). ");
            }
            
            lastRouteChangeReason = reason.ToString();
            if (string.IsNullOrEmpty(lastRouteChangeReason))
            {
                lastRouteChangeReason = "Unknown reason";
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
                Debug.Log($"[NavDebug:{gameObject.name}] {message}");
            }
        }
        
        private void OnDrawGizmos()
        {
            if (!enableDebugger || !Application.isPlaying) return;
            
            // Draw current state text
            if (navController != null)
            {
                DrawLabel(transform.position + new Vector3(0, 1.5f, 0), 
                    $"{navController.GetCurrentState()} - {(isUsingAlternatePath ? "ALTERNATE" : "DIRECT")}");
            }
            
            // Draw direct path to target
            if (showDirectRoute && playerPosition != Vector2.zero)
            {
                // Choose color based on path status
                Color pathColor = directPathColor;
                if (isDirectPathThroughAvoidedLayer)
                    pathColor = avoidedPathColor;
                else if (isDirectPathBlocked)
                    pathColor = blockedPathColor;
                    
                Gizmos.color = pathColor;
                Gizmos.DrawLine(transform.position, playerPosition);
                
                // Draw cost if enabled
                if (showPathCosts)
                {
                    Vector3 midPoint = (transform.position + (Vector3)playerPosition) * 0.5f;
                    DrawLabel(midPoint, $"Cost: {directPathCost:F2}");
                }
                
                // Show reason if path is avoided
                if (isDirectPathThroughAvoidedLayer)
                {
                    Vector3 midPoint = (transform.position + (Vector3)playerPosition) * 0.5f;
                    DrawLabel(midPoint + new Vector3(0, 0.3f, 0), "Avoided Layer");
                }
            }
            
            // Draw alternate path
            if (showAlternateRoute && isUsingAlternatePath && currentAlternatePath != null && currentAlternatePath.Count > 1)
            {
                Gizmos.color = alternatePathColor;
                
                // Draw lines between waypoints
                for (int i = 0; i < currentAlternatePath.Count - 1; i++)
                {
                    Gizmos.DrawLine(currentAlternatePath[i], currentAlternatePath[i + 1]);
                    Gizmos.DrawWireSphere(currentAlternatePath[i], 0.2f);
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
                if (showPathCosts && alternatePathCost > 0)
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
                        DrawLabel(midPos + new Vector3(0, 0.4f, 0), $"Cost: {alternatePathCost:F2}");
                    }
                }
            }
            
            // Draw avoided layers visualization
            if (showAvoidedLayers)
            {
                LayerMask avoidedLayers = GetPrivateFieldValue<LayerMask>(
                    pathPlanner != null ? pathPlanner : navController, 
                    "avoidedLayers");
                
                if (avoidedLayers.value != 0)
                {
                    // Only draw avoided layers if direct path crosses them
                    if (isDirectPathThroughAvoidedLayer)
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
        }
        
        private void DrawLabel(Vector3 position, string text)
        {
            #if UNITY_EDITOR
            UnityEditor.Handles.Label(position, text);
            #endif
        }
    }
}