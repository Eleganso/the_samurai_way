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
        
        [SerializeField] private float gravityResetDelay = 2f;  // Delay before resetting gravity
        [SerializeField] private float topLadderSafetyMargin = 0.5f; // How far to extend detection at top
        private Coroutine gravityResetCoroutine;
        private Collider2D currentLadderCollider;

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
            
            // First, check if we're already at the top of a ladder and should stay in climbing mode
            if (isAtTopOfLadder)
            {
                // Keep detecting as true if we're at the top
                detected = true;
                
                if (debugLadderDetection)
                {
                    Debug.Log($"Still at ladder top: {transform.position.y}, last ladder Y: {lastLadderY}");
                }
                
                return true;
            }
            
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
                lastLadderY = ladderCollider.bounds.max.y;
                
                if (debugLadderDetection)
                {
                    Debug.Log($"Ladder detected at position Y: {transform.position.y}, ladder top: {lastLadderY}");
                }
            }
            else if (isClimbing)
            {
                // If we're climbing but don't detect a ladder, check if we're near the top
                float distanceToLadderTop = Mathf.Abs(transform.position.y - lastLadderY);
                
                // If we're close to the top and moving up, consider us still on the ladder
                if (distanceToLadderTop < topLadderBuffer && transform.position.y >= lastLadderY - topLadderBuffer)
                {
                    isAtTopOfLadder = true;
                    detected = true;
                    
                    if (debugLadderDetection)
                    {
                        Debug.Log($"At top of ladder: position {transform.position.y}, last ladder Y {lastLadderY}");
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
                // If we're at the top of ladder but within the safety margin
                if (isAtTopOfLadder)
                {
                    // Check if we've moved away from the top (horizontally or completely up)
                    float distanceToLadderTop = Mathf.Abs(transform.position.y - lastLadderY);
                    
                    if (distanceToLadderTop > topLadderSafetyMargin * 2f)
                    {
                        // We've moved away from the top of the ladder
                        isAtTopOfLadder = false;
                        if (!currentlyOnLadder)
                        {
                            StartDelayedGravityReset();
                        }
                    }
                    else
                    {
                        // We're still at the top - ensure gravity is still 0
                        rb.gravityScale = 0;
                        
                        // Allow slight movement at the top to help transition
                        float verticalDirection = Mathf.Sign(targetY - transform.position.y);
                        if (verticalDirection > 0 && transform.position.y < lastLadderY + topLadderSafetyMargin)
                        {
                            // Allow slight upward movement to clear the top
                            rb.linearVelocity = new Vector2(0, verticalDirection * climbSpeed);
                        }
                        else
                        {
                            // Stop at the top
                            rb.linearVelocity = Vector2.zero;
                        }
                    }
                }
                // Regular ladder climbing logic
                else if (!currentlyOnLadder && wasOnLadder)
                {
                    // We just left the ladder - start the delayed gravity reset
                    StartDelayedGravityReset();
                }
                else if (currentlyOnLadder)
                {
                    // Calculate climb direction and apply movement
                    HandleClimbingMovement(targetY);
                }
            }
            
            // Update ladder state for next frame
            wasOnLadder = currentlyOnLadder;
        }
        
        private void HandleClimbingMovement(float targetY)
        {
            // Cancel any pending gravity reset
            CancelGravityReset();
            
            // Reset top of ladder flag when we're on a ladder but not at the top
            if (isAtTopOfLadder && transform.position.y < lastLadderY - topLadderBuffer)
            {
                isAtTopOfLadder = false;
            }
            
            // Calculate climbing direction
            float verticalDirection = Mathf.Sign(targetY - transform.position.y);
            
            // Special handling near top of ladder
            if (currentLadderCollider != null)
            {
                float distanceToTop = currentLadderCollider.bounds.max.y - transform.position.y;
                
                // If we're approaching the top of the ladder
                if (distanceToTop < topLadderBuffer && verticalDirection > 0)
                {
                    // Slow down as we approach the top
                    float slowDownFactor = Mathf.Clamp01(distanceToTop / topLadderBuffer);
                    verticalDirection *= slowDownFactor;
                    
                    // If we're very close to the top, snap to it
                    if (distanceToTop < 0.1f)
                    {
                        transform.position = new Vector3(transform.position.x, currentLadderCollider.bounds.max.y, transform.position.z);
                        rb.linearVelocity = Vector2.zero;
                        isAtTopOfLadder = true;
                        return;
                    }
                }
            }
            
            // Apply vertical movement for climbing
            rb.linearVelocity = new Vector2(0, verticalDirection * climbSpeed);
            
            // Animate climbing based on direction
            UpdateClimbingAnimation(verticalDirection);
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
                // If we're at the top of the ladder, override the state transition
                if (isAtTopOfLadder && (newState == NavigationState.Falling || newState == NavigationState.Walking))
                {
                    Debug.Log("Prevented state change from climbing at ladder top");
                    
                    // If we need to notify the navigation controller to stay in climbing
                    EnemyNavigationController navController = GetComponent<EnemyNavigationController>();
                    if (navController != null)
                    {
                        navController.ForceClimbingState();
                    }
                    
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
                Gizmos.DrawWireSphere(ladderCheckTransform.position, ladderCheckRadius * 1.5f);
            }
            else
            {
                Vector2 handsPosition = (Vector2)transform.position + new Vector2(0, 0.5f);
                Gizmos.DrawWireSphere(handsPosition, ladderCheckRadius * 1.5f);
            }
            
            // Draw a box at the top of the ladder if we know where it is
            if (isAtTopOfLadder || lastLadderY > 0)
            {
                Gizmos.color = isAtTopOfLadder ? Color.green : Color.yellow;
                Vector2 topPosition = new Vector2(transform.position.x, lastLadderY);
                Gizmos.DrawCube(topPosition, new Vector3(1f, 0.1f, 0.1f));
            }
        }
    }
}