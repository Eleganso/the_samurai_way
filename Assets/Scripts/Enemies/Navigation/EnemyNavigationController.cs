using System.Collections;
using System.Collections.Generic;
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
        [SerializeField] private float jumpForwardSpeed = 2f;      // Forward jump speed
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

        [Header("Enhanced Navigation")]
        [SerializeField] private bool useEnhancedNavigation = true;
        [SerializeField] private float waypointDetectionRadius = 10f;
        [SerializeField] private float alternateRoutePreference = 0.8f; // Lower values prefer alternate routes more
        [SerializeField] private float intermediateTargetReachedDistance = 1f;
        [SerializeField] private float pathRecalculationInterval = 1.5f;
        [SerializeField] private bool visualizeEnhancedPaths = true;

        [Header("Layer Avoidance")]
        [SerializeField] private LayerMask avoidedLayers; // Layers to always avoid when possible
        [SerializeField] private bool alwaysUseAlternateForAvoidedLayers = true; // Always prefer waypoints when path crosses avoided layers
        [SerializeField] private float avoidedLayerCheckFrequency = 0.5f; // How often to check for avoided layers (in seconds)
        [SerializeField] private bool debugAvoidanceDecisions = false; // Log path avoidance decisions for debugging

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

        // Advanced navigation
        private WaypointSystem waypointSystem;
        private bool isUsingAlternatePath = false;
        private List<Vector2> alternatePath = new List<Vector2>();
        private int currentPathIndex = 0;
        private Transform intermediateTarget = null;
        private float lastPathCalculationTime = 0f;

        // Layer avoidance state
        private bool isDirectPathThroughAvoidedLayer = false;
        private float lastAvoidanceCheckTime = 0f;

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
            
            // Get waypoint system if enhanced navigation is enabled
            if (useEnhancedNavigation)
            {
                waypointSystem = FindObjectOfType<WaypointSystem>();
                if (waypointSystem == null)
                {
                    Debug.LogWarning("WaypointSystem not found. Enhanced navigation will be limited.");
                }
            }
            
            // Set avoided layers in the obstacle detector if available
            var avoidedLayersField = obstacleDetector.GetType().GetField("avoidedLayers", 
                System.Reflection.BindingFlags.NonPublic | 
                System.Reflection.BindingFlags.Public | 
                System.Reflection.BindingFlags.Instance);
                
            if (avoidedLayersField != null)
            {
                avoidedLayersField.SetValue(obstacleDetector, avoidedLayers);
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
            // Update isFacingRight based on current scale for obstacle detection
            isFacingRight = transform.localScale.x > 0;
            isObstacleAhead = obstacleDetector.IsObstacleAhead(isFacingRight);
            isEdgeAhead = obstacleDetector.IsEdgeAhead(isFacingRight);
            isLadderDetected = climbController.IsLadderDetected();

            // Target analysis
            Vector2 dirVec = (target.position - transform.position).normalized;
            isTargetAbove = target.position.y > transform.position.y + 0.5f;
            var hit = Physics2D.Linecast(transform.position, target.position, obstacleLayer);
            isTargetReachable = hit.collider == null || hit.collider.transform == target;

            // Check for layer avoidance (at a lower frequency than the normal update)
            if (avoidedLayers.value != 0 && Time.time - lastAvoidanceCheckTime >= avoidedLayerCheckFrequency)
            {
                lastAvoidanceCheckTime = Time.time;
                isDirectPathThroughAvoidedLayer = IsDirectPathThroughAvoidedLayer();
                
                // If direct path crosses avoided layers and we should use alternate paths
                if (isDirectPathThroughAvoidedLayer && alwaysUseAlternateForAvoidedLayers)
                {
                    // Force evaluation of alternate path
                    EvaluateAndSetAlternatePath();
                }
            }

            // Enhanced navigation - handle intermediate targets
            if (useEnhancedNavigation && intermediateTarget != null)
            {
                // Check if we've reached the intermediate target
                float distToIntermediate = Vector2.Distance(transform.position, intermediateTarget.position);
                if (distToIntermediate <= intermediateTargetReachedDistance)
                {
                    // If using alternate path, advance to next waypoint
                    if (isUsingAlternatePath && currentPathIndex < alternatePath.Count - 1)
                    {
                        AdvanceToNextWaypoint();
                    }
                    else
                    {
                        ClearIntermediateTarget();
                    }
                }
            }

            // Enhanced navigation - recalculate path periodically
            if (useEnhancedNavigation && waypointSystem != null)
            {
                if (Time.time - lastPathCalculationTime >= pathRecalculationInterval)
                {
                    lastPathCalculationTime = Time.time;
                    
                    // Only recalculate when aggroed and target is unreachable, at different height, 
                    // or path crosses avoided layers
                    bool aggro = enemyAggro != null ? enemyAggro.IsAggroed : false;
                    if (aggro && (!isTargetReachable || isTargetAbove || isDirectPathThroughAvoidedLayer))
                    {
                        EvaluateAndSetAlternatePath();
                    }
                    else if (!isDirectPathThroughAvoidedLayer)
                    {
                        // Only clear if direct path doesn't cross avoided layers
                        ClearAlternatePath();
                    }
                }
            }

            // Decisions for standard navigation
            shouldJump = ShouldJump();
            shouldClimb = ShouldClimb();

            UpdateNavigationState();
            
            // The ElfSwordsman class will handle all facing direction changes
            
            UpdateAnimations();
            CheckIfStuck();
        }

        private void FixedUpdate()
        {
            if (target == null || isNavigationPaused) return;

            // When using an intermediate target, adjust the target reference for movement
            Transform effectiveTarget = (intermediateTarget != null) ? intermediateTarget : target;

            switch (currentState)
            {
                case NavigationState.Idle:
                    rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
                    break;
                case NavigationState.Walking:
                    float dx = effectiveTarget.position.x - transform.position.x;
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
                    climbController.ExecuteClimb(effectiveTarget.position.y);
                    break;
                case NavigationState.Falling:
                    float airDir = Mathf.Sign(effectiveTarget.position.x - transform.position.x);
                    rb.linearVelocity = new Vector2(airDir * moveSpeed * 0.8f, rb.linearVelocity.y);
                    break;
                case NavigationState.PathPlanning:
                    rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
                    break;
            }
        }

        /// <summary>
        /// Check if the direct path to the target crosses any layers that should be avoided
        /// </summary>
        private bool IsDirectPathThroughAvoidedLayer()
        {
            if (avoidedLayers.value == 0 || target == null)
                return false;
                
            // Simple linecast to check if path crosses avoided layers
            RaycastHit2D hit = Physics2D.Linecast(transform.position, target.position, avoidedLayers);
            
            // If we hit something that isn't the target itself, the path crosses an avoided layer
            bool isAvoided = hit.collider != null && hit.collider.transform != target;
            
            if (isAvoided && debugAvoidanceDecisions)
            {
                Debug.Log($"Avoiding path through layer: {LayerMask.LayerToName(hit.collider.gameObject.layer)}");
                Debug.DrawLine(transform.position, hit.point, Color.magenta, 0.5f);
            }
            
            return isAvoided;
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
    
    // Increase this distance check to detect ladders from further away
    float ladderDetectionRadius = waypointDetectionRadius * 0.5f; // Use half the waypoint detection radius
    
    // Check if any ladder waypoints are within detection radius
    bool ladderPathAvailable = false;
    if (waypointSystem != null)
    {
        // Try to find a ladder waypoint within the detection radius
        var method = waypointSystem.GetType().GetMethod("FindWaypointsInRadius", 
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            
        if (method != null)
        {
            // Search for LadderBottom waypoints
            var ladderBottoms = method.Invoke(waypointSystem, 
                new object[] { (Vector2)transform.position, ladderDetectionRadius, WaypointType.LadderBottom });
                
            if (ladderBottoms is IList ladderList && ladderList.Count > 0)
            {
                ladderPathAvailable = true;
                if (debugAvoidanceDecisions)
                    Debug.Log($"Found ladder waypoint within {ladderDetectionRadius} units");
            }
        }
    }
    
    // Extended ladder detection - consider ladders from further away
    if (ladderPathAvailable || isLadderDetected) 
    {
        // When using an intermediate target, adjust the target reference for decisions
        Transform effectiveTarget = (intermediateTarget != null) ? intermediateTarget : target;
        
        // Climb if target is above or if trying to reach intermediate waypoint via ladder path
        if (effectiveTarget.position.y > transform.position.y + 0.5f) 
        {
            if (debugAvoidanceDecisions)
                Debug.Log("Choosing to climb because target is above");
            return true;
        }
        
        // Climb if we can't jump over an obstacle and target is behind it
        if (isObstacleAhead && !jumpController.CanJumpOver(isFacingRight))
        {
            float obstDir = isFacingRight ? 1 : -1;
            float targDir = effectiveTarget.position.x - transform.position.x;
            if (Mathf.Sign(targDir) == obstDir) 
            {
                if (debugAvoidanceDecisions)
                    Debug.Log("Choosing to climb to get past obstacle");
                return true;
            }
        }
    }
    
    // Standard ladder detection
    if (!isLadderDetected) return false;
    
    // Original remaining logic
    Transform finalTarget = (intermediateTarget != null) ? intermediateTarget : target;
    if (finalTarget.position.y > transform.position.y + 0.5f) return true;
    
    // Climb if we can't jump over an obstacle and target is behind it
    if (isObstacleAhead && !jumpController.CanJumpOver(isFacingRight))
    {
        float obstDir = isFacingRight ? 1 : -1;
        float targDir = finalTarget.position.x - transform.position.x;
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
            
            // Enhanced navigation - try to find an alternate path
            if (useEnhancedNavigation && waypointSystem != null)
            {
                EvaluateAndSetAlternatePath();
            }
            
            // Let the ElfSwordsman handle flipping, don't flip here
            // Flip();
            currentState = NavigationState.Walking;
            NotifyStateChange(NavigationState.Walking);
        }

        /// <summary>
        /// Flip the enemy's facing direction
        /// </summary>
        public void Flip()
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
        /// Evaluate and set an alternate path to the target using waypoints
        /// </summary>
        private void EvaluateAndSetAlternatePath()
        {
            if (waypointSystem == null || target == null)
                return;

            // Check if direct path is blocked, vertical difference is significant,
            // or the path crosses avoided layers
            bool isPathDifficult = !isTargetReachable || 
                                  Mathf.Abs(target.position.y - transform.position.y) > 1.5f || 
                                  isDirectPathThroughAvoidedLayer;
            
            if (isPathDifficult)
            {
                // Find a path using waypoints
                List<Vector2> path = waypointSystem.FindPathFromPositions(
                    transform.position, 
                    target.position, 
                    waypointDetectionRadius
                );
                
                // If we found a valid path with at least one waypoint
                if (path.Count > 2) // Start, at least one waypoint, and end
                {
                    // Validate that the path doesn't cross avoided layers
                    bool pathValid = true;
                    
                    if (avoidedLayers.value != 0)
                    {
                        for (int i = 0; i < path.Count - 1; i++)
                        {
                            RaycastHit2D hit = Physics2D.Linecast(path[i], path[i+1], avoidedLayers);
                            if (hit.collider != null)
                            {
                                pathValid = false;
                                if (debugAvoidanceDecisions)
                                    Debug.Log($"Waypoint path segment {i} crosses avoided layer: {LayerMask.LayerToName(hit.collider.gameObject.layer)}");
                                break;
                            }
                        }
                    }
                    
                    if (pathValid)
                    {
                        alternatePath = path;
                        currentPathIndex = 1; // Index 0 is our starting position
                        isUsingAlternatePath = true;
                        
                        // Set the first waypoint as an intermediate target
                        SetIntermediateTarget(alternatePath[currentPathIndex]);
                        
                        if (debugAvoidanceDecisions)
                            Debug.Log($"Set alternate path with {alternatePath.Count - 2} waypoints to avoid obstacles/layers");
                        return;
                    }
                    else if (debugAvoidanceDecisions)
                    {
                        Debug.Log("Alternate path also crosses avoided layers - using direct path");
                    }
                }
            }
            
            // If no alternate path was found or needed, clear any existing one
            ClearAlternatePath();
        }
        
        /// <summary>
        /// Clear the alternate path and reset to direct targeting
        /// </summary>
        private void ClearAlternatePath()
        {
            // Don't clear the path if we're still avoiding layers
            if (isDirectPathThroughAvoidedLayer && alwaysUseAlternateForAvoidedLayers && isUsingAlternatePath)
            {
                if (debugAvoidanceDecisions)
                    Debug.Log("Not clearing alternate path because direct path crosses avoided layers");
                return;
            }
            
            isUsingAlternatePath = false;
            alternatePath.Clear();
            currentPathIndex = 0;
            ClearIntermediateTarget();
        }
        
        /// <summary>
        /// Set an intermediate target at the specified position
        /// </summary>
        private void SetIntermediateTarget(Vector2 position)
        {
            // Clean up any existing intermediate target
            ClearIntermediateTarget();
            
            // Create a new GameObject for the intermediate target
            GameObject targetObj = new GameObject("IntermediateTarget");
            targetObj.transform.position = position;
            intermediateTarget = targetObj.transform;
            
            Debug.Log($"Set intermediate target at {position}");
        }
        
        /// <summary>
        /// Clear the current intermediate target
        /// </summary>
        private void ClearIntermediateTarget()
        {
            if (intermediateTarget != null)
            {
                Destroy(intermediateTarget.gameObject);
                intermediateTarget = null;
            }
        }
        
        /// <summary>
        /// Advance to the next waypoint in the path
        /// </summary>
        private void AdvanceToNextWaypoint()
{
    if (!isUsingAlternatePath || alternatePath.Count <= 2 || currentPathIndex >= alternatePath.Count - 1)
    {
        ClearAlternatePath();
        return;
    }
    
    // Store the current waypoint information before moving to the next
    Vector2 currentWaypointPos = alternatePath[currentPathIndex];
    Vector2 nextWaypointPos = alternatePath[currentPathIndex + 1];
    
    // Check if we're at an EdgeTop waypoint and next is EdgeBottom
    bool isJumpDownTransition = false;
    
    // Find the waypoints to check their types
    Waypoint currentWaypoint = FindWaypointAtPosition(currentWaypointPos);
    Waypoint nextWaypoint = FindWaypointAtPosition(nextWaypointPos);
    
    if (currentWaypoint != null && nextWaypoint != null)
    {
        if (currentWaypoint.Type == WaypointType.EdgeTop && nextWaypoint.Type == WaypointType.EdgeBottom)
        {
            isJumpDownTransition = true;
            
            // Special handling for jumping/falling down
            currentState = NavigationState.Falling;
            
            // Log the transition
            if (debugAvoidanceDecisions)
                Debug.Log($"EdgeTop to EdgeBottom transition detected. Forcing fall state.");
                
            // Notify about the state change
            NotifyStateChange(NavigationState.Falling);
                
            // Ensure the enemy doesn't stop at the edge by setting velocity directly
            if (rb != null)
            {
                // Set a small horizontal velocity in the direction of the edge
                float directionX = Mathf.Sign(nextWaypointPos.x - currentWaypointPos.x);
                float fallVelocity = directionX == 0 ? 0 : directionX * moveSpeed * 0.5f;
                
                // Apply fall velocity - keep y component to allow gravity to work
                rb.linearVelocity = new Vector2(fallVelocity, rb.linearVelocity.y);
            }
        }
    }
    
    // Advance to the next waypoint
    currentPathIndex++;
    SetIntermediateTarget(alternatePath[currentPathIndex]);
    
    if (debugAvoidanceDecisions)
        Debug.Log($"Advanced to waypoint {currentPathIndex} of {alternatePath.Count-1}" + 
                  (isJumpDownTransition ? " (Jump Down)" : ""));
}

/// <summary>
/// Helper method to find a waypoint at a specific position
/// </summary>
private Waypoint FindWaypointAtPosition(Vector2 position)
{
    if (waypointSystem == null)
        return null;
        
    // Use reflection to access the FindNearestWaypoint method
    var method = waypointSystem.GetType().GetMethod("FindNearestWaypoint", 
        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance,
        null,
        new System.Type[] { typeof(Vector2) },
        null);
        
    if (method != null)
    {
        // The tolerance for considering a waypoint to be at the position
        const float positionTolerance = 0.1f;
        
        // Call the method to find the nearest waypoint
        Waypoint nearestWaypoint = method.Invoke(waypointSystem, new object[] { position }) as Waypoint;
        
        // Check if the waypoint is close enough to the position
        if (nearestWaypoint != null && 
            Vector2.Distance(position, (Vector2)nearestWaypoint.transform.position) < positionTolerance)
        {
            return nearestWaypoint;
        }
    }
    
    return null;
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
                // Draw line to target
                if (isDirectPathThroughAvoidedLayer)
                {
                    // Draw path in red/magenta if it crosses avoided layers
                    Gizmos.color = new Color(1f, 0f, 1f, 0.8f); // Magenta
                    Gizmos.DrawLine(transform.position, target.position);
                    
                    #if UNITY_EDITOR
                    Vector3 midPoint = (transform.position + target.position) * 0.5f;
                    UnityEditor.Handles.Label(midPoint, "Avoided Layer");
                    #endif
                }
                else
                {
                    // Standard path color based on reachability
                    Gizmos.color = isTargetReachable ? Color.green : Color.yellow;
                    Gizmos.DrawLine(transform.position, target.position);
                }
            }
            
            // Draw enhanced navigation debug info
            if (visualizeEnhancedPaths && useEnhancedNavigation)
            {
                // Draw intermediate target
                if (intermediateTarget != null)
                {
                    Gizmos.color = Color.magenta;
                    Gizmos.DrawWireSphere(intermediateTarget.position, intermediateTargetReachedDistance);
                    Gizmos.DrawLine(transform.position, intermediateTarget.position);
                    
                    #if UNITY_EDITOR
                    UnityEditor.Handles.Label(intermediateTarget.position + Vector3.up * 0.5f, "Intermediate Target");
                    #endif
                }
                
                // Draw alternate path
                if (isUsingAlternatePath && alternatePath.Count > 1)
                {
                    // Use a brighter color if we're using the path to avoid layers
                    Gizmos.color = isDirectPathThroughAvoidedLayer ? Color.green : Color.cyan;
                    
                    for (int i = 0; i < alternatePath.Count - 1; i++)
                    {
                        Gizmos.DrawLine(alternatePath[i], alternatePath[i + 1]);
                        Gizmos.DrawWireSphere(alternatePath[i], 0.2f);
                    }
                    Gizmos.DrawWireSphere(alternatePath[alternatePath.Count - 1], 0.2f);
                    
                    // Highlight current waypoint
                    if (currentPathIndex < alternatePath.Count)
                    {
                        Gizmos.color = Color.yellow;
                        Gizmos.DrawWireSphere(alternatePath[currentPathIndex], 0.3f);
                    }
                    
                    #if UNITY_EDITOR
                    string pathReason = isDirectPathThroughAvoidedLayer ? 
                        "Avoiding Layer" : "Using Alternate Path";
                    UnityEditor.Handles.Label(transform.position + Vector3.up * 1.5f, pathReason);
                    #endif
                }
                
                // Draw avoided layer visualization
                if (avoidedLayers.value != 0 && debugAvoidanceDecisions)
                {
                    // Highlight any nearby avoided layers
                    Collider2D[] avoidedColliders = Physics2D.OverlapCircleAll(
                        transform.position, 
                        waypointDetectionRadius,
                        avoidedLayers
                    );
                    
                    foreach (Collider2D coll in avoidedColliders)
                    {
                        Gizmos.color = new Color(1f, 0f, 1f, 0.3f); // Semi-transparent magenta
                        
                        if (coll is BoxCollider2D box)
                        {
                            // Draw box outline
                            Gizmos.DrawWireCube(box.bounds.center, box.bounds.size);
                        }
                        else
                        {
                            // Draw generic outline for other collider types
                            Gizmos.DrawWireSphere(coll.bounds.center, 
                                                 Mathf.Max(coll.bounds.extents.x, coll.bounds.extents.y));
                        }
                        
                        #if UNITY_EDITOR
                        UnityEditor.Handles.Label(coll.bounds.center, 
                            $"Layer: {LayerMask.LayerToName(coll.gameObject.layer)}");
                        #endif
                    }
                }
            }
        }
    }
}