using UnityEngine;
using System.Collections.Generic;

namespace Enemies.Navigation
{
    public class ImprovedPathPlanning : MonoBehaviour
    {
        [SerializeField] private LayerMask obstacleLayerMask; // Ground and wall layers
        [SerializeField] private float obstacleCheckResolution = 0.25f; // Distance between path checks
        [SerializeField] private bool visualizePaths = true;
        
        private WaypointSystem waypointSystem;
        private EnemyNavigationController navigationController;
        
        private bool isDirectPathBlocked = false;
        private List<Vector2> currentPath = new List<Vector2>();
        private int currentPathIndex = 0;
        
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
        }
        
        private void Update()
        {
            if (navigationController == null) return;
            
            // Get target from navigation controller
            Transform target = GetTargetFromNavigationController();
            if (target == null) return;
            
            // Check if direct path is blocked
            Vector2 startPos = transform.position;
            Vector2 targetPos = target.position;
            
            // Only recalculate path periodically or when positions change significantly
            if (ShouldRecalculatePath(startPos, targetPos))
            {
                isDirectPathBlocked = IsPathBlocked(startPos, targetPos);
                lastStartPos = startPos;
                lastTargetPos = targetPos;
                lastPathResult = isDirectPathBlocked;
                
                // If direct path is blocked, find alternate path using waypoints
                if (isDirectPathBlocked && waypointSystem != null)
                {
                    FindAndSetAlternatePath(startPos, targetPos);
                }
                else
                {
                    ClearPath();
                }
            }
            
            // Follow current path if we have one
            FollowCurrentPath();
        }
        
        private bool ShouldRecalculatePath(Vector2 startPos, Vector2 targetPos)
        {
            // Recalculate if positions have changed significantly
            float startDifference = Vector2.Distance(startPos, lastStartPos);
            float targetDifference = Vector2.Distance(targetPos, lastTargetPos);
            
            return startDifference > 1f || targetDifference > 1f || Time.frameCount % 30 == 0;
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
        
        private void FindAndSetAlternatePath(Vector2 startPos, Vector2 targetPos)
        {
            // Use waypoint system to find path
            currentPath = waypointSystem.FindPathFromPositions(startPos, targetPos, 15f);
            
            if (currentPath.Count > 1)
            {
                // Path found, set first waypoint as intermediate target
                currentPathIndex = 1; // Skip the first point (our position)
                SetIntermediateTarget(currentPath[currentPathIndex]);
            }
            else
            {
                // No path found, fall back to direct movement
                ClearPath();
            }
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
                Gizmos.color = Color.green;
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
                Gizmos.color = lastPathResult ? Color.red : Color.white;
                Gizmos.DrawLine(lastStartPos, lastTargetPos);
            }
        }
    }
}