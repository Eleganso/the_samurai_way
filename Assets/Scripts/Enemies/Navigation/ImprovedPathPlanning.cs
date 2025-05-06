using UnityEngine;
using System.Collections.Generic;

namespace Enemies.Navigation
{
    public class ImprovedPathPlanning : MonoBehaviour
    {
        [Header("Obstacle Detection")]
        [SerializeField] private LayerMask obstacleLayerMask; // Ground and wall layers
        [SerializeField] private float obstacleCheckResolution = 0.25f; // Distance between path checks
        
        [Header("Path Avoidance")]
        [SerializeField] private LayerMask avoidedLayers; // Specific layers to ALWAYS avoid
        [SerializeField] private bool prioritizeAlternatePaths = true; // Always prefer alternate paths when direct crosses avoided layers
        [SerializeField] private float pathRecalculationInterval = 0.5f; // How often to recalculate paths (seconds)
        
        [Header("Path Finding Settings")]
        [SerializeField] private float maxPathfindingDistance = 40f; // Maximum distance to search for paths
        [SerializeField] private float waypointSearchRadius = 40f; // How far to look for waypoints
        [SerializeField] private bool alwaysUseAlternateWhenBlocked = true; // Force alternate paths when blocked
        
        [Header("Debug Options")]
        [SerializeField] private bool visualizePaths = true;
        [SerializeField] private bool debugLogDecisions = false; // Log detailed decision info
        [SerializeField] private bool useOffsetDebugLabels = true; // Prevent overlapping labels
        [SerializeField] private Color directPathColor = Color.white;
        [SerializeField] private Color avoidedPathColor = Color.red;
        [SerializeField] private Color alternatePathColor = Color.green;
        [SerializeField] private Color blockedPathColor = Color.magenta;
        
        // References
        private WaypointSystem waypointSystem;
        private EnemyNavigationController navigationController;
        
        // Path state
        private bool isDirectPathBlocked = false;
        private bool isDirectPathCrossingAvoidedLayers = false;
        private List<Vector2> currentPath = new List<Vector2>();
        private int currentPathIndex = 0;
        private string currentDecisionReason = ""; // Added to store reason for decision
        
        // Path costs
        private float directPathCost = 0f;
        private float alternatePathCost = float.MaxValue;
        
        // Path evaluation timing
        private float lastPathEvaluationTime = 0f;
        
        // Cache for debugging
        private Vector2 lastStartPos;
        private Vector2 lastTargetPos;
        private bool lastPathResult;
        
        private void Start()
        {
            navigationController = GetComponent<EnemyNavigationController>();
            waypointSystem = FindObjectOfType<WaypointSystem>();
            
            if (waypointSystem == null)
            {
                Debug.LogWarning("WaypointSystem not found! Path planning will be limited.");
            }
            
            // Force immediate path calculation
            lastPathEvaluationTime = -pathRecalculationInterval;
        }
        
        private void Update()
        {
            if (navigationController == null) return;
            
            // Get target from navigation controller
            Transform target = GetTargetFromNavigationController();
            if (target == null) return;
            
            // Check if it's time to recalculate path
            if (Time.time - lastPathEvaluationTime >= pathRecalculationInterval)
            {
                lastPathEvaluationTime = Time.time;
                
                // Check if direct path is blocked or crosses avoided layers
                Vector2 startPos = transform.position;
                Vector2 targetPos = target.position;
                
                // Calculate distance to target
                float distanceToTarget = Vector2.Distance(startPos, targetPos);
                
                // Check direct path status - always check regardless of distance
                isDirectPathBlocked = IsPathBlocked(startPos, targetPos);
                isDirectPathCrossingAvoidedLayers = IsPathCrossingAvoidedLayers(startPos, targetPos);
                
                // Calculate direct path cost (even if blocked, for comparison)
                directPathCost = distanceToTarget;
                if (isDirectPathBlocked || isDirectPathCrossingAvoidedLayers)
                {
                    // Apply a high cost penalty for blocked paths
                    directPathCost *= 10f;
                }
                
                lastStartPos = startPos;
                lastTargetPos = targetPos;
                lastPathResult = isDirectPathBlocked || isDirectPathCrossingAvoidedLayers;
                
                // If the direct path is blocked, ALWAYS try to find an alternate path
                // regardless of distance to target
                if (isDirectPathBlocked || isDirectPathCrossingAvoidedLayers)
                {
                    FindAndSetAlternatePath(startPos, targetPos);
                    if (debugLogDecisions)
                        Debug.Log($"Path blocked, searching for alternate path. Found path with {currentPath.Count} points");
                }
                else
                {
                    // Path not blocked, determine if we should use alternate for other reasons
                    bool shouldUseAlternateForCost = ShouldUseAlternateForCost();
                    
                    if (shouldUseAlternateForCost)
                    {
                        FindAndSetAlternatePath(startPos, targetPos);
                    }
                    else
                    {
                        // Clear path if we should be taking direct route
                        ClearPath();
                        currentDecisionReason = "Using direct path - alternate not needed";
                    }
                }
            }
            
            // Follow current path if we have one
            FollowCurrentPath();
        }
        
        // This separates path cost evaluation from blocking evaluation
        private bool ShouldUseAlternateForCost()
        {
            // If alternate path is significantly better, use it
            if (alternatePathCost < directPathCost * 0.8f)
            {
                currentDecisionReason = $"Alternate path more efficient ({alternatePathCost:F1} vs {directPathCost:F1})";
                if (debugLogDecisions)
                    Debug.Log($"Alternate path is more efficient: {alternatePathCost} vs {directPathCost}");
                return true;
            }
            
            return false;
        }
        
        private bool IsPathBlocked(Vector2 startPos, Vector2 targetPos)
        {
            Vector2 direction = (targetPos - startPos);
            float distance = direction.magnitude;
            direction.Normalize();
            
            // Check if there's an obstacle in the direct path
            float checkDistance = 0f;
            while (checkDistance < distance)
            {
                Vector2 checkPoint = startPos + direction * checkDistance;
                Collider2D obstacle = Physics2D.OverlapCircle(checkPoint, 0.1f, obstacleLayerMask);
                if (obstacle != null)
                {
                    if (debugLogDecisions)
                        Debug.Log($"Path blocked at distance {checkDistance:F1} from start by {obstacle.name}");
                    return true; // Path is blocked
                }
                checkDistance += obstacleCheckResolution;
            }
            
            // Double-check with a linecast
            RaycastHit2D hit = Physics2D.Linecast(startPos, targetPos, obstacleLayerMask);
            bool isBlocked = hit.collider != null;
            
            if (isBlocked && debugLogDecisions)
            {
                string layerName = LayerMask.LayerToName(hit.collider.gameObject.layer);
                Debug.Log($"Path blocked by linecast: hit {hit.collider.name} on layer {layerName}");
            }
            
            return isBlocked;
        }
        
        private bool IsPathCrossingAvoidedLayers(Vector2 startPos, Vector2 targetPos)
        {
            // No avoided layers configured
            if (avoidedLayers.value == 0)
                return false;
                
            // Check if the target itself is on an avoided layer (don't count that)
            Transform target = GetTargetFromNavigationController();
            
            // Use linecast for more accurate detection of crossed layers
            RaycastHit2D hit = Physics2D.Linecast(startPos, targetPos, avoidedLayers);
            
            // If we hit something that isn't the target, it's an avoided layer
            if (hit.collider != null && (target == null || hit.collider.transform != target))
            {
                if (debugLogDecisions)
                    Debug.Log($"Avoiding path through layer: {LayerMask.LayerToName(hit.collider.gameObject.layer)}");
                return true;
            }
            
            // For more complex situations, check multiple points along the path
            Vector2 direction = (targetPos - startPos);
            float distance = direction.magnitude;
            direction.Normalize();
            
            int numChecks = Mathf.CeilToInt(distance / obstacleCheckResolution);
            
            for (int i = 1; i < numChecks; i++) // Skip start point
            {
                float t = i / (float)numChecks;
                Vector2 checkPoint = Vector2.Lerp(startPos, targetPos, t);
                
                // Check if this point is inside an avoided layer
                Collider2D overlapCollider = Physics2D.OverlapPoint(checkPoint, avoidedLayers);
                if (overlapCollider != null && (target == null || overlapCollider.transform != target))
                {
                    if (debugLogDecisions)
                        Debug.Log($"Path crosses avoided layer at point {checkPoint}, layer: {LayerMask.LayerToName(overlapCollider.gameObject.layer)}");
                    return true;
                }
            }
            
            return false;
        }
        
        private void FindAndSetAlternatePath(Vector2 startPos, Vector2 targetPos)
        {
            // Try a much larger radius for pathfinding when the direct path is blocked
            float searchRadius = waypointSearchRadius;
            if (isDirectPathBlocked || isDirectPathCrossingAvoidedLayers)
            {
                // Use a very large radius when path is blocked to ensure we find alternatives
                searchRadius = 100f;
                
                if (debugLogDecisions)
                    Debug.Log($"Path blocked! Using extended search radius: {searchRadius}");
            }
            
            // Use waypoint system to find path
            List<Vector2> waypoints = waypointSystem.FindPathFromPositions(
                startPos, targetPos, searchRadius);
            
            if (waypoints.Count > 1)
            {
                // Calculate the cost of this path
                alternatePathCost = CalculatePathCost(waypoints);
                
                // Validate that alternate path doesn't cross avoided layers
                bool isAlternatePathValid = ValidateAlternatePath(waypoints);
                
                if (isAlternatePathValid)
                {
                    // Path found, set first waypoint as intermediate target
                    currentPath = waypoints;
                    currentPathIndex = 1; // Skip the first point (our position)
                    
                    // If the path only has one waypoint (just start and end), increase index
                    if (currentPathIndex >= currentPath.Count)
                    {
                        currentPathIndex = 0;
                        currentDecisionReason = "Simple alternate path (direct to target)";
                    }
                    else
                    {
                        SetIntermediateTarget(currentPath[currentPathIndex]);
                        currentDecisionReason = $"Using alternate path with {currentPath.Count-2} waypoints";
                    }
                    
                    if (debugLogDecisions)
                        Debug.Log($"Set alternate path with {currentPath.Count-2} waypoints, cost: {alternatePathCost:F2}");
                }
                else
                {
                    currentDecisionReason = "Alternate path crosses avoided layers";
                    if (debugLogDecisions)
                        Debug.LogWarning("Alternate path also crosses avoided layers, clearing path");
                    ClearPath();
                }
            }
            else
            {
                // Attempt with even larger radius as a last resort
                if (!isDirectPathBlocked && !isDirectPathCrossingAvoidedLayers)
                {
                    // If not blocked, don't keep trying with larger radius
                    currentDecisionReason = "No alternate path found - waypoints unavailable";
                    ClearPath();
                    return;
                }
                
                // Last attempt with huge radius - only when path is blocked
                waypoints = waypointSystem.FindPathFromPositions(
                    startPos, targetPos, 100f);
                
                if (waypoints.Count > 1)
                {
                    // Path found with extreme radius
                    currentPath = waypoints;
                    currentPathIndex = 1;
                    alternatePathCost = CalculatePathCost(waypoints);
                    SetIntermediateTarget(currentPath[currentPathIndex]);
                    currentDecisionReason = $"Last resort path found with {currentPath.Count-2} waypoints";
                    
                    if (debugLogDecisions)
                        Debug.Log($"Last resort path found with extreme radius!");
                }
                else
                {
                    // No path found even with extreme radius
                    currentDecisionReason = "CRITICAL: No alternate path found - waypoints unavailable";
                    if (debugLogDecisions)
                        Debug.LogError("No alternate path found through waypoints, even with extended radius!");
                    ClearPath();
                }
            }
        }
        
        private float CalculatePathCost(List<Vector2> path)
        {
            if (path.Count <= 1)
                return float.MaxValue;
            
            float cost = 0f;
            
            // Add up the distance of each segment
            for (int i = 0; i < path.Count - 1; i++)
            {
                cost += Vector2.Distance(path[i], path[i + 1]);
            }
            
            return cost;
        }
        
        private bool ValidateAlternatePath(List<Vector2> path)
        {
            // If no avoided layers set, path is always valid
            if (avoidedLayers.value == 0)
                return true;
                
            // Check each segment of the path
            for (int i = 0; i < path.Count - 1; i++)
            {
                if (IsPathCrossingAvoidedLayers(path[i], path[i+1]))
                {
                    return false; // Path segment crosses avoided layers
                }
            }
            
            return true; // All segments are valid
        }
        
        private void ClearPath()
        {
            currentPath.Clear();
            currentPathIndex = 0;
            ClearIntermediateTarget();
            alternatePathCost = float.MaxValue;
        }
        
        private void FollowCurrentPath()
        {
            if (currentPath.Count <= 1 || currentPathIndex >= currentPath.Count) return;
            
            // Check if we've reached the current waypoint
            float distToWaypoint = Vector2.Distance(transform.position, currentPath[currentPathIndex]);
            if (distToWaypoint < 0.5f)
            {
                // Move to next waypoint
                currentPathIndex++;
                
                if (currentPathIndex < currentPath.Count)
                {
                    // Set new intermediate target
                    SetIntermediateTarget(currentPath[currentPathIndex]);
                    
                    if (debugLogDecisions)
                        Debug.Log($"Moving to next waypoint {currentPathIndex} of {currentPath.Count-1}");
                }
                else
                {
                    // End of path reached
                    if (debugLogDecisions)
                        Debug.Log("End of path reached");
                    ClearPath();
                }
            }
        }
        
        private Transform GetTargetFromNavigationController()
        {
            // Try to get target using reflection since it's private
            var field = navigationController.GetType().GetField("target", 
                                System.Reflection.BindingFlags.NonPublic | 
                                System.Reflection.BindingFlags.Instance);
                                
            if (field != null)
            {
                return field.GetValue(navigationController) as Transform;
            }
            
            // Fallback - look for player
            return GameObject.FindGameObjectWithTag("Player")?.transform;
        }
        
        private void SetIntermediateTarget(Vector2 position)
        {
            // Clean up any existing intermediate target first
            ClearIntermediateTarget();
            
            // Create a temporary object at this position
            GameObject tempTarget = new GameObject("IntermediateTarget");
            tempTarget.transform.position = position;
            
            // Try to set it as the navigation target
            var field = navigationController.GetType().GetField("intermediateTarget", 
                System.Reflection.BindingFlags.NonPublic | 
                System.Reflection.BindingFlags.Instance);
                
            if (field != null)
            {
                field.SetValue(navigationController, tempTarget.transform);
                
                if (debugLogDecisions)
                    Debug.Log($"Set intermediate target at {position}");
            }
            else
            {
                // Try to use a public method if available
                navigationController.SendMessage("SetIntermediateTarget", tempTarget.transform, SendMessageOptions.DontRequireReceiver);
            }
        }
        
        private void ClearIntermediateTarget()
        {
            // Reset intermediate target
            var field = navigationController.GetType().GetField("intermediateTarget", 
                                System.Reflection.BindingFlags.NonPublic | 
                                System.Reflection.BindingFlags.Instance);
                                
            if (field != null)
            {
                Transform currentTarget = field.GetValue(navigationController) as Transform;
                if (currentTarget != null)
                {
                    Destroy(currentTarget.gameObject);
                }
                field.SetValue(navigationController, null);
            }
        }
        
        // Helper method to convert LayerMask to string for debugging
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
            return names.Length > 0 ? names.ToString() + $" ({mask.value})" : "Nothing";
        }
        
        private void OnDrawGizmos()
        {
            if (!visualizePaths || !Application.isPlaying)
                return;
            
            // Only show decision reason - much simpler debugging
            #if UNITY_EDITOR
            // Draw the current decision above the enemy
            Vector3 infoPos = new Vector3(transform.position.x, transform.position.y + 2.0f, 0);
            bool usingAlternate = currentPath.Count > 1;
            
            string routeInfo = usingAlternate ? "USING ALTERNATE PATH" : "USING DIRECT PATH";
            
            // Simplified text - one line only
            UnityEditor.Handles.color = usingAlternate ? Color.green : Color.white;
            UnityEditor.Handles.Label(infoPos, routeInfo);
            
            // Show path blocked status separately on second line
            if (isDirectPathBlocked)
            {
                Vector3 blockPos = new Vector3(transform.position.x, transform.position.y + 1.5f, 0);
                UnityEditor.Handles.color = Color.red;
                UnityEditor.Handles.Label(blockPos, "DIRECT PATH BLOCKED!");
            }
            
            // Draw direct path line
            if (lastStartPos != Vector2.zero && lastTargetPos != Vector2.zero)
            {
                Gizmos.color = isDirectPathBlocked ? Color.red : Color.white;
                Gizmos.DrawLine(lastStartPos, lastTargetPos);
            }
            
            // Draw alternate path with minimal labels
            if (usingAlternate && currentPath.Count > 1)
            {
                Gizmos.color = Color.green;
                for (int i = 0; i < currentPath.Count - 1; i++)
                {
                    Gizmos.DrawLine(currentPath[i], currentPath[i + 1]);
                }
                
                // Highlight active waypoint
                if (currentPathIndex < currentPath.Count)
                {
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawWireSphere(currentPath[currentPathIndex], 0.4f);
                }
            }
            #endif
        }
    }
}