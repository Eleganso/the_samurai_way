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
        
        [SerializeField] private float gravityResetDelay = 2f;  // Delay before resetting gravity
        [SerializeField] private float topLadderSafetyMargin = 0.5f; // How far to extend detection at top
        private Coroutine gravityResetCoroutine;

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
        }

        public bool IsLadderDetected()
        {
            bool detected = false;
            float highestLadderY = float.MinValue;
            
            // First, try normal ladder detection
            if (ladderCheckTransform == null)
            {
                // Calculate a position at mid-height of the entity
                Vector2 handsPosition = (Vector2)transform.position + new Vector2(0, 0.5f);
                Collider2D ladder = Physics2D.OverlapCircle(handsPosition, ladderCheckRadius, ladderLayer);
                
                if (ladder != null)
                {
                    detected = true;
                    highestLadderY = ladder.bounds.max.y;
                }
            }
            else
            {
                // Check for ladder at current position
                Collider2D ladder = Physics2D.OverlapCircle(ladderCheckTransform.position, ladderCheckRadius * 1.2f, ladderLayer);
                
                if (ladder != null)
                {
                    detected = true;
                    highestLadderY = ladder.bounds.max.y;
                }
                else
                {
                    // Try a box cast for better detection
                    RaycastHit2D hit = Physics2D.BoxCast(
                        ladderCheckTransform.position,
                        new Vector2(ladderCheckRadius * 1.5f, ladderCheckRadius * 2f),
                        0f,
                        Vector2.zero,
                        0f,
                        ladderLayer
                    );
                    
                    if (hit.collider != null)
                    {
                        detected = true;
                        highestLadderY = hit.collider.bounds.max.y;
                    }
                }
            }
            
            // Special handling for top of ladder
            if (!detected && isClimbing)
            {
                // We were climbing but lost contact - check if we're at the top of the ladder
                float currentY = transform.position.y;
                
                // If we're close to the last known ladder top position
                if (Mathf.Abs(currentY - lastLadderY) < topLadderSafetyMargin)
                {
                    isAtTopOfLadder = true;
                    detected = true;
                    
                    if (debugLadderDetection)
                    {
                        Debug.Log($"At top of ladder: position {currentY}, last ladder Y {lastLadderY}");
                    }
                }
            }
            
            // Update last ladder Y position if we detected a ladder
            if (detected && highestLadderY > float.MinValue)
            {
                lastLadderY = highestLadderY;
            }
            
            if (debugLadderDetection)
            {
                Debug.Log($"Ladder detected: {detected} at position Y: {transform.position.y}, ladder top: {lastLadderY}");
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
                // Check if we've left the ladder
                if (!currentlyOnLadder && wasOnLadder && !isAtTopOfLadder)
                {
                    // We just left the ladder - start the delayed gravity reset
                    StartDelayedGravityReset();
                }
                else if (currentlyOnLadder)
                {
                    // Reset top of ladder flag when we're clearly on a ladder
                    isAtTopOfLadder = false;
                    
                    // We're still on the ladder, cancel any pending gravity reset
                    CancelGravityReset();
                    
                    // Calculate climbing direction
                    float verticalDirection = Mathf.Sign(targetY - transform.position.y);
                    
                    // Apply vertical movement for climbing
                    rb.linearVelocity = new Vector2(0, verticalDirection * climbSpeed);
                    
                    // Animate climbing based on direction
                    UpdateClimbingAnimation(verticalDirection);
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
                // If we're at the top of the ladder, don't allow falling transition
                if (isAtTopOfLadder && newState == NavigationState.Falling)
                {
                    // Force back to climbing state
                    Debug.Log("Prevented falling at top of ladder - staying in climbing state");
                    return;
                }
                
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
            if (!Application.isPlaying || !debugLadderDetection) return;
            
            // Draw ladder detection radius
            Gizmos.color = IsLadderDetected() ? Color.green : Color.red;
            
            if (ladderCheckTransform != null)
            {
                Gizmos.DrawWireSphere(ladderCheckTransform.position, ladderCheckRadius);
            }
            else
            {
                Vector2 handsPosition = (Vector2)transform.position + new Vector2(0, 0.5f);
                Gizmos.DrawWireSphere(handsPosition, ladderCheckRadius);
            }
        }
    }
}