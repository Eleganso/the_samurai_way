using UnityEngine;
using System.Collections.Generic;

namespace Enemies.Navigation
{
    // Enhanced debugging tool for enemy detection systems
    public class EnemyDetectionDebugger : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform groundCheck;
        [SerializeField] private Transform wallCheck;
        [SerializeField] private LayerMask groundLayer;
        [SerializeField] private LayerMask obstacleLayer;
        
        [Header("Detection Settings")]
        [SerializeField] private float groundCheckRadius = 0.5f;
        [SerializeField] private float obstacleDetectionDistance = 1f;
        [SerializeField] private bool isFacingRight = true;
        
        [Header("Debug Options")]
        [SerializeField] private bool showDebug = true;
        [SerializeField] private bool logToConsole = true;
        [SerializeField] private bool forceGrounded = false;
        [SerializeField] private bool printGroundObjects = false;
        
        [Header("Debug Actions")]
        [SerializeField] private bool debugObstacles = false;
        
        [Header("Colors")]
        [SerializeField] private Color groundedColor = Color.green;
        [SerializeField] private Color notGroundedColor = Color.red;
        [SerializeField] private Color obstacleColor = Color.yellow;
        
        private bool isGrounded;
        private bool isObstacleDetected;
        private Rigidbody2D rb;
        private Collider2D[] groundColliders = new Collider2D[5];
        
        // Cache results for visualization
        private readonly List<Vector3> debugPoints = new List<Vector3>();
        private readonly List<Color> debugColors = new List<Color>();
        private readonly List<float> debugRadii = new List<float>();
        
        // Statistics
        private int framesGrounded = 0;
        private int totalFrames = 0;
        private string statusText = "";
        
        private void Start()
        {
            rb = GetComponent<Rigidbody2D>();
            
            // Try to find child transforms if not assigned
            if (groundCheck == null)
                groundCheck = transform.Find("GroundCheck");
                
            if (wallCheck == null)
                wallCheck = transform.Find("WallCheck");
                
            // Try to get layers from ObstacleDetection component if not set
            ObstacleDetection detector = GetComponent<ObstacleDetection>();
            if (detector != null)
            {
                // We can't directly access private fields, but we can check if our layers are 0
                if (groundLayer == 0 || obstacleLayer == 0)
                {
                    Debug.LogWarning("Ground or obstacle layer not set on EnemyDetectionDebugger. Make sure to set them manually.");
                }
            }
            
            if (groundLayer == 0)
            {
                Debug.LogError("Ground layer is not set! Ground detection will not work.");
            }
        }
        
        private void Update()
        {
            if (!showDebug) return;
            
            // Clear previous debug points
            debugPoints.Clear();
            debugColors.Clear();
            debugRadii.Clear();
            
            // Update detection states
            CheckGrounded();
            CheckObstacleAhead();
            
            // Update facing direction based on scale
            isFacingRight = transform.localScale.x > 0;
            
            // Update statistics
            totalFrames++;
            if (isGrounded) framesGrounded++;
            
            // Build status text
            EnemyNavigationController navController = GetComponent<EnemyNavigationController>();
            string stateStr = "Unknown";
            if (navController != null)
            {
                stateStr = navController.GetCurrentState().ToString();
            }
            
            statusText = $"{stateStr}\n";
            statusText += $"Grounded: {isGrounded || forceGrounded}, Obstacle: {isObstacleDetected}\n";
            statusText += $"Grounded {(framesGrounded * 100.0f / totalFrames):F1}% of time";
            
            // Check if debug button was pressed
            if (debugObstacles)
            {
                debugObstacles = false;
                CheckObstacleDetection();
            }
        }
        
        private void CheckGrounded()
        {
            if (groundCheck == null) return;
            
            // Store the initial state
            bool wasGrounded = isGrounded;
            isGrounded = false;
            
            // Try multiple detection methods
            // 1. OverlapCircle at exact position
            int numColliders = Physics2D.OverlapCircleNonAlloc(
                groundCheck.position, 
                groundCheckRadius, 
                groundColliders, 
                groundLayer
            );
            
            isGrounded = numColliders > 0;
            
            // Record debug visualization
            debugPoints.Add(groundCheck.position);
            debugColors.Add(isGrounded ? groundedColor : notGroundedColor);
            debugRadii.Add(groundCheckRadius);
            
            // 2. If not grounded, try with BoxCast
            if (!isGrounded)
            {
                RaycastHit2D hit = Physics2D.BoxCast(
                    groundCheck.position,
                    new Vector2(0.8f, 0.1f),
                    0f,
                    Vector2.down,
                    0.3f,
                    groundLayer
                );
                
                isGrounded = hit.collider != null;
                
                // Visualize box cast
                Vector2 boxCenter = (Vector2)groundCheck.position + new Vector2(0, -0.15f);
                debugPoints.Add(boxCenter);
                debugColors.Add(isGrounded ? groundedColor : notGroundedColor);
                debugRadii.Add(0.1f); // Just a marker
            }
            
            // 3. Try ray cast
            if (!isGrounded)
            {
                RaycastHit2D hit = Physics2D.Raycast(
                    groundCheck.position,
                    Vector2.down,
                    0.5f,
                    groundLayer
                );
                
                isGrounded = hit.collider != null;
                
                // Visualize raycast
                debugPoints.Add(groundCheck.position + Vector3.down * (hit.collider != null ? hit.distance : 0.5f));
                debugColors.Add(isGrounded ? groundedColor : notGroundedColor);
                debugRadii.Add(0.05f);
            }
            
            // Apply force grounded if enabled
            if (forceGrounded)
            {
                isGrounded = true;
            }
            
            // Log changes to grounded state to avoid spam
            if (logToConsole && wasGrounded != isGrounded)
            {
                if (isGrounded)
                {
                    Debug.Log($"Now GROUNDED at position {groundCheck.position}");
                }
                else
                {
//                    Debug.Log($"Now NOT GROUNDED at position {groundCheck.position}, Layer: {LayerMaskToString(groundLayer)}");
                }
            }
            
            // Print info about ground objects if requested
            if (printGroundObjects && numColliders > 0)
            {
                string colliderInfo = "Ground colliders detected:";
                for (int i = 0; i < numColliders; i++)
                {
                    colliderInfo += $"\n- {groundColliders[i].name} (Layer: {LayerMask.LayerToName(groundColliders[i].gameObject.layer)})";
                }
                Debug.Log(colliderInfo);
                
                // Only print once when requested
                printGroundObjects = false;
            }
        }
        
        private void CheckObstacleAhead()
        {
            if (wallCheck == null) return;
            
            Vector2 direction = isFacingRight ? Vector2.right : Vector2.left;
            RaycastHit2D hit = Physics2D.Raycast(wallCheck.position, direction, obstacleDetectionDistance, obstacleLayer);
            
            // Skip if it's our own collider
            if (hit.collider != null && (hit.collider.transform == transform || hit.collider.transform.IsChildOf(transform)))
            {
                isObstacleDetected = false;
                return;
            }
            
            isObstacleDetected = hit.collider != null;
            
            if (logToConsole && isObstacleDetected && Time.frameCount % 60 == 0)
            {
                Debug.Log($"Obstacle detected: {hit.collider.name}, Layer: {LayerMaskToString(obstacleLayer)}");
            }
        }
        
        public void CheckObstacleDetection()
        {
            if (wallCheck == null) return;
            
            Vector2 direction = isFacingRight ? Vector2.right : Vector2.left;
            float obstacleDetectDist = obstacleDetectionDistance;
            
            // Get all colliders in detection area
            Collider2D[] colliders = Physics2D.OverlapCircleAll(
                wallCheck.position + (Vector3)(direction * (obstacleDetectDist / 2)), 
                obstacleDetectDist / 2
            );
            
            string colliderInfo = "Obstacles in detection area:";
            bool foundObstacle = false;
            
            foreach (var col in colliders)
            {
                bool isOwnCollider = col.transform == transform || col.transform.IsChildOf(transform);
                bool isObstacleLayer = (obstacleLayer.value & (1 << col.gameObject.layer)) != 0;
                
                string status = isOwnCollider ? " (OWN COLLIDER)" : 
                               isObstacleLayer ? " (VALID OBSTACLE)" : " (NOT OBSTACLE LAYER)";
                               
                colliderInfo += $"\n- {col.name}{status} (Layer: {LayerMask.LayerToName(col.gameObject.layer)})";
                
                if (!isOwnCollider && isObstacleLayer && !col.isTrigger)
                {
                    foundObstacle = true;
                }
            }
            
            Debug.Log(colliderInfo);
            Debug.Log($"Obstacle ahead: {foundObstacle}");
            
            // Visual debugging
            Debug.DrawRay(wallCheck.position, direction * obstacleDetectDist, 
                         foundObstacle ? obstacleColor : Color.green);
        }
        
        private string LayerMaskToString(LayerMask mask)
        {
            string result = "";
            for (int i = 0; i < 32; i++)
            {
                if (((1 << i) & mask) != 0)
                {
                    result += (result.Length > 0 ? ", " : "") + LayerMask.LayerToName(i);
                }
            }
            return string.IsNullOrEmpty(result) ? "Nothing" : result + $" ({mask.value})";
        }
        
        private void OnDrawGizmos()
        {
            if (!showDebug || !Application.isPlaying) return;
            
            // Draw recorded debug points
            for (int i = 0; i < debugPoints.Count; i++)
            {
                Gizmos.color = debugColors[i];
                if (debugRadii[i] > 0.01f)
                {
                    Gizmos.DrawWireSphere(debugPoints[i], debugRadii[i]);
                }
                else
                {
                    // Draw a cross for points
                    float size = 0.1f;
                    Vector3 p = debugPoints[i];
                    Gizmos.DrawLine(p + Vector3.left * size, p + Vector3.right * size);
                    Gizmos.DrawLine(p + Vector3.up * size, p + Vector3.down * size);
                }
            }
            
            // Draw ground check connections
            if (groundCheck != null)
            {
                Gizmos.color = isGrounded ? groundedColor : notGroundedColor;
                Gizmos.DrawLine(transform.position, groundCheck.position);
                Gizmos.DrawRay(groundCheck.position, Vector2.down * 0.5f);
            }
            
            // Draw obstacle detection
            if (wallCheck != null)
            {
                Vector2 direction = isFacingRight ? Vector2.right : Vector2.left;
                Gizmos.color = isObstacleDetected ? obstacleColor : Color.green;
                Gizmos.DrawLine(transform.position, wallCheck.position);
                Gizmos.DrawRay(wallCheck.position, direction * obstacleDetectionDistance);
            }
            
            // Draw state text above the enemy
            #if UNITY_EDITOR
            UnityEditor.Handles.Label(transform.position + Vector3.up * 1.5f, statusText);
            #endif
        }
        
        // Method to force a ground check and print detailed information
        public void DebugGroundCheck()
        {
            printGroundObjects = true;
            CheckGrounded();
        }
        
        // Helper method to toggle forced grounding
        public void ToggleForceGrounded()
        {
            forceGrounded = !forceGrounded;
            Debug.Log($"Force grounded: {forceGrounded}");
        }
    }
}