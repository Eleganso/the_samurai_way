using UnityEngine;
using System.Collections.Generic;

namespace Enemies.Navigation
{
    [RequireComponent(typeof(EnemyNavigationController))]
    public class PathPredictor : MonoBehaviour
    {
        [Header("Path Prediction Settings")]
        [SerializeField] private int maxPredictionSteps = 20;
        [SerializeField] private float stepDistance = 1f;
        [SerializeField] private float directPathCostWeight = 1f;
        [SerializeField] private float heightDifferenceWeight = 10f;
        [SerializeField] private float obstacleCountWeight = 3f;
        [SerializeField] private float maxPathfindingDistance = 15f;
        
        [Header("Path Switching")]
        [SerializeField] private float pathSwitchCooldown = 1.5f; // How long before switching paths
        [SerializeField] private bool targetTrackingPriority = true; // Makes direct tracking a priority
        [SerializeField] private float minDirectDistanceThreshold = 5f; // If player is closer than this, use direct path
        
        [Header("Debug Visualization")]
        [SerializeField] private bool visualizePaths = true;
        [SerializeField] private Color directPathColor = Color.green;
        [SerializeField] private Color alternatePathColor = Color.yellow;
        [SerializeField] private Color blockedPathColor = Color.red;
        
        // References
        private EnemyNavigationController navigationController;
        private Rigidbody2D rb;
        private ObstacleDetection obstacleDetector;
        
        // Cached layer mask for obstacles
        private LayerMask obstacleLayer;
        
        // Path data
        private List<Vector2> directPath = new List<Vector2>();
        private List<Vector2> alternatePath = new List<Vector2>();
        private bool isDirectPathBlocked = false;
        private float directPathCost = 0f;
        private float alternatePathCost = float.MaxValue;
        
        // References to target and waypoint system
        private Transform target;
        private Transform currentTarget; // Current actual navigation target
        private WaypointSystem waypointSystem;
        
        // Path switching state
        private bool isUsingAlternatePath = false;
        private float lastPathSwitchTime = 0f;
        private GameObject tempTarget = null;
        private Vector2 lastTargetPosition;
        private bool targetJustSwitchedSides = false;
        
        private void Awake()
        {
            navigationController = GetComponent<EnemyNavigationController>();
            rb = GetComponent<Rigidbody2D>();
            obstacleDetector = GetComponent<ObstacleDetection>();
            
            // Find WaypointSystem
            waypointSystem = FindObjectOfType<WaypointSystem>();
            if (waypointSystem == null)
            {
                Debug.LogWarning("WaypointSystem not found. PathPredictor will have limited functionality.");
            }
            
            // Set a default obstacle layer if we can't get it from ObstacleDetection
            obstacleLayer = LayerMask.GetMask("Obstacle");
            
            // Try to get the obstacle layer from the navigation controller
            if (navigationController != null)
            {
                var obstacleLayerField = navigationController.GetType().GetField("obstacleLayer", 
                                             System.Reflection.BindingFlags.NonPublic | 
                                             System.Reflection.BindingFlags.Instance);
                                             
                if (obstacleLayerField != null)
                {
                    obstacleLayer = (LayerMask)obstacleLayerField.GetValue(navigationController);
                    Debug.Log($"Got obstacle layer from navigation controller: {LayerMaskToString(obstacleLayer)}");
                }
            }
        }
        
        private void Start()
        {
            // Get target from navigation controller
            var navTarget = navigationController.GetType().GetField("target", 
                                System.Reflection.BindingFlags.NonPublic | 
                                System.Reflection.BindingFlags.Instance);
            
            if (navTarget != null)
            {
                target = (Transform)navTarget.GetValue(navigationController);
                currentTarget = target;
            }
            
            if (target == null)
            {
                GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
                if (playerObj != null)
                {
                    target = playerObj.transform;
                    currentTarget = target;
                }
            }
            
            if (target == null)
            {
                Debug.LogError("No target found for PathPredictor.");
                enabled = false;
            }
            
            if (target != null)
            {
                lastTargetPosition = target.position;
            }
        }
        
        private void Update()
        {
            if (target == null || !navigationController.enabled)
                return;
                
            // Check if the path switch cooldown is active
            if (Time.time - lastPathSwitchTime < pathSwitchCooldown)
                return;
                
            // Direct distance to target
            float distanceToTarget = Vector2.Distance(transform.position, target.position);
            
            // Check if target has switched sides (crossed over the enemy)
            CheckTargetSideSwitched();
            
            // If target just switched sides OR is close, force direct path
            if (targetJustSwitchedSides || (targetTrackingPriority && distanceToTarget < minDirectDistanceThreshold))
            {
                if (isUsingAlternatePath)
                {
                    RevertToDirectPath();
                    isUsingAlternatePath = false;
                    lastPathSwitchTime = Time.time;
                    Debug.Log("Switched to direct path due to target switching sides or being close");
                }
                targetJustSwitchedSides = false;
                return;
            }
            
            // Don't recalculate if target is too far
            if (distanceToTarget > maxPathfindingDistance)
                return;
                
            // Predict paths
            PredictDirectPath();
            PredictAlternatePath();
            
            // Decide if we should switch paths
            bool shouldUseAlternatePath = ShouldUseAlternatePath();
            
            if (shouldUseAlternatePath != isUsingAlternatePath)
            {
                // Switch paths
                if (shouldUseAlternatePath)
                {
                    ApplyAlternatePathNavigation();
                    isUsingAlternatePath = true;
                }
                else
                {
                    RevertToDirectPath();
                    isUsingAlternatePath = false;
                }
                
                // Set cooldown
                lastPathSwitchTime = Time.time;
            }
            
            // Save target position for next frame
            lastTargetPosition = target.position;
        }
        
        // Check if target crossed over the enemy (switched sides)
        private void CheckTargetSideSwitched()
        {
            if (target == null) return;
            
            // Calculate which side of the enemy the target is on
            bool wasOnRight = lastTargetPosition.x > transform.position.x;
            bool isOnRight = target.position.x > transform.position.x;
            
            // If target switched sides, flag it
            if (wasOnRight != isOnRight)
            {
                targetJustSwitchedSides = true;
                Debug.Log("Target switched sides!");
            }
        }
        
        // Check if we should use the alternate path
        public bool ShouldUseAlternatePath()
        {
            // If direct path is blocked, use alternate
            if (isDirectPathBlocked && alternatePathCost < float.MaxValue)
                return true;
                
            // If alternate path is significantly better, use it
            if (alternatePathCost < directPathCost * 0.8f)
                return true;
                
            // Default to direct path
            return false;
        }
        
        // Switch to direct path targeting the player
        private void RevertToDirectPath()
        {
            // Access the target field
            var navTarget = navigationController.GetType().GetField("target", 
                                System.Reflection.BindingFlags.NonPublic | 
                                System.Reflection.BindingFlags.Instance);
                                
            if (navTarget != null && target != null)
            {
                // Set the original target
                navTarget.SetValue(navigationController, target);
                currentTarget = target;
                
                Debug.Log("Reverting to direct path to player");
            }
            
            // Clean up temporary target if any
            if (tempTarget != null)
            {
                Destroy(tempTarget);
                tempTarget = null;
            }
        }
        
        // Predict the direct path to target
        private void PredictDirectPath()
        {
            directPath.Clear();
            isDirectPathBlocked = false;
            
            // Start position
            Vector2 currentPos = rb.position;
            directPath.Add(currentPos);
            
            // Target position
            Vector2 targetPos = target.position;
            
            // Direction to target
            Vector2 direction = (targetPos - currentPos).normalized;
            float totalDistance = Vector2.Distance(currentPos, targetPos);
            
            // Count obstacles and height differences
            int obstacleCount = 0;
            float totalHeightDifference = 0f;
            
            // Check if there's a direct line of sight
            RaycastHit2D hit = Physics2D.Linecast(currentPos, targetPos, obstacleLayer);
            
            // If direct line is clear, just add target and return
            if (hit.collider == null || hit.collider.transform == target)
            {
                directPath.Add(targetPos);
                
                // Still account for height difference
                totalHeightDifference = Mathf.Abs(targetPos.y - currentPos.y);
                
                directPathCost = CalculatePathCost(directPath, obstacleCount, totalHeightDifference);
                return;
            }
            
            // Step through the path
            for (int i = 0; i < maxPredictionSteps && Vector2.Distance(currentPos, targetPos) > stepDistance; i++)
            {
                // Move one step
                Vector2 newPos = currentPos + direction * stepDistance;
                
                // Check for obstacles
                hit = Physics2D.Linecast(currentPos, newPos, obstacleLayer);
                if (hit.collider != null && hit.collider.transform != target && 
                    hit.collider.transform != transform && !hit.collider.transform.IsChildOf(transform))
                {
                    obstacleCount++;
                    
                    // Path is blocked by a significant obstacle
                    if (obstacleCount >= 3)
                    {
                        isDirectPathBlocked = true;
                        break;
                    }
                    
                    // Try to go around the obstacle
                    newPos = hit.point + hit.normal * 0.5f;
                }
                
                // Add height difference
                totalHeightDifference += Mathf.Abs(newPos.y - currentPos.y);
                
                // Add point to path
                directPath.Add(newPos);
                currentPos = newPos;
            }
            
            // Add target as final point if we got close enough
            if (!isDirectPathBlocked)
            {
                directPath.Add(targetPos);
            }
            
            // Calculate path cost
            directPathCost = CalculatePathCost(directPath, obstacleCount, totalHeightDifference);
        }
        
        // Predict an alternate path using waypoints
        private void PredictAlternatePath()
        {
            alternatePath.Clear();
            alternatePathCost = float.MaxValue;
            
            if (waypointSystem == null)
                return;
                
            // Find path using waypoint system
            List<Vector2> waypointPath = waypointSystem.FindPathFromPositions(
                rb.position, target.position, maxPathfindingDistance);
                
            if (waypointPath.Count <= 1)
                return;
                
            // Set the alternate path
            alternatePath = waypointPath;
            
            // Calculate path metrics
            int obstacleCount = 0;
            float totalHeightDifference = 0f;
            
            // Check each path segment for obstacles and height differences
            for (int i = 0; i < alternatePath.Count - 1; i++)
            {
                Vector2 start = alternatePath[i];
                Vector2 end = alternatePath[i + 1];
                
                // Count obstacles
                RaycastHit2D hit = Physics2D.Linecast(start, end, obstacleLayer);
                if (hit.collider != null && hit.collider.transform != target &&
                    hit.collider.transform != transform && !hit.collider.transform.IsChildOf(transform))
                {
                    obstacleCount++;
                }
                
                // Add height difference with a discount for ladder segments
                float heightDiff = Mathf.Abs(end.y - start.y);
                
                // If this is a ladder segment (mostly vertical), apply discount
                if (IsLadderSegment(i))
                {
                    // Apply 80% discount to ladder height differences
                    totalHeightDifference += heightDiff * 0.2f;
                }
                else
                {
                    totalHeightDifference += heightDiff;
                }
            }
            
            // Calculate cost and apply a small discount to waypoint paths
            alternatePathCost = CalculatePathCost(alternatePath, obstacleCount, totalHeightDifference) * 0.9f;
        }
        
        // Check if a segment is likely a ladder (mostly vertical)
        private bool IsLadderSegment(int index)
        {
            if (alternatePath.Count <= index + 1) return false;
            
            Vector2 start = alternatePath[index];
            Vector2 end = alternatePath[index + 1];
            
            float yDiff = Mathf.Abs(end.y - start.y);
            float xDiff = Mathf.Abs(end.x - start.x);
            
            // If vertical component is much larger than horizontal, it's likely a ladder
            return yDiff > xDiff * 1.5f;
        }
        
        // Apply the alternate path to the navigation controller
        private void ApplyAlternatePathNavigation()
        {
            if (alternatePath.Count <= 1)
                return;
                
            // Get next waypoint (index 1 since index 0 is current position)
            if (alternatePath.Count > 1)
            {
                Vector2 nextPoint = alternatePath[1];
                
                // For now, directly access the target field if available
                var navTarget = navigationController.GetType().GetField("target", 
                                    System.Reflection.BindingFlags.NonPublic | 
                                    System.Reflection.BindingFlags.Instance);
                                    
                if (navTarget != null)
                {
                    // Clean up existing temp target if any
                    if (tempTarget != null)
                    {
                        Destroy(tempTarget);
                    }
                    
                    // Create temporary object at waypoint position
                    tempTarget = new GameObject("TempNavigationTarget");
                    tempTarget.transform.position = nextPoint;
                    
                    // Set as target
                    navTarget.SetValue(navigationController, tempTarget.transform);
                    currentTarget = tempTarget.transform;
                    
                    Debug.Log($"Set intermediate target at {nextPoint}");
                }
            }
        }
        
        // Calculate the cost of a path based on length, obstacles, and height changes
        private float CalculatePathCost(List<Vector2> path, int obstacleCount, float heightDifference)
        {
            if (path.Count < 2)
                return float.MaxValue;
                
            // Calculate path length
            float pathLength = 0f;
            for (int i = 0; i < path.Count - 1; i++)
            {
                pathLength += Vector2.Distance(path[i], path[i + 1]);
            }
            
            // Calculate total cost
            float cost = pathLength * directPathCostWeight + 
                        heightDifference * heightDifferenceWeight + 
                        obstacleCount * obstacleCountWeight;
                        
            return cost;
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
        
        // Visualize the paths
        private void OnDrawGizmos()
        {
            if (!visualizePaths || !Application.isPlaying)
                return;
                
            // Draw direct path
            if (directPath.Count > 1)
            {
                Gizmos.color = isDirectPathBlocked ? blockedPathColor : directPathColor;
                for (int i = 0; i < directPath.Count - 1; i++)
                {
                    Gizmos.DrawLine(directPath[i], directPath[i + 1]);
                }
                
                // Draw cost
                if (directPath.Count > 0)
                {
                    Vector2 midPoint = directPath[directPath.Count / 2];
                    DrawLabel(midPoint, $"Cost: {directPathCost:F1}");
                }
            }
            
            // Draw alternate path
            if (alternatePath.Count > 1)
            {
                Gizmos.color = alternatePathColor;
                for (int i = 0; i < alternatePath.Count - 1; i++)
                {
                    // Highlight ladder segments
                    if (IsLadderSegment(i))
                    {
                        Gizmos.color = Color.magenta;
                        Gizmos.DrawLine(alternatePath[i], alternatePath[i + 1]);
                        Gizmos.color = alternatePathColor;
                    }
                    else
                    {
                        Gizmos.DrawLine(alternatePath[i], alternatePath[i + 1]);
                    }
                }
                
                // Draw cost
                if (alternatePath.Count > 0)
                {
                    Vector2 midPoint = alternatePath[alternatePath.Count / 2];
                    DrawLabel(midPoint, $"Cost: {alternatePathCost:F1}");
                }
            }
            
            // Show current target info
            if (isUsingAlternatePath)
            {
                DrawLabel(transform.position + Vector3.up * 0.5f, "Using Alternate Path");
                
                // Highlight current waypoint
                if (tempTarget != null)
                {
                    Gizmos.color = Color.cyan;
                    Gizmos.DrawWireSphere(tempTarget.transform.position, 0.3f);
                }
            }
            else
            {
                DrawLabel(transform.position + Vector3.up * 0.5f, "Using Direct Path");
            }
        }
        
        private void DrawLabel(Vector2 position, string text)
        {
            #if UNITY_EDITOR
            UnityEditor.Handles.Label(position, text);
            #endif
        }
        
        // Clean up on destroy
        private void OnDestroy()
        {
            if (tempTarget != null)
            {
                Destroy(tempTarget);
                tempTarget = null;
            }
        }
    }
}