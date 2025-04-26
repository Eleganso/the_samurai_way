using UnityEngine;
using System.Collections;

namespace Enemies.Navigation
{
    public class ClimbController : MonoBehaviour
    {
        private Rigidbody2D rb;
        private Animator animator;
        private Transform ladderCheckTransform;
        private float climbSpeed;
        private LayerMask ladderLayer;
        private float ladderCheckRadius;

        private bool isClimbing = false;
        private bool wasOnLadder = false;
        private float originalGravity;
        private bool isAtTopOfLadder = false;
        private float lastLadderY = 0f;
        private float topLadderBuffer = 0.5f; // Buffer zone at the top of ladder

        // Grace-period support for forgiving ladder detection
        [SerializeField] private float ladderGracePeriod = 0.2f;    // seconds to remain "on ladder" after losing overlap
        private float lastLadderContactTime = 0f;

        // Add target reference to find player
        private Transform target;

        [SerializeField] private float gravityResetDelay = 2f;        // Delay before resetting gravity
        [SerializeField] private float topLadderSafetyMargin = 0.5f; // Extend detection at top
        [SerializeField] private float climbCompletionThreshold = 2.0f; // Height above ladder to finish

        [Header("Ladder Exit Jump Settings")]
        [SerializeField] private float exitJumpForce = 8f;           // Jump force when exiting ladder
        [SerializeField] private float exitHorizontalForce = 3f;     // Horizontal push when exiting ladder
        
        private Coroutine gravityResetCoroutine;
        private Collider2D currentLadderCollider;
        private float ladderTopPosition = 0f;
        private float timeAtLadderTop = 0f;
        
        // Keep for potential future use
        private Vector3 lastPosition;

        // Debug/visualization
        [SerializeField] private bool debugLadderDetection = true;

        public void Initialize(Rigidbody2D rigidbody, Animator anim, Transform ladderCheck,
                              float speed, LayerMask ladders, float radius)
        {
            rb = rigidbody;
            animator = anim;
            ladderCheckTransform = ladderCheck;
            climbSpeed = speed;
            ladderLayer = ladders;
            ladderCheckRadius = radius;

            // Store the original gravity scale
            originalGravity = rb.gravityScale;
            Debug.Log($"ClimbController initialized with original gravity: {originalGravity}");

            // Find player target if not already set
            if (target == null)
            {
                target = GameObject.FindGameObjectWithTag("Player")?.transform;
            }
        }

        private void Update()
        {
            if (isClimbing)
            {
                // Get the bottom of the LadderCheck collider
                Collider2D ladderCheckCollider = ladderCheckTransform.GetComponent<Collider2D>();
                if (ladderCheckCollider != null)
                {
                    float ladderCheckBottom = ladderCheckCollider.bounds.min.y;

                    // If bottom is above ladder top + small margin, we've exited
                    if (ladderCheckBottom > ladderTopPosition + 0.1f)
                    {
                        timeAtLadderTop += Time.deltaTime;

                        if (timeAtLadderTop > 0.2f) // stable detection delay
                        {
                            Debug.Log($"LadderCheck fully cleared ladder. Bottom: {ladderCheckBottom}, Ladder top: {ladderTopPosition}");
                            CompleteClimbing();
                            return;
                        }
                    }
                    else
                    {
                        timeAtLadderTop = 0f;
                    }
                }
                
                // Update last position for potential future use
                lastPosition = transform.position;
            }
        }

        private void CompleteClimbing()
        {
            Debug.Log("Completing climb - LadderCheck fully cleared ladder");

            isClimbing = false;
            isAtTopOfLadder = false;

            // Restore gravity
            rb.gravityScale = originalGravity;

            // Always force a jump when completing the climb
            Debug.Log("Forcing jump on ladder exit");
            JumpAfterClimb();

            // Note: We don't call navController.LadderClimbCompleted() here 
            // because the jump will handle the state transition
        }

        // Add this new method for the direct jump after climbing
        private void JumpAfterClimb()
        {
            // Get target direction for the jump
            float horizontalDirection = 1f;
            if (target != null)
            {
                horizontalDirection = (target.position.x > transform.position.x) ? 1f : -1f;
            }

            // Apply jump force - use higher values to ensure the jump is effective
            rb.linearVelocity = new Vector2(horizontalDirection * exitHorizontalForce, exitJumpForce);
            
            // Make sure gravity is restored
            rb.gravityScale = originalGravity;

            // Notify navigation controller to change state
            EnemyNavigationController navController = GetComponent<EnemyNavigationController>();
            if (navController != null)
            {
                // Force change to jumping state
                navController.ForceStateChange(NavigationState.Jumping);
                
                // Also try direct call to existing jump controller if available
                JumpController jumpController = GetComponent<JumpController>();
                if (jumpController != null)
                {
                    // Execute jump through the jump controller
                    jumpController.ExecuteJump();
                    Debug.Log("Executed jump using JumpController after ladder climb");
                }
            }
        }

        public bool IsLadderDetected()
        {
            bool detected = false;

            // Use a much larger radius for ladder detection
            float detectionMultiplier = 0.5f; // Increased from 1.5f

            if (ladderCheckTransform != null)
            {
                // Multiple check points along ladder height
                Vector3[] checkPositions = new Vector3[]
                {
                    ladderCheckTransform.position,
                    ladderCheckTransform.position + Vector3.up * 0.5f,
                    ladderCheckTransform.position + Vector3.down * 0.5f
                };

                foreach (Vector3 checkPos in checkPositions)
                {
                    Collider2D ladderCollider = Physics2D.OverlapCircle(
                        checkPos,
                        ladderCheckRadius * detectionMultiplier,
                        ladderLayer
                    );

                    if (ladderCollider != null)
                    {
                        detected = true;
                        currentLadderCollider = ladderCollider;
                        ladderTopPosition = ladderCollider.bounds.max.y;
                        lastLadderY = ladderTopPosition;
                        break;
                    }
                }
            }

            // If already climbing, allow a short grace period after losing contact
            if (!detected && isClimbing)
            {
                detected = (Time.time - lastLadderContactTime) < ladderGracePeriod;
            }

            if (detected)
            {
                lastLadderContactTime = Time.time;
            }

            return detected;
        }

        public void ExecuteClimb(float targetY)
        {
            bool currentlyOnLadder = IsLadderDetected();

            // If starting climb
            if (!isClimbing && currentlyOnLadder)
            {
                StartClimbing();
            }

            if (isClimbing)
            {
                if (currentlyOnLadder)
                {
                    // Continue climbing
                    rb.gravityScale = 0;
                    rb.linearVelocity = new Vector2(0, climbSpeed);
                    UpdateClimbingAnimation(1.0f);
                }
                // else: let Update() handle when we've exited
            }

            wasOnLadder = currentlyOnLadder;
        }

        private void UpdateClimbingAnimation(float verticalDirection)
        {
            if (animator == null) return;

            if (Mathf.Abs(verticalDirection) > 0.1f)
            {
                animator.speed = 1.0f;
                if (verticalDirection > 0)
                    animator.Play("ClimbUp");
                else
                    animator.Play("ClimbDown");
            }
            else
            {
                animator.speed = 0f;
            }
        }

        private void StartClimbing()
        {
            isClimbing = true;
            rb.gravityScale = 0;
            timeAtLadderTop = 0f;
            CancelGravityReset();

            // Reset position tracking
            lastPosition = transform.position;

            if (animator != null)
                animator.SetBool("isClimbing", true);

            Debug.Log($"Started climbing. Gravity set to 0 (from {originalGravity})");
        }

        private void StartDelayedGravityReset()
        {
            if (gravityResetCoroutine == null)
            {
                gravityResetCoroutine = StartCoroutine(ResetGravityAfterDelay());
            }
        }

        private void CancelGravityReset()
        {
            if (gravityResetCoroutine != null)
            {
                StopCoroutine(gravityResetCoroutine);
                gravityResetCoroutine = null;
                Debug.Log("Gravity reset timer canceled");
            }
        }

        private IEnumerator ResetGravityAfterDelay()
        {
            Debug.Log($"Starting gravity reset timer: {gravityResetDelay} seconds. Currently isClimbing={isClimbing}");
            yield return new WaitForSeconds(gravityResetDelay);
            StopClimbing();
            Debug.Log($"Gravity reset to: {originalGravity} after delay");
            gravityResetCoroutine = null;
        }

        public void StopClimbing()
        {
            if (isClimbing)
            {
                isClimbing = false;
                isAtTopOfLadder = false;
                rb.gravityScale = originalGravity;

                if (animator != null)
                {
                    animator.SetBool("isClimbing", false);
                    animator.speed = 1.0f;
                }

                Debug.Log($"Stopped climbing. Gravity restored to {originalGravity}");
            }
        }

        public void OnNavigationStateChanged(NavigationState newState)
        {
            // If leaving climbing state
            if (newState != NavigationState.Climbing)
            {
                bool shouldStopImmediately = newState == NavigationState.Falling ||
                                             newState == NavigationState.Jumping;

                if (shouldStopImmediately)
                {
                    CancelGravityReset();
                    StopClimbing();
                    Debug.Log($"Force stopped climbing due to state change to {newState}");
                }
            }
        }

        private void OnDisable()
        {
            if (rb != null && isClimbing)
            {
                rb.gravityScale = originalGravity;
                Debug.Log($"OnDisable: Restored gravity to {originalGravity}");
            }

            if (gravityResetCoroutine != null)
            {
                StopCoroutine(gravityResetCoroutine);
                gravityResetCoroutine = null;
            }
        }

        private void OnDrawGizmos()
        {
            if (!Application.isPlaying) return;

            // Draw ladder top line
            if (currentLadderCollider != null)
            {
                Gizmos.color = Color.cyan;
                Vector3 topPos = new Vector3(
                    currentLadderCollider.bounds.center.x,
                    currentLadderCollider.bounds.max.y,
                    0
                );
                Gizmos.DrawLine(topPos - Vector3.right * 0.5f, topPos + Vector3.right * 0.5f);

#if UNITY_EDITOR
                UnityEditor.Handles.Label(
                    topPos + Vector3.up * 0.2f,
                    $"Ladder Top: {ladderTopPosition:F2}"
                );
#endif
            }

            // Draw LadderCheck bounds
            if (ladderCheckTransform != null)
            {
                Collider2D ladderCheckCollider = ladderCheckTransform.GetComponent<Collider2D>();
                if (ladderCheckCollider != null)
                {
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawWireCube(
                        ladderCheckCollider.bounds.center,
                        ladderCheckCollider.bounds.size
                    );

                    Gizmos.color = Color.red;
                    Vector3 bottomPos = new Vector3(
                        ladderCheckCollider.bounds.center.x,
                        ladderCheckCollider.bounds.min.y,
                        0
                    );
                    Gizmos.DrawSphere(bottomPos, 0.1f);

#if UNITY_EDITOR
                    UnityEditor.Handles.Label(
                        bottomPos - Vector3.up * 0.2f,
                        $"Bottom: {ladderCheckCollider.bounds.min.y:F2}"
                    );
#endif
                }
            }

#if UNITY_EDITOR
            if (isClimbing)
            {
                UnityEditor.Handles.Label(
                    transform.position + Vector3.up * 2f,
                    $"Climbing: {isClimbing}\nDetecting Ladder: {IsLadderDetected()}"
                );
            }
#endif
        }
    }
}