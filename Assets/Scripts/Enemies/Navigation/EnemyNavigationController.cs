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
        [SerializeField] private float jumpForwardSpeed = 2f;      // New tunable forward jump speed
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

        // Enemy behavior interfaces
        private IEnemyActions enemyActions;
        private IEnemyAggro enemyAggro;

        // Navigation flags
        private bool isGrounded;
        private bool isFacingRight; // Will be determined by current scale
        private bool isObstacleAhead;
        private bool isEdgeAhead;
        private bool isLadderDetected;
        private bool isTargetAbove;
        private bool isTargetReachable;
        private bool shouldJump;
        private bool shouldClimb;
        private float lastAggroTime;
        private bool isNavigationPaused = false;

        // Stuck detection
        private float stuckTimer = 0f;
        private Vector2 lastPosition;
        private const float STUCK_THRESHOLD = 1.5f; // Time in seconds to consider enemy stuck
        private const float STUCK_MOVEMENT_THRESHOLD = 0.005f; // Minimum movement to consider not stuck

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            enemyActions = GetComponent<IEnemyActions>();
            enemyAggro = GetComponent<IEnemyAggro>();

            // Initialize facing direction based on current scale
            isFacingRight = transform.localScale.x > 0;
            
            // Initialize components
            obstacleDetector = gameObject.AddComponent<ObstacleDetection>();
            jumpController = gameObject.AddComponent<JumpController>();
            climbController = gameObject.AddComponent<ClimbController>();

            // Find or warn about checks
            if (groundCheck == null)
            {
                groundCheck = transform.Find("GroundCheck");
                if (groundCheck == null)
                    Debug.LogWarning("No GroundCheck transform found, using calculated position");
            }
            if (wallCheck == null)
            {
                wallCheck = transform.Find("WallCheck");
                if (wallCheck == null)
                    Debug.LogWarning("No WallCheck transform found, using calculated position");
            }
            if (ladderCheck == null)
            {
                ladderCheck = transform.Find("LadderCheck");
                if (ladderCheck == null)
                    Debug.LogWarning("No LadderCheck transform found, using calculated position");
            }

            // Exclude enemy hitbox layer from ground checks
            int excludeEnemyLayers = ~(1 << LayerMask.NameToLayer("EnemyHitbox"));
            LayerMask modifiedGroundLayer = groundLayer & excludeEnemyLayers;

            // Initialize detectors
            obstacleDetector.Initialize(
                transform,
                groundCheck,
                wallCheck,
                obstacleDetectionDistance,
                edgeDetectionDistance,
                modifiedGroundLayer,
                obstacleLayer
            );

            // Initialize jump and pass forward speed
            jumpController.Initialize(
                rb,
                animator,
                jumpForce,
                maxJumpDistance,
                jumpForwardSpeed
            );

            climbController.Initialize(
                rb,
                animator,
                ladderCheck,
                climbSpeed,
                ladderLayer,
                ladderCheckRadius
            );

            // Auto-find player target
            if (target == null)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player != null) target = player.transform;
            }
            
            Debug.Log($"Initial facing direction: {(isFacingRight ? "right" : "left")}");
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
            return names.Length > 0 ? names + $" ({mask.value})" : "Nothing";
        }

        /// <summary>
        /// Allows external setup of references and layers.
        /// </summary>
        public void SetupReferences(
            Animator anim,
            Transform ground,
            Transform wall,
            Transform ladder,
            LayerMask groundLayerMask,
            LayerMask obstacleLayerMask,
            LayerMask ladderLayerMask
        )
        {
            animator = anim;
            groundCheck = ground;
            wallCheck = wall;
            ladderCheck = ladder;

            groundLayer = groundLayerMask;
            obstacleLayer = obstacleLayerMask;
            ladderLayer = ladderLayerMask;

            int excludeEnemyLayers = ~(1 << LayerMask.NameToLayer("EnemyHitbox"));
            LayerMask modifiedGroundLayer = groundLayer & excludeEnemyLayers;

            if (obstacleDetector != null)
                obstacleDetector.Initialize(
                    transform,
                    groundCheck,
                    wallCheck,
                    obstacleDetectionDistance,
                    edgeDetectionDistance,
                    modifiedGroundLayer,
                    obstacleLayer
                );

            if (jumpController != null)
                jumpController.Initialize(
                    rb,
                    animator,
                    jumpForce,
                    maxJumpDistance,
                    jumpForwardSpeed
                );

            if (climbController != null)
                climbController.Initialize(
                    rb,
                    animator,
                    ladderCheck,
                    climbSpeed,
                    ladderLayer,
                    ladderCheckRadius
                );
        }

        /// <summary>
        /// Update movement parameters at runtime.
        /// </summary>
        public void SetMovementParameters(
            float move,
            float climb,
            float jump,
            float forwardJump,
            float maxJump
        )
        {
            moveSpeed = move;
            climbSpeed = climb;
            jumpForce = jump;
            jumpForwardSpeed = forwardJump;
            maxJumpDistance = maxJump;

            if (jumpController != null)
                jumpController.Initialize(
                    rb,
                    animator,
                    jumpForce,
                    maxJumpDistance,
                    jumpForwardSpeed
                );
        }

        /// <summary>
        /// Backward-compatible overload.
        /// </summary>
        public void SetMovementParameters(float move, float climb, float jump, float maxJump)
        {
            SetMovementParameters(move, climb, jump, jumpForwardSpeed, maxJump);
        }

        public NavigationState GetCurrentState() => currentState;

        private void Update()
        {
            if (target == null || isNavigationPaused) return;

            // Environment checks
            isGrounded = obstacleDetector.IsGrounded();
            isObstacleAhead = obstacleDetector.IsObstacleAhead(isFacingRight);
            isEdgeAhead = obstacleDetector.IsEdgeAhead(isFacingRight);
            isLadderDetected = climbController.IsLadderDetected();

            // Target analysis
            Vector2 dirVec = (target.position - transform.position).normalized;
            isTargetAbove = target.position.y > transform.position.y + 0.5f;
            var hit = Physics2D.Linecast(transform.position, target.position, obstacleLayer);
            isTargetReachable = hit.collider == null || hit.collider.transform == target;

            // Decisions
            shouldJump = ShouldJump();
            shouldClimb = ShouldClimb();

            UpdateNavigationState();

            // Check if enemy is aggro'd - IMPORTANT: Same logic as in UpdateNavigationState
            bool aggro = false;
            if (enemyAggro != null) aggro = enemyAggro.IsAggroed;
            else if (Vector2.Distance(transform.position, target.position) <= maxTargetDistance && isTargetReachable) 
                aggro = true;
            else if (Time.time - lastAggroTime < aggroCooldown) 
                aggro = true;

            // Only update facing direction if aggro'd and in appropriate states
            if (aggro && currentState != NavigationState.Climbing && currentState != NavigationState.Jumping)
            {
                // Only flip if the enemy needs to move (not just standing near the player)
                if (Mathf.Abs(target.position.x - transform.position.x) > minTargetDistance)
                {
                    if (dirVec.x > 0.1f && !isFacingRight) Flip();
                    else if (dirVec.x < -0.1f && isFacingRight) Flip();
                }
            }

            UpdateAnimations();
            CheckIfStuck();
        }

        private void FixedUpdate()
        {
            if (target == null || isNavigationPaused) return;

            switch (currentState)
            {
                case NavigationState.Idle:
                    rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
                    break;
                case NavigationState.Walking:
                    float dx = target.position.x - transform.position.x;
                    float mDir = Mathf.Sign(dx);
                    if (Mathf.Abs(dx) > minTargetDistance)
                        rb.linearVelocity = new Vector2(mDir * moveSpeed, rb.linearVelocity.y);
                    else
                        rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
                    break;
                case NavigationState.Jumping:
                    jumpController.ExecuteJump();
                    break;
                case NavigationState.Climbing:
                    climbController.ExecuteClimb(target.position.y);
                    break;
                case NavigationState.Falling:
                    float airDir = Mathf.Sign(target.position.x - transform.position.x);
                    rb.linearVelocity = new Vector2(airDir * moveSpeed * 0.8f, rb.linearVelocity.y);
                    break;
                case NavigationState.PathPlanning:
                    rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
                    break;
            }
        }

        /// <summary>
        /// Check if the enemy is stuck and attempt recovery
        /// </summary>
        private void CheckIfStuck()
        {
            // If we're in a state where we should be moving but aren't
            if (currentState == NavigationState.Walking || currentState == NavigationState.Falling)
            {
                // Check if we've moved significantly
                float distance = Vector2.Distance(rb.position, lastPosition);
                
                if (distance < STUCK_MOVEMENT_THRESHOLD) // Not moving much
                {
                    stuckTimer += Time.deltaTime;
                    
                    // If stuck for too long, try to jump
                    if (stuckTimer > STUCK_THRESHOLD)
                    {
                        Debug.Log("Enemy appears stuck - forcing jump to recover");
                        
                        // Force a jump
                        currentState = NavigationState.Jumping;
                        if (jumpController != null)
                        {
                            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
                        }
                        
                        // Reset timer
                        stuckTimer = 0f;
                    }
                }
                else
                {
                    // Reset timer if we're moving
                    stuckTimer = 0f;
                }
            }
            else
            {
                // Reset timer if we're not in a potentially stuck state
                stuckTimer = 0f;
            }
            
            // Update last position
            lastPosition = rb.position;
        }

        /// <summary>
        /// Force a specific navigation state
        /// </summary>
        public void ForceStateChange(NavigationState newState)
        {
            // Only change state if it's different
            if (currentState != newState)
            {
                NavigationState oldState = currentState;
                currentState = newState;
                Debug.Log($"Navigation state FORCED from {oldState} to {newState}");
                
                // No facing direction changes here
                
                // Notify listeners about the state change
                NotifyStateChange(newState);
            }
        }
        
        /// <summary>
        /// Force climbing state
        /// </summary>
        public void ForceClimbingState()
        {
            if (currentState != NavigationState.Climbing)
            {
                currentState = NavigationState.Climbing;
                Debug.Log("Navigation state forced to Climbing");
                NotifyStateChange(NavigationState.Climbing);
            }
        }
        
        /// <summary>
        /// Called when a ladder climb is completed
        /// </summary>
        public void LadderClimbCompleted()
        {
            // Force state to walking if we were climbing
            if (currentState == NavigationState.Climbing)
            {
                currentState = NavigationState.Walking;
                Debug.Log("Ladder climb completed, transitioning to Walking state");
                
                // No facing direction changes here
                
                NotifyStateChange(NavigationState.Walking);
            }
        }

        /// <summary>
        /// Update the navigation state based on current conditions
        /// </summary>
        private void UpdateNavigationState()
        {
            // Store the previous state to detect transitions
            NavigationState previousState = currentState;
            
            float dist = Vector2.Distance(transform.position, target.position);
            bool inRange = dist <= maxTargetDistance;
            bool aggro = false;
            if (enemyAggro != null) aggro = enemyAggro.IsAggroed;
            else if (inRange && isTargetReachable) { aggro = true; lastAggroTime = Time.time; }
            else if (Time.time - lastAggroTime < aggroCooldown) aggro = true;

            if (!aggro)
            {
                currentState = NavigationState.Idle;
                
                // Notify state change if state changed
                if (previousState != currentState) 
                    NotifyStateChange(currentState);
                    
                return;
            }

            // Get current ladder position from climb controller
            float ladderTopY = 0;
            bool isAtLadderTop = false;
            
            if (climbController != null && currentState == NavigationState.Climbing)
            {
                // Use reflection to get private fields from climbController
                var topField = climbController.GetType().GetField("isAtTopOfLadder", 
                                                 System.Reflection.BindingFlags.NonPublic | 
                                                 System.Reflection.BindingFlags.Instance);
                var yField = climbController.GetType().GetField("lastLadderY", 
                                                 System.Reflection.BindingFlags.NonPublic | 
                                                 System.Reflection.BindingFlags.Instance);
                
                if (topField != null)
                    isAtLadderTop = (bool)topField.GetValue(climbController);
                    
                if (yField != null)
                    ladderTopY = (float)yField.GetValue(climbController);
            }

            // Special handling for ladder top transitions
            bool preventStateChange = false;
            
            if (currentState == NavigationState.Climbing && isAtLadderTop)
            {
                // Check if target is above or to the sides
                bool targetIsAbove = target.position.y > transform.position.y + 0.5f;
                bool targetIsNearHorizontally = Mathf.Abs(target.position.x - transform.position.x) < 2f;
                
                if (targetIsAbove || !targetIsNearHorizontally)
                {
                    // Keep climbing if target is above or not directly to the sides
                    preventStateChange = true;
                    Debug.Log("Preventing state change at ladder top - target position requires staying on ladder");
                }
            }

            if (!preventStateChange)
            {
                switch (currentState)
                {
                    case NavigationState.Idle:
                        if (aggro) 
                        {
                            currentState = NavigationState.Walking;
                            // No facing direction changes here
                        }
                        break;
                        
                    case NavigationState.Walking:
                        if (shouldJump)
                        {
                            currentState = NavigationState.Jumping;
                            Debug.Log("Transitioning to jumping state");
                        }
                        else if (shouldClimb)
                            currentState = NavigationState.Climbing;
                        else if (!isGrounded)
                            currentState = NavigationState.Falling;
                        else if (isObstacleAhead)
                        {
                            currentState = NavigationState.PathPlanning;
                            StartCoroutine(ReconsiderPath());
                        }
                        break;
                        
                    case NavigationState.Jumping:
                        if (isGrounded && !jumpController.IsJumping)
                        {
                            currentState = NavigationState.Walking;
                            // No facing direction changes here
                        }
                        else if (!isGrounded && !jumpController.IsJumping) 
                            currentState = NavigationState.Falling;
                        break;
                        
                    case NavigationState.Climbing:
                        // Only leave climbing state if we're not on a ladder AND not at the top
                        if (!isLadderDetected && !climbController.IsLadderDetected() && !isAtLadderTop)
                        {
                            // Check if the target is directly above us
                            bool targetAboveUs = target.position.y > transform.position.y + 1.0f &&
                                                Mathf.Abs(target.position.x - transform.position.x) < 1.0f;
                            
                            // If the target is above us and we're at the top of a ladder,
                            // don't transition away from climbing
                            if (targetAboveUs && isAtLadderTop)
                            {
                                Debug.Log("Target is above us at ladder top, staying in climbing state");
                            }
                            else
                            {
                                if (isGrounded)
                                {
                                    currentState = NavigationState.Walking;
                                    // No facing direction changes here
                                }
                                else
                                    currentState = NavigationState.Falling;
                                    
                                Debug.Log("Transitioning away from climbing state - no ladder detected");
                            }
                        }
                        break;
                        
                    case NavigationState.Falling:
                        if (isGrounded)
                        {
                            currentState = NavigationState.Walking;
                            // No facing direction changes here
                        }
                        else if (shouldClimb) 
                            currentState = NavigationState.Climbing;
                        break;
                    // PathPlanning resumes via coroutine
                }
            }
            
            // Notify controllers about state change
            if (previousState != currentState)
                NotifyStateChange(currentState);
        }
        
        /// <summary>
        /// Notify listeners about state changes
        /// </summary>
        private void NotifyStateChange(NavigationState newState)
        {
            // Notify the ClimbController about state changes
            if (climbController != null)
            {
                climbController.OnNavigationStateChanged(newState);
            }
            
            // Log the state transition
            Debug.Log($"Navigation state changed to: {newState}");
        }

        /// <summary>
        /// Determine if the enemy should jump
        /// </summary>
        private bool ShouldJump()
        {
            if (!isGrounded) return false;
            return isObstacleAhead && jumpController.CanJumpOver(isFacingRight);
        }

        /// <summary>
        /// Determine if the enemy should climb
        /// </summary>
        private bool ShouldClimb()
        {
            // Special case: if we're already climbing and the ClimbController says we're on a ladder, keep climbing
            if (currentState == NavigationState.Climbing && climbController != null && climbController.IsLadderDetected())
            {
                return true;
            }
            
            // Standard ladder detection
            if (!isLadderDetected) return false;
            
            // Climb if target is above
            if (isTargetAbove) return true;
            
            // Climb if we can't jump over an obstacle and target is behind it
            if (isObstacleAhead && !jumpController.CanJumpOver(isFacingRight))
            {
                float obstDir = isFacingRight ? 1 : -1;
                float targDir = target.position.x - transform.position.x;
                if (Mathf.Sign(targDir) == obstDir) return true;
            }
            
            return false;
        }

        /// <summary>
        /// Reconsider the path when an obstacle is encountered
        /// </summary>
        private IEnumerator ReconsiderPath()
        {
            yield return new WaitForSeconds(0.5f);
            Flip();
            currentState = NavigationState.Walking;
            NotifyStateChange(NavigationState.Walking);
        }

        /// <summary>
        /// Flip the enemy's facing direction
        /// </summary>
        private void Flip()
        {
            isFacingRight = !isFacingRight;
            var scale = transform.localScale;
            scale.x *= -1;
            transform.localScale = scale;
            Debug.Log($"Flipped to face {(isFacingRight ? "right" : "left")}");
        }

        /// <summary>
        /// Update animation parameters based on current state
        /// </summary>
        private void UpdateAnimations()
        {
            if (animator == null) return;
            animator.SetBool("isWalking", currentState == NavigationState.Walking);
            animator.SetBool("isJumping",
                currentState == NavigationState.Jumping ||
                (currentState == NavigationState.Falling && rb.linearVelocity.y > 0)
            );
            animator.SetBool("isFalling", currentState == NavigationState.Falling && rb.linearVelocity.y < 0);
            animator.SetBool("isClimbing", currentState == NavigationState.Climbing);
            animator.SetBool("isGrounded", isGrounded);
        }

        /// <summary>
        /// Pause navigation for a specified duration
        /// </summary>
        public void PauseNavigation(float duration)
        {
            StartCoroutine(PauseNavigationCoroutine(duration));
        }

        /// <summary>
        /// Pause navigation coroutine
        /// </summary>
        private IEnumerator PauseNavigationCoroutine(float duration)
        {
            isNavigationPaused = true;
            rb.linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(duration);
            isNavigationPaused = false;
        }

        /// <summary>
        /// Set the target transform
        /// </summary>
        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        /// <summary>
        /// Enable or disable navigation
        /// </summary>
        public void EnableNavigation(bool enable)
        {
            isNavigationPaused = !enable;
            if (!enable) rb.linearVelocity = Vector2.zero;
        }

        /// <summary>
        /// Draw debug information in the editor
        /// </summary>
        private void OnDrawGizmos()
        {
            if (!Application.isPlaying) return;
            #if UNITY_EDITOR
            UnityEditor.Handles.Label(transform.position + Vector3.up * 2, currentState.ToString());
            #endif
            if (target != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(transform.position, target.position);
            }
        }
    }
}