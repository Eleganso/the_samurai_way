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
        
        // Add target reference to find player
        private Transform target;
        
        [SerializeField] private float gravityResetDelay = 2f;  // Delay before resetting gravity
        [SerializeField] private float topLadderSafetyMargin = 0.5f; // How far to extend detection at top
        [SerializeField] private float climbCompletionThreshold = 2.0f; // Distance above ladder to consider climbing complete
        private Coroutine gravityResetCoroutine;
        private Collider2D currentLadderCollider;
        private float ladderTopPosition = 0f;
        private float timeAtLadderTop = 0f;

        // Debug/visualization properties
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

        // Called every frame to check for ladder exit conditions
        private void Update()
        {
            if (isClimbing)
            {
                // Get the bottom of the LadderCheck collider (assuming it's a CapsuleCollider2D for full height)
                Collider2D ladderCheckCollider = ladderCheckTransform.GetComponent<Collider2D>();
                if (ladderCheckCollider != null)
                {
                    float ladderCheckBottom = ladderCheckCollider.bounds.min.y;
                    
                    // If the bottom of our LadderCheck is above the ladder top, we've fully exited
                    if (ladderCheckBottom > ladderTopPosition + 0.1f)
                    {
                        timeAtLadderTop += Time.deltaTime;
                        
                        if (timeAtLadderTop > 0.2f) // Short delay to ensure stable detection
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
            }
        }

        // New method to complete climbing and transition to walking
        private void CompleteClimbing()
        {
            Debug.Log("Completing climb - LadderCheck fully cleared ladder");
            
            // Reset climbing state
            isClimbing = false;
            isAtTopOfLadder = false;
            
            // Restore gravity
            rb.gravityScale = originalGravity;
            
            // Notify the navigation controller
            EnemyNavigationController navController = GetComponent<EnemyNavigationController>();
            if (navController != null)
            {
                navController.LadderClimbCompleted();
            }
        }

        public bool IsLadderDetected()
        {
            bool detected = false;
            
            // Check for ladder collider
            Collider2D ladderCollider = null;
            
            if (ladderCheckTransform != null)
            {
                // Use the designated ladderCheck transform
                ladderCollider = Physics2D.OverlapCircle(ladderCheckTransform.position, ladderCheckRadius * 1.5f, ladderLayer);
            }
            else
            {
                // Fallback to a calculated position
                Vector2 handsPosition = (Vector2)transform.position + new Vector2(0, 0.5f);
                ladderCollider = Physics2D.OverlapCircle(handsPosition, ladderCheckRadius * 1.5f, ladderLayer);
            }
            
            if (ladderCollider != null)
            {
                detected = true;
                currentLadderCollider = ladderCollider;
                
                // Store ladder top position
                ladderTopPosition = ladderCollider.bounds.max.y;
                lastLadderY = ladderTopPosition;
                
                if (debugLadderDetection)
                {
                    Collider2D ladderCheckCollider = ladderCheckTransform.GetComponent<Collider2D>();
                    float ladderCheckBottom = ladderCheckCollider != null ? ladderCheckCollider.bounds.min.y : transform.position.y;
                    
                    Debug.Log($"Ladder detected! Ladder top: {ladderTopPosition}, LadderCheck bottom: {ladderCheckBottom}");
                }
            }
            else if (isClimbing)
            {
                // If we're climbing but don't detect a ladder anymore,
                // continue climbing until LadderCheck fully clears the ladder
                Collider2D ladderCheckCollider = ladderCheckTransform.GetComponent<Collider2D>();
                if (ladderCheckCollider != null)
                {
                    float ladderCheckBottom = ladderCheckCollider.bounds.min.y;
                    
                    if (ladderCheckBottom <= ladderTopPosition)
                    {
                        // We're still exiting the ladder
                        detected = true;
                        if (debugLadderDetection)
                        {
                            Debug.Log($"Still exiting ladder. LadderCheck bottom: {ladderCheckBottom}, Ladder top: {ladderTopPosition}");
                        }
                    }
                }
            }
            
            return detected;
        }
        
        public void ExecuteClimb(float targetY)
        {
            bool currentlyOnLadder = IsLadderDetected();
            
            // If we weren't climbing but now we should, start climbing
            if (!isClimbing && currentlyOnLadder)
            {
                StartClimbing();
            }
            
            // If we are in climbing state
            if (isClimbing)
            {
                if (currentlyOnLadder)
                {
                    // Continue climbing upward
                    rb.gravityScale = 0;
                    rb.linearVelocity = new Vector2(0, climbSpeed);
                    UpdateClimbingAnimation(1.0f);
                }
                else
                {
                    // We're no longer on the ladder
                    // This will be handled by the Update method and CompleteClimbing
                }
            }
            
            // Update ladder state for next frame
            wasOnLadder = currentlyOnLadder;
        }
        
        private void UpdateClimbingAnimation(float verticalDirection)
        {
            if (animator == null) return;
            
            if (Mathf.Abs(verticalDirection) > 0.1f)
            {
                animator.speed = 1.0f; // Normal animation speed when moving
                if (verticalDirection > 0)
                {
                    animator.Play("ClimbUp");
                }
                else
                {
                    animator.Play("ClimbDown");
                }
            }
            else
            {
                animator.speed = 0f; // Pause animation when not moving on ladder
            }
        }
        
        private void StartClimbing()
        {
            isClimbing = true;
            rb.gravityScale = 0; // Disable gravity while climbing
            timeAtLadderTop = 0f;
            
            // Cancel any pending gravity reset
            CancelGravityReset();
            
            if (animator != null)
            {
                animator.SetBool("isClimbing", true);
            }
            
            Debug.Log($"Started climbing. Gravity set to 0 (from {originalGravity})");
        }
        
        private void StartDelayedGravityReset()
        {
            // Only start a new coroutine if one isn't already running
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
            
            // Wait for the specified delay time
            yield return new WaitForSeconds(gravityResetDelay);
            
            // Reset gravity and stop climbing
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
                rb.gravityScale = originalGravity; // Restore original gravity
                
                if (animator != null)
                {
                    animator.SetBool("isClimbing", false);
                    animator.speed = 1.0f; // Reset animation speed
                }
                
                Debug.Log($"Stopped climbing. Gravity restored to {originalGravity}");
            }
        }
        
        // This is called when the NavigationController transitions states
        public void OnNavigationStateChanged(NavigationState newState)
        {
            // If we're transitioning away from climbing state
            if (newState != NavigationState.Climbing)
            {
                // Check if we need to stop climbing immediately
                bool shouldStopImmediately = newState == NavigationState.Falling ||
                                            newState == NavigationState.Jumping;
                
                if (shouldStopImmediately)
                {
                    // Cancel any pending gravity reset and stop climbing right away
                    CancelGravityReset();
                    StopClimbing();
                    Debug.Log($"Force stopped climbing due to state change to {newState}");
                }
            }
        }
        
        // Call this when the entity is destroyed or disabled
        private void OnDisable()
        {
            // Ensure we don't leave the gravity at zero
            if (rb != null && isClimbing)
            {
                rb.gravityScale = originalGravity;
                Debug.Log($"OnDisable: Restored gravity to {originalGravity}");
            }
            
            // Clean up any running coroutines
            if (gravityResetCoroutine != null)
            {
                StopCoroutine(gravityResetCoroutine);
                gravityResetCoroutine = null;
            }
        }
        
        private void OnDrawGizmos()
        {
            if (!Application.isPlaying) return;
            
            // Draw ladder detection
            if (currentLadderCollider != null)
            {
                // Draw the ladder top
                Gizmos.color = Color.cyan;
                Vector3 topPos = new Vector3(currentLadderCollider.bounds.center.x, currentLadderCollider.bounds.max.y, 0);
                Gizmos.DrawLine(topPos - Vector3.right * 0.5f, topPos + Vector3.right * 0.5f);
                
                // Label the ladder top position
                #if UNITY_EDITOR
                UnityEditor.Handles.Label(topPos + Vector3.up * 0.2f, $"Ladder Top: {ladderTopPosition:F2}");
                #endif
            }
            
            // Draw LadderCheck bounds if available
            if (ladderCheckTransform != null)
            {
                Collider2D ladderCheckCollider = ladderCheckTransform.GetComponent<Collider2D>();
                if (ladderCheckCollider != null)
                {
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawWireCube(ladderCheckCollider.bounds.center, ladderCheckCollider.bounds.size);
                    
                    // Draw the bottom point of the LadderCheck
                    Gizmos.color = Color.red;
                    Vector3 bottomPos = new Vector3(ladderCheckCollider.bounds.center.x, ladderCheckCollider.bounds.min.y, 0);
                    Gizmos.DrawSphere(bottomPos, 0.1f);
                    
                    #if UNITY_EDITOR
                    UnityEditor.Handles.Label(bottomPos - Vector3.up * 0.2f, $"Bottom: {ladderCheckCollider.bounds.min.y:F2}");
                    #endif
                }
            }
            
            // Draw climbing status
            #if UNITY_EDITOR
            if (isClimbing)
            {
                UnityEditor.Handles.Label(transform.position + Vector3.up * 2f, 
                    $"Climbing: {isClimbing}\nDetecting Ladder: {IsLadderDetected()}");
            }
            #endif
        }
    }
}