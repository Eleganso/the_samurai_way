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
        [SerializeField] private float heightDifferenceWeight = 2f;
        [SerializeField] private float obstacleCountWeight = 3f;
        [SerializeField] private float maxPathfindingDistance = 15f;
        
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
        private WaypointSystem waypointSystem;
        
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
                
            // Check distance to target
            float distanceToTarget = Vector2.Distance(transform.position, target.position);
            if (distanceToTarget > maxPathfindingDistance)
                return;
                
            // Predict paths
            PredictDirectPath();
            PredictAlternatePath();
            
            // Compare paths and update navigation if needed
            if (ShouldUseAlternatePath())
            {
                ApplyAlternatePathNavigation();
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
                directPathCost = totalDistance;
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
                
                // Add height difference
                totalHeightDifference += Mathf.Abs(end.y - start.y);
            }
            
            // Calculate cost
            alternatePathCost = CalculatePathCost(alternatePath, obstacleCount, totalHeightDifference);
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
                
                // Set intermediate target for navigation
                // This requires adding a SetIntermediateTarget method to EnemyNavigationController
                // which we'll implement later
                
                // For now, directly access the target field if available
                var navTarget = navigationController.GetType().GetField("target", 
                                    System.Reflection.BindingFlags.NonPublic | 
                                    System.Reflection.BindingFlags.Instance);
                                    
                if (navTarget != null)
                {
                    // Store original target
                    Transform originalTarget = target;
                    
                    // Create temporary object at waypoint position
                    GameObject tempTarget = new GameObject("TempNavigationTarget");
                    tempTarget.transform.position = nextPoint;
                    
                    // Set as target
                    navTarget.SetValue(navigationController, tempTarget.transform);
                    
                    // Schedule destruction and target reset
                    StartCoroutine(ResetTarget(tempTarget, originalTarget, 1.5f));
                }
            }
        }
        
        // Reset the target after using a temporary waypoint
        private System.Collections.IEnumerator ResetTarget(GameObject tempTarget, Transform originalTarget, float delay)
        {
            yield return new WaitForSeconds(delay);
            
            // Reset original target
            var navTarget = navigationController.GetType().GetField("target", 
                                System.Reflection.BindingFlags.NonPublic | 
                                System.Reflection.BindingFlags.Instance);
                                
            if (navTarget != null)
            {
                navTarget.SetValue(navigationController, originalTarget);
            }
            
            // Destroy temp object
            Destroy(tempTarget);
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
                    Gizmos.DrawLine(alternatePath[i], alternatePath[i + 1]);
                }
                
                // Draw cost
                if (alternatePath.Count > 0)
                {
                    Vector2 midPoint = alternatePath[alternatePath.Count / 2];
                    DrawLabel(midPoint, $"Cost: {alternatePathCost:F1}");
                }
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