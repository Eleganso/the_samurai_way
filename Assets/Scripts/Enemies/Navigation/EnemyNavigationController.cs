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

            // Facing logic
            if (currentState != NavigationState.Climbing && currentState != NavigationState.Jumping)
            {
                if (dirVec.x > 0.1f && !isFacingRight) Flip();
                else if (dirVec.x < -0.1f && isFacingRight) Flip();
            }

            UpdateAnimations();
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

        private void UpdateNavigationState()
        {
            float dist = Vector2.Distance(transform.position, target.position);
            bool inRange = dist <= maxTargetDistance;
            bool aggro = false;
            if (enemyAggro != null) aggro = enemyAggro.IsAggroed;
            else if (inRange && isTargetReachable) { aggro = true; lastAggroTime = Time.time; }
            else if (Time.time - lastAggroTime < aggroCooldown) aggro = true;

            if (!aggro)
            {
                currentState = NavigationState.Idle;
                return;
            }

            switch (currentState)
            {
                case NavigationState.Idle:
                    if (aggro) currentState = NavigationState.Walking;
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
                    if (isGrounded && !jumpController.IsJumping) currentState = NavigationState.Walking;
                    else if (!isGrounded && !jumpController.IsJumping) currentState = NavigationState.Falling;
                    break;
                case NavigationState.Climbing:
                    if (!isLadderDetected || !isTargetAbove) currentState = NavigationState.Walking;
                    break;
                case NavigationState.Falling:
                    if (isGrounded) currentState = NavigationState.Walking;
                    else if (shouldClimb) currentState = NavigationState.Climbing;
                    break;
                // PathPlanning resumes via coroutine
            }
        }

        private bool ShouldJump()
        {
            if (!isGrounded) return false;
            return isObstacleAhead && jumpController.CanJumpOver(isFacingRight);
        }

        private bool ShouldClimb()
        {
            if (!isLadderDetected) return false;
            if (isTargetAbove) return true;
            if (isObstacleAhead && !jumpController.CanJumpOver(isFacingRight))
            {
                float obstDir = isFacingRight ? 1 : -1;
                float targDir = target.position.x - transform.position.x;
                if (Mathf.Sign(targDir) == obstDir) return true;
            }
            return false;
        }

        private IEnumerator ReconsiderPath()
        {
            yield return new WaitForSeconds(0.5f);
            Flip();
            currentState = NavigationState.Walking;
        }

        private void Flip()
        {
            isFacingRight = !isFacingRight;
            var scale = transform.localScale;
            scale.x *= -1;
            transform.localScale = scale;
        }

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

        public void PauseNavigation(float duration)
        {
            StartCoroutine(PauseNavigationCoroutine(duration));
        }

        private IEnumerator PauseNavigationCoroutine(float duration)
        {
            isNavigationPaused = true;
            rb.linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(duration);
            isNavigationPaused = false;
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        public void EnableNavigation(bool enable)
        {
            isNavigationPaused = !enable;
            if (!enable) rb.linearVelocity = Vector2.zero;
        }

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
