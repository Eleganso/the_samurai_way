using System.Collections;
using UnityEngine;

namespace Enemies.Navigation
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    public class EnemyNavigationController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Animator animator;
        [SerializeField] private Transform groundCheck;
        [SerializeField] private Transform wallCheck;
        [SerializeField] private Transform ladderCheck;
        
        [Header("Target Settings")]
        [SerializeField] private Transform target; // Usually the player
        [SerializeField] private float maxTargetDistance = 15f; // Max distance to follow
        [SerializeField] private float minTargetDistance = 1.5f; // Min distance to maintain

        [Header("Movement Settings")]
        [SerializeField] private float moveSpeed = 3f;
        [SerializeField] private float climbSpeed = 2f;
        [SerializeField] private float jumpForce = 8f;
        [SerializeField] private float maxJumpDistance = 3f;
        [SerializeField] private float aggroCooldown = 3f; // How long to chase after losing sight

        [Header("Ground Detection")]
        [SerializeField] private float groundCheckRadius = 0.2f;
        [SerializeField] private LayerMask groundLayer;
        
        [Header("Obstacle Detection")]
        [SerializeField] private float obstacleDetectionDistance = 1f;
        [SerializeField] private float edgeDetectionDistance = 1f;
        [SerializeField] private LayerMask obstacleLayer;
        
        [Header("Ladder Detection")]
        [SerializeField] private float ladderCheckRadius = 0.5f;
        [SerializeField] private LayerMask ladderLayer;

        // State management
        private NavigationState currentState = NavigationState.Idle;
        private Rigidbody2D rb;
        private ObstacleDetection obstacleDetector;
        private JumpController jumpController;
        private ClimbController climbController;

        // Enemy behavior interface (for enemy-specific behaviors)
        private IEnemyActions enemyActions;
        private IEnemyAggro enemyAggro;

        // Navigation flags
        private bool isGrounded;
        private bool isFacingRight = true;
        private bool isObstacleAhead;
        private bool isEdgeAhead;
        private bool isLadderDetected;
        private bool isTargetAbove;
        private bool isTargetReachable;
        private bool shouldJump;
        private bool shouldClimb;
        private float lastAggroTime;
        private bool isNavigationPaused = false;

        private void Awake()
{
    rb = GetComponent<Rigidbody2D>();
    enemyActions = GetComponent<IEnemyActions>();
    enemyAggro = GetComponent<IEnemyAggro>();

    // Initialize components
    obstacleDetector = gameObject.AddComponent<ObstacleDetection>();
    jumpController = gameObject.AddComponent<JumpController>();
    climbController = gameObject.AddComponent<ClimbController>();

    // Try to find the check transforms if not explicitly assigned
    if (groundCheck == null)
    {
        // Try to find the child transform
        groundCheck = transform.Find("GroundCheck");
        if (groundCheck == null)
        {
            Debug.LogWarning("No GroundCheck transform found, will use calculated position");
        }
        else
        {
            Debug.Log("Found GroundCheck transform as child");
        }
    }

    if (wallCheck == null)
    {
        // Try to find the child transform
        wallCheck = transform.Find("WallCheck");
        if (wallCheck == null)
        {
            Debug.LogWarning("No WallCheck transform found, will use calculated position");
        }
        else
        {
            Debug.Log("Found WallCheck transform as child");
        }
    }

    if (ladderCheck == null)
    {
        // Try to find the child transform
        ladderCheck = transform.Find("LadderCheck");
        if (ladderCheck == null)
        {
            Debug.LogWarning("No LadderCheck transform found, will use calculated position");
        }
        else
        {
            Debug.Log("Found LadderCheck transform as child");
        }
    }

    // Modify ground layer mask to exclude the enemy's own layers
    int excludeEnemyLayers = ~(1 << LayerMask.NameToLayer("EnemyHitbox"));
    LayerMask modifiedGroundLayer = groundLayer & excludeEnemyLayers;
    
    // Log the layer masks for debugging
    Debug.Log($"Original ground layer: {LayerMaskToString(groundLayer)}, " +
              $"Modified ground layer: {LayerMaskToString(modifiedGroundLayer)}");

    // Configure components to use the transforms and modified layer mask
    obstacleDetector.Initialize(transform, groundCheck, wallCheck, 
        obstacleDetectionDistance, edgeDetectionDistance, modifiedGroundLayer, obstacleLayer);
    
    jumpController.Initialize(rb, animator, jumpForce, maxJumpDistance);
    
    climbController.Initialize(rb, animator, ladderCheck, climbSpeed, ladderLayer, ladderCheckRadius);

    // Find player if target not manually assigned
    if (target == null)
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            target = player.transform;
        }
    }
}
// Helper method to convert layer mask to readable string
private string LayerMaskToString(LayerMask mask)
{
    string result = "";
    for (int i = 0; i < 32; i++)
    {
        if (((1 << i) & mask.value) != 0)
        {
            result += (result.Length > 0 ? ", " : "") + LayerMask.LayerToName(i);
        }
    }
    return string.IsNullOrEmpty(result) ? "Nothing" : result + $" ({mask.value})";
}
        // Added method for easier setup from other scripts
        public void SetupReferences(Animator anim, Transform ground, Transform wall, Transform ladder, 
                          LayerMask groundLayerMask, LayerMask obstacleLayerMask, LayerMask ladderLayerMask)
{
    animator = anim;
    groundCheck = ground;
    wallCheck = wall;
    ladderCheck = ladder;
    
    // Store the original masks
    groundLayer = groundLayerMask;
    obstacleLayer = obstacleLayerMask;
    ladderLayer = ladderLayerMask;
    
    // Modify ground layer mask to exclude the enemy's own layers
    int excludeEnemyLayers = ~(1 << LayerMask.NameToLayer("EnemyHitbox"));
    LayerMask modifiedGroundLayer = groundLayerMask & excludeEnemyLayers;
    
    // Re-initialize components with new references
    if (obstacleDetector != null)
    {
        obstacleDetector.Initialize(transform, groundCheck, wallCheck, 
            obstacleDetectionDistance, edgeDetectionDistance, modifiedGroundLayer, obstacleLayer);
    }
    
    if (jumpController != null)
    {
        jumpController.Initialize(rb, animator, jumpForce, maxJumpDistance);
    }
    
    if (climbController != null)
    {
        climbController.Initialize(rb, animator, ladderCheck, climbSpeed, ladderLayer, ladderCheckRadius);
    }
}

        // Additional method to set movement parameters
        public void SetMovementParameters(float move, float climb, float jump, float maxJump)
        {
            moveSpeed = move;
            climbSpeed = climb;
            jumpForce = jump;
            maxJumpDistance = maxJump;
            
            if (jumpController != null)
            {
                jumpController.Initialize(rb, animator, jumpForce, maxJumpDistance);
            }
        }

        // Method to get current navigation state
        public NavigationState GetCurrentState()
        {
            return currentState;
        }

        private void Update()
        {
            if (target == null || isNavigationPaused) return;

            // Update environment detection
            isGrounded = obstacleDetector.IsGrounded();
            isObstacleAhead = obstacleDetector.IsObstacleAhead(isFacingRight);
            isEdgeAhead = obstacleDetector.IsEdgeAhead(isFacingRight);
            isLadderDetected = climbController.IsLadderDetected();

            // Analyze target position
            Vector2 targetDirection = (target.position - transform.position).normalized;
            float distanceToTarget = Vector2.Distance(transform.position, target.position);
            isTargetAbove = target.position.y > transform.position.y + 0.5f;
            
            // Determine if target is currently reachable (line of sight)
            RaycastHit2D hit = Physics2D.Linecast(transform.position, target.position, obstacleLayer);
            isTargetReachable = hit.collider == null || hit.collider.transform == target;

            // Determine navigation decisions
            shouldJump = ShouldJump();
            shouldClimb = ShouldClimb();

            // State machine transitions
            UpdateNavigationState();

            // Direction facing logic
            if (currentState != NavigationState.Climbing && currentState != NavigationState.Jumping)
            {
                // Determine which way to face based on target direction
                if (targetDirection.x > 0.1f && !isFacingRight)
                    Flip();
                else if (targetDirection.x < -0.1f && isFacingRight)
                    Flip();
            }

            // Update animation states
            UpdateAnimations();
        }

        private void FixedUpdate()
        {
            if (target == null || isNavigationPaused) return;
            
            switch (currentState)
            {
                case NavigationState.Idle:
                    // Do nothing, stay in place
                    rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
                    break;

                case NavigationState.Walking:
                    // Get direction to target
                    float targetXDirection = target.position.x - transform.position.x;
                    float moveDirection = Mathf.Sign(targetXDirection);

                    // Move towards target
                    float distance = Mathf.Abs(targetXDirection);
                    if (distance > minTargetDistance)
                    {
                        rb.linearVelocity = new Vector2(moveDirection * moveSpeed, rb.linearVelocity.y);
                    }
                    else
                    {
                        rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
                    }
                    break;

                case NavigationState.Jumping:
                    jumpController.ExecuteJump();
                    break;

                case NavigationState.Climbing:
                    climbController.ExecuteClimb(target.position.y);
                    break;

                case NavigationState.Falling:
                    // Apply movement in air, but allow gravity to do its work
                    float airMoveDirection = target.position.x - transform.position.x;
                    rb.linearVelocity = new Vector2(Mathf.Sign(airMoveDirection) * moveSpeed * 0.8f, rb.linearVelocity.y);
                    break;

                case NavigationState.PathPlanning:
                    // Currently just pausing to reconsider path
                    rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
                    break;
            }
        }

        private void UpdateNavigationState()
        {
            // Check if target is within max follow distance
            float distanceToTarget = Vector2.Distance(transform.position, target.position);
            bool isTargetInRange = distanceToTarget <= maxTargetDistance;

            // Aggro logic (uses IEnemyAggro interface if available)
            bool isAggro = false;
            if (enemyAggro != null)
            {
                isAggro = enemyAggro.IsAggroed;
            }
            else
            {
                // Simple fallback aggro logic if no IEnemyAggro component
                if (isTargetInRange && isTargetReachable)
                {
                    isAggro = true;
                    lastAggroTime = Time.time;
                }
                else if (Time.time - lastAggroTime < aggroCooldown)
                {
                    isAggro = true;
                }
            }

            // If not aggro'd, go to idle
            if (!isAggro)
            {
                currentState = NavigationState.Idle;
                return;
            }

            // Transition logic based on current state and environment
            switch (currentState)
            {
                case NavigationState.Idle:
                    if (isAggro)
                    {
                        currentState = NavigationState.Walking;
                    }
                    break;

                case NavigationState.Walking:
                    if (shouldJump)
                    {
                        currentState = NavigationState.Jumping;
                    }
                    else if (shouldClimb)
                    {
                        currentState = NavigationState.Climbing;
                    }
                    else if (!isGrounded)
                    {
                        currentState = NavigationState.Falling;
                    }
                    else if (isObstacleAhead && !shouldJump && !shouldClimb)
                    {
                        currentState = NavigationState.PathPlanning;
                        StartCoroutine(ReconsiderPath());
                    }
                    break;

                case NavigationState.Jumping:
                    if (isGrounded && !jumpController.IsJumping)
                    {
                        currentState = NavigationState.Walking;
                    }
                    else if (!isGrounded && !jumpController.IsJumping)
                    {
                        currentState = NavigationState.Falling;
                    }
                    break;

                case NavigationState.Climbing:
                    if (!isLadderDetected || !isTargetAbove)
                    {
                        currentState = NavigationState.Walking;
                    }
                    break;

                case NavigationState.Falling:
                    if (isGrounded)
                    {
                        currentState = NavigationState.Walking;
                    }
                    else if (shouldClimb)
                    {
                        currentState = NavigationState.Climbing;
                    }
                    break;

                case NavigationState.PathPlanning:
                    // State changed by coroutine
                    break;
            }
        }

        private bool ShouldJump()
{
    // Only consider jumping if grounded - check with less frequent logging
    if (!isGrounded) 
    {
        if (Time.frameCount % 60 == 0) // Only log once every 60 frames to reduce spam
        {
            Debug.Log("Not jumping: Not grounded");
        }
        return false;
    }

    // Jump if there's an obstacle ahead but we can jump over it
    if (isObstacleAhead && jumpController.CanJumpOver(isFacingRight))
    {
        Debug.Log("Should jump: Obstacle ahead that we can jump over");
        return true;
    }

    // Jump if there's a gap ahead that we can jump across
    if (isEdgeAhead && jumpController.CanJumpAcross(isFacingRight))
    {
        Debug.Log("Should jump: Edge ahead that we can jump across");
        return true;
    }

    // Jump if target is above and no ladder is available
    if (isTargetAbove && !isLadderDetected && Mathf.Abs(target.position.x - transform.position.x) < 1.5f)
    {
        Debug.Log("Should jump: Target is above us");
        return true;
    }

    return false;
}

        private bool ShouldClimb()
        {
            // Only consider climbing if a ladder is detected
            if (!isLadderDetected) return false;

            // Climb if target is above us
            if (isTargetAbove)
            {
                return true;
            }

            // Climb if there's an obstacle ahead that we can't jump over
            if (isObstacleAhead && !jumpController.CanJumpOver(isFacingRight))
            {
                // Check if target is on same side of obstacle
                float obstacleDirection = isFacingRight ? 1 : -1;
                float targetDirection = target.position.x - transform.position.x;
                
                if (Mathf.Sign(targetDirection) == obstacleDirection)
                {
                    return true;
                }
            }

            return false;
        }

        private IEnumerator ReconsiderPath()
        {
            // Wait a moment to reconsider path
            yield return new WaitForSeconds(0.5f);

            // Simple path reconsideration: just turn around if stuck
            Flip();
            
            // Resume walking
            currentState = NavigationState.Walking;
        }

        private void Flip()
        {
            isFacingRight = !isFacingRight;
            Vector3 scale = transform.localScale;
            scale.x *= -1;
            transform.localScale = scale;
        }

        private void UpdateAnimations()
        {
            if (animator == null) return;

            // Update animation parameters based on state
            animator.SetBool("isWalking", currentState == NavigationState.Walking);
            animator.SetBool("isJumping", currentState == NavigationState.Jumping || 
                                         (currentState == NavigationState.Falling && rb.linearVelocity.y > 0));
            animator.SetBool("isFalling", currentState == NavigationState.Falling && rb.linearVelocity.y < 0);
            animator.SetBool("isClimbing", currentState == NavigationState.Climbing);
            animator.SetBool("isGrounded", isGrounded);
        }

        // Method to pause navigation (for attack animations, taking damage, etc.)
        public void PauseNavigation(float duration)
        {
            StartCoroutine(PauseNavigationCoroutine(duration));
        }

        private IEnumerator PauseNavigationCoroutine(float duration)
        {
            isNavigationPaused = true;
            rb.linearVelocity = Vector2.zero; // Stop movement
            
            yield return new WaitForSeconds(duration);
            
            isNavigationPaused = false;
        }

        // Method to override the target
        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        // Method to enable/disable navigation
        public void EnableNavigation(bool enable)
        {
            isNavigationPaused = !enable;
            if (!enable)
            {
                rb.linearVelocity = Vector2.zero;
            }
        }

        // Optional: Visualization for debugging
        private void OnDrawGizmos()
        {
            if (!Application.isPlaying) return;

            // Draw current state as text
            #if UNITY_EDITOR
            UnityEditor.Handles.Label(transform.position + Vector3.up * 2, currentState.ToString());
            #endif
            
            // Draw target connection
            if (target != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(transform.position, target.position);
            }
        }
    }
}