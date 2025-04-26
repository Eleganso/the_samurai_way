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
        
        [Header("Debug")]
        [SerializeField] private bool visualizePaths = true;
        [SerializeField] private Color directPathColor = Color.white;
        [SerializeField] private Color avoidedPathColor = Color.red;
        [SerializeField] private Color alternatePathColor = Color.green;
        
        private WaypointSystem waypointSystem;
        private EnemyNavigationController navigationController;
        
        private bool isDirectPathBlocked = false;
        private bool isDirectPathCrossingAvoidedLayers = false;
        private List<Vector2> currentPath = new List<Vector2>();
        private int currentPathIndex = 0;
        
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
                
                // Check direct path status
                isDirectPathBlocked = IsPathBlocked(startPos, targetPos);
                isDirectPathCrossingAvoidedLayers = IsPathCrossingAvoidedLayers(startPos, targetPos);
                
                lastStartPos = startPos;
                lastTargetPos = targetPos;
                lastPathResult = isDirectPathBlocked || isDirectPathCrossingAvoidedLayers;
                
                // Determine if we should use alternate path
                bool shouldUseAlternatePath = ShouldUseAlternatePath();
                
                if (shouldUseAlternatePath)
                {
                    // If we don't already have a path, or if the path needs refreshing
                    if (currentPath.Count <= 1 || 
                        Vector2.Distance(startPos, currentPath[0]) > 1.0f ||
                        Vector2.Distance(targetPos, currentPath[currentPath.Count-1]) > 1.0f)
                    {
                        FindAndSetAlternatePath(startPos, targetPos);
                    }
                }
                else
                {
                    // Clear path if we should be taking direct route
                    ClearPath();
                }
            }
            
            // Follow current path if we have one
            FollowCurrentPath();
        }
        
        private bool ShouldUseAlternatePath()
        {
            // Always use alternate path if direct path crosses avoided layers
            if (isDirectPathCrossingAvoidedLayers && prioritizeAlternatePaths)
                return true;
            
            // Use alternate path if direct path is blocked
            if (isDirectPathBlocked)
                return true;
            
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
                if (Physics2D.OverlapCircle(checkPoint, 0.1f, obstacleLayerMask))
                {
                    return true; // Path is blocked
                }
                checkDistance += obstacleCheckResolution;
            }
            
            // Double-check with a linecast
            RaycastHit2D hit = Physics2D.Linecast(startPos, targetPos, obstacleLayerMask);
            return hit.collider != null;
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
                    return true;
                }
            }
            
            return false;
        }
        
        private void FindAndSetAlternatePath(Vector2 startPos, Vector2 targetPos)
        {
            // Use waypoint system to find path
            currentPath = waypointSystem.FindPathFromPositions(startPos, targetPos, 15f);
            
            if (currentPath.Count > 1)
            {
                // Validate that alternate path doesn't cross avoided layers
                bool isAlternatePathValid = ValidateAlternatePath(currentPath);
                
                if (isAlternatePathValid)
                {
                    // Path found, set first waypoint as intermediate target
                    currentPathIndex = 1; // Skip the first point (our position)
                    SetIntermediateTarget(currentPath[currentPathIndex]);
                    Debug.Log($"Set alternate path with {currentPath.Count-2} waypoints");
                }
                else
                {
                    Debug.LogWarning("Alternate path also crosses avoided layers, clearing path");
                    ClearPath();
                }
            }
            else
            {
                // No path found, fall back to direct movement
                ClearPath();
            }
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
                }
                else
                {
                    // End of path reached
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
        
        private void OnDrawGizmos()
        {
            if (!visualizePaths || !Application.isPlaying) return;
            
            // Draw current path
            if (currentPath.Count > 1)
            {
                Gizmos.color = alternatePathColor;
                for (int i = 0; i < currentPath.Count - 1; i++)
                {
                    Gizmos.DrawLine(currentPath[i], currentPath[i + 1]);
                    Gizmos.DrawSphere(currentPath[i], 0.2f);
                }
                Gizmos.DrawSphere(currentPath[currentPath.Count - 1], 0.2f);
                
                // Highlight current target waypoint
                if (currentPathIndex < currentPath.Count)
                {
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawWireSphere(currentPath[currentPathIndex], 0.5f);
                }
            }
            
            // Draw direct path and show if it's blocked
            if (lastStartPos != Vector2.zero && lastTargetPos != Vector2.zero)
            {
                Gizmos.color = lastPathResult ? avoidedPathColor : directPathColor;
                Gizmos.DrawLine(lastStartPos, lastTargetPos);
                
                // Indicate specifically if it's an avoided path
                if (isDirectPathCrossingAvoidedLayers)
                {
                    Gizmos.color = Color.red;
                    Vector2 midpoint = (lastStartPos + lastTargetPos) / 2;
                    Gizmos.DrawWireSphere(midpoint, 0.3f);
                    
                    #if UNITY_EDITOR
                    UnityEditor.Handles.Label(midpoint, "Avoided Layer");
                    #endif
                }
            }
        }
    }
}