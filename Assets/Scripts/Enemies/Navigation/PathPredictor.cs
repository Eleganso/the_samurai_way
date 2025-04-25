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
        [SerializeField] private float heightDifferenceWeight = 15f; // Increased from 10f to 15f
        [SerializeField] private float obstacleCountWeight = 5f; // Increased from 3f to 5f
        [SerializeField] private float maxPathfindingDistance = 15f;
        [SerializeField] private float verticalPathPenalty = 10f; // Increased from 5f to 10f
        [SerializeField] private float alternatePathPreference = 0.3f; // Made even more aggressive (from 0.4f to 0.3f)
        [SerializeField] private float minVerticalDifferenceForAlternate = 1.0f; // Force alternate path for height diff > 1.0
        
        [Header("Ladder Settings")]
        [SerializeField] private float ladderSegmentDiscount = 0.1f; // 90% discount for ladder segments (was 0.2f)
        [SerializeField] private float ladderSegmentVerticalRatio = 1.5f; // Identify ladder when vertical > 1.5x horizontal
        [SerializeField] private float waypointPathDiscount = 0.7f; // 30% discount for any waypoint path (was 0.9f)
        
        [Header("Path Switching")]
        [SerializeField] private float pathSwitchCooldownDuration = 4f; // Longer cooldown (was 2f)
        [SerializeField] private float pathSwitchPenalty = 0.7f; // Stronger path switching penalty
        
        [Header("Debug Visualization")]
        [SerializeField] private bool visualizePaths = true;
        [SerializeField] private Color directPathColor = Color.green;
        [SerializeField] private Color alternatePathColor = Color.yellow;
        [SerializeField] private Color blockedPathColor = Color.red;
        [SerializeField] private Color ladderSegmentColor = Color.magenta;
        
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
        private WaypointSystem waypointSystem;
        
        // Ladder detection
        private bool hasLadderInAlternatePath = false;
        
        // Consistency tracking - reduces path switching
        private bool wasUsingAlternatePath = false;
        private float pathSwitchCooldown = 0f;
        private int consecutiveAlternatePathFrames = 0;
        private int consecutiveDirectPathFrames = 0;
        private int requiredConsistentFrames = 10; // Require this many frames before switching back

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
            }
            
            if (target == null)
            {
                GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
                if (playerObj != null)
                {
                    target = playerObj.transform;
                }
            }
            
            if (target == null)
            {
                Debug.LogError("No target found for PathPredictor.");
                enabled = false;
            }
        }
        
        private void Update()
        {
            if (target == null || !navigationController.enabled)
                return;
            
            // Update path switch cooldown    
            if (pathSwitchCooldown > 0)
            {
                pathSwitchCooldown -= Time.deltaTime;
            }
            
            // Check distance to target
            float distanceToTarget = Vector2.Distance(transform.position, target.position);
            if (distanceToTarget > maxPathfindingDistance)
                return;
                
            // Get vertical distance - critical for path decisions
            float verticalDistance = Mathf.Abs(target.position.y - transform.position.y);
            bool targetIsAbove = target.position.y > transform.position.y + 0.5f;
            bool significantHeightDifference = verticalDistance > minVerticalDifferenceForAlternate;
            
            // Always clear path data before recalculating
            hasLadderInAlternatePath = false;
            
            // Predict paths
            PredictDirectPath();
            PredictAlternatePath();
            
            // Apply additional vertical penalty to direct path if significant height difference
            if (verticalDistance > minVerticalDifferenceForAlternate)
            {
                directPathCost += verticalDistance * verticalPathPenalty;
            }
            
            // Fast track decision if target is above and there's a ladder
            bool forceAlternatePath = false;
            if (targetIsAbove && significantHeightDifference && hasLadderInAlternatePath)
            {
                forceAlternatePath = true;
            }
            
            // Decision logic
            bool shouldUseAlternate = ShouldUseAlternatePath() || forceAlternatePath;
            
            // Update consecutive frame counters
            if (shouldUseAlternate)
            {
                consecutiveAlternatePathFrames++;
                consecutiveDirectPathFrames = 0;
            }
            else
            {
                consecutiveDirectPathFrames++;
                consecutiveAlternatePathFrames = 0;
            }
            
            // Add hysteresis - require several consistent frames to switch FROM alternate path
            if (wasUsingAlternatePath && consecutiveDirectPathFrames < requiredConsistentFrames)
            {
                shouldUseAlternate = true;
            }
            
            // Compare paths and update navigation if needed
            if (shouldUseAlternate)
            {
                // Only apply path if not on cooldown or already using alternate
                if (pathSwitchCooldown <= 0 || wasUsingAlternatePath)
                {
                    ApplyAlternatePathNavigation();
                    
                    // Set flag only after successfully applying path
                    if (!wasUsingAlternatePath)
                    {
                        wasUsingAlternatePath = true;
                        pathSwitchCooldown = pathSwitchCooldownDuration;
                        Debug.Log("Switched TO alternate path with waypoints");
                    }
                }
            }
            else
            {
                // Only switch back if not on cooldown or already using direct
                if (pathSwitchCooldown <= 0 || !wasUsingAlternatePath)
                {
                    // Reset to original target
                    ResetToDirectPath();
                    
                    // Set flag only after successfully resetting
                    if (wasUsingAlternatePath)
                    {
                        wasUsingAlternatePath = false;
                        pathSwitchCooldown = pathSwitchCooldownDuration;
                        Debug.Log("Switched BACK to direct path");
                    }
                }
            }
            
            // Force the navigation controller to consider climbing if there's a ladder and the target is above
            if (wasUsingAlternatePath && hasLadderInAlternatePath && targetIsAbove)
            {
                ForceLadderClimbing();
            }
        }
        
        // Force the navigation to climb
        private void ForceLadderClimbing()
        {
            // Try to get the navigationController's state
            var currentStateField = navigationController.GetType().GetField("currentState", 
                                       System.Reflection.BindingFlags.NonPublic | 
                                       System.Reflection.BindingFlags.Instance);
                                       
            if (currentStateField != null)
            {
                // Get the current state
                object currentState = currentStateField.GetValue(navigationController);
                
                // Assuming NavigationState is accessible
                // Try to call ForceClimbingState method if we have one
                var forceClimbingMethod = navigationController.GetType().GetMethod("ForceClimbingState", 
                                              System.Reflection.BindingFlags.Public | 
                                              System.Reflection.BindingFlags.Instance);
                                              
                if (forceClimbingMethod != null)
                {
                    // Only force climbing if we're close to a ladder
                    bool nearLadder = IsNearLadder();
                    if (nearLadder)
                    {
                        forceClimbingMethod.Invoke(navigationController, null);
                    }
                }
            }
        }
        
        // Check if we're near a ladder
        private bool IsNearLadder()
        {
            // Only check if we have alternate path with ladder
            if (!hasLadderInAlternatePath || alternatePath.Count < 2)
                return false;
                
            // Check for a ladder segment within range
            float nearestLadderDistance = float.MaxValue;
            for (int i = 0; i < alternatePath.Count - 1; i++)
            {
                if (IsLadderSegment(i))
                {
                    // Calculate distance to ladder segment
                    Vector2 ladderSegmentStart = alternatePath[i];
                    float distToLadder = Vector2.Distance(rb.position, ladderSegmentStart);
                    nearestLadderDistance = Mathf.Min(nearestLadderDistance, distToLadder);
                }
            }
            
            // Consider "near ladder" if within 2 units
            return nearestLadderDistance < 2f;
        }
        
        // Reset to direct path to player
        private void ResetToDirectPath()
        {
            var navTarget = navigationController.GetType().GetField("target", 
                                System.Reflection.BindingFlags.NonPublic | 
                                System.Reflection.BindingFlags.Instance);
                                
            if (navTarget != null && target != null)
            {
                navTarget.SetValue(navigationController, target);
            }
        }
        
        // Check if we should use the alternate path
        public bool ShouldUseAlternatePath()
        {
            // If direct path is blocked, use alternate
            if (isDirectPathBlocked && alternatePathCost < float.MaxValue)
                return true;
                
            // If direct path has significant height difference and we have a ladder path
            float verticalDistance = Mathf.Abs(target.position.y - transform.position.y);
            if (verticalDistance > minVerticalDifferenceForAlternate && 
                hasLadderInAlternatePath && 
                alternatePathCost < float.MaxValue)
            {
                return true;
            }
                
            // If alternate path is significantly better, use it
            if (alternatePathCost < directPathCost * alternatePathPreference)
                return true;
                
            // Apply path switch penalty if currently using alternate path
            if (wasUsingAlternatePath)
            {
                // Need a MUCH better direct path to switch back
                if (directPathCost * pathSwitchPenalty < alternatePathCost)
                {
                    return false;
                }
                return true;
            }
                
            // Default to direct path
            return false;
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
                
                // Calculate vertical component of path
                float verticalDistance = Mathf.Abs(targetPos.y - currentPos.y);
                totalHeightDifference = verticalDistance;
                
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
                    if (obstacleCount >= 2) // More sensitive to obstacles
                    {
                        isDirectPathBlocked = true;
                        break;
                    }
                    
                    // Try to go around the obstacle
                    newPos = hit.point + hit.normal * 0.5f;
                }
                
                // Add height difference (with increased weight for vertical movement)
                float heightDiff = Mathf.Abs(newPos.y - currentPos.y);
                totalHeightDifference += heightDiff;
                
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
            hasLadderInAlternatePath = false;
            
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
            bool hasFoundLadder = false;
            
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
                
                // Add height difference - but with REDUCED weight for ladder segments
                float heightDiff = Mathf.Abs(end.y - start.y);
                
                // Check if this segment is part of a ladder (between LadderBottom and LadderTop waypoints)
                bool isLadder = IsLadderSegment(i);
                if (isLadder)
                {
                    hasFoundLadder = true;
                    // Ladder segments get a HUGE discount for vertical movement
                    totalHeightDifference += heightDiff * ladderSegmentDiscount;
                }
                else
                {
                    totalHeightDifference += heightDiff;
                }
            }
            
            // Update the ladder detection flag
            hasLadderInAlternatePath = hasFoundLadder;
            
            // Calculate cost
            alternatePathCost = CalculatePathCost(alternatePath, obstacleCount, totalHeightDifference);
            
            // Provide a bonus to alternate paths to make them more appealing
            alternatePathCost *= waypointPathDiscount;
            
            // Additional bonus if it contains a ladder and target is above
            if (hasLadderInAlternatePath && target.position.y > transform.position.y + 0.5f)
            {
                alternatePathCost *= 0.8f; // 20% additional discount for ladder paths when target is above
            }
        }
        
        // Check if a path segment is likely a ladder
        private bool IsLadderSegment(int segmentIndex)
        {
            // Very simple heuristic - if the segment is mostly vertical, consider it a ladder
            if (alternatePath.Count <= segmentIndex + 1) return false;
            
            Vector2 start = alternatePath[segmentIndex];
            Vector2 end = alternatePath[segmentIndex + 1];
            
            float yDiff = Mathf.Abs(end.y - start.y);
            float xDiff = Mathf.Abs(end.x - start.x);
            
            // If the segment is much more vertical than horizontal, assume it's a ladder
            return yDiff > xDiff * ladderSegmentVerticalRatio;
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
                
                // Check if we're close to nextPoint - if so, advance to the next waypoint
                if (Vector2.Distance(rb.position, nextPoint) < 0.5f && alternatePath.Count > 2)
                {
                    nextPoint = alternatePath[2];
                }
                
                // For now, directly access the target field if available
                var navTarget = navigationController.GetType().GetField("target", 
                                    System.Reflection.BindingFlags.NonPublic | 
                                    System.Reflection.BindingFlags.Instance);
                                    
                if (navTarget != null)
                {
                    // Check if we already have a temp target
                    Transform currentTarget = (Transform)navTarget.GetValue(navigationController);
                    
                    // Only create a new target if needed
                    if (currentTarget == null || currentTarget == target || 
                        Vector2.Distance(currentTarget.position, nextPoint) > 0.1f)
                    {
                        // Store original target
                        Transform originalTarget = target;
                        
                        // Destroy existing temp target if it exists and isn't the player
                        if (currentTarget != null && currentTarget != target)
                        {
                            Destroy(currentTarget.gameObject);
                        }
                        
                        // Create temporary object at waypoint position
                        GameObject tempTarget = new GameObject("TempNavigationTarget");
                        tempTarget.transform.position = nextPoint;
                        
                        // Set as target
                        navTarget.SetValue(navigationController, tempTarget.transform);
                        
                        // Schedule destruction and target reset
                        StartCoroutine(ResetTarget(tempTarget, originalTarget, 3.0f));
                    }
                }
            }
        }
        
        // Reset the target after using a temporary waypoint
        private System.Collections.IEnumerator ResetTarget(GameObject tempTarget, Transform originalTarget, float delay)
        {
            // Always wait for the specified delay
            yield return new WaitForSeconds(delay);
            
            // Only reset if we're still using this temp target
            var navTarget = navigationController.GetType().GetField("target", 
                                System.Reflection.BindingFlags.NonPublic | 
                                System.Reflection.BindingFlags.Instance);
                                
            if (navTarget != null)
            {
                Transform currentTarget = (Transform)navTarget.GetValue(navigationController);
                
                // Only reset if this is still our active temp target
                if (currentTarget != null && currentTarget.gameObject == tempTarget)
                {
                    // Only reset to original if we're no longer using alternate path
                    if (!wasUsingAlternatePath)
                    {
                        navTarget.SetValue(navigationController, originalTarget);
                    }
                    
                    // Destroy temp target if it still exists
                    Destroy(tempTarget);
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
                        Gizmos.color = ladderSegmentColor;
                        Gizmos.DrawLine(alternatePath[i], alternatePath[i + 1]);
                        // Draw thicker line for ladder
                        Vector2 perpendicular = Vector2.Perpendicular(alternatePath[i+1] - alternatePath[i]).normalized * 0.1f;
                        Gizmos.DrawLine(alternatePath[i] + perpendicular, alternatePath[i+1] + perpendicular);
                        Gizmos.DrawLine(alternatePath[i] - perpendicular, alternatePath[i+1] - perpendicular);
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
                
                // Highlight active target in the path
                if (alternatePath.Count > 1 && wasUsingAlternatePath)
                {
                    Gizmos.color = Color.cyan;
                    Gizmos.DrawWireSphere(alternatePath[1], 0.3f);
                }
            }
            
            // Draw decision state
            if (target != null)
            {
                #if UNITY_EDITOR
                string decisionText = wasUsingAlternatePath ? "Using Alternate Path" : "Using Direct Path";
                decisionText += hasLadderInAlternatePath ? " (Ladder Available)" : "";
                UnityEditor.Handles.Label(transform.position + Vector3.up * 1.5f, decisionText);
                #endif
            }
        }
        
        private void DrawLabel(Vector2 position, string text)
        {
            #if UNITY_EDITOR
            UnityEditor.Handles.Label(position, text);
            #endif
        }
    }
}