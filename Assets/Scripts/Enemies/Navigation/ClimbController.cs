using UnityEngine;

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

        public void Initialize(Rigidbody2D rigidbody, Animator anim, Transform ladderCheck, 
                              float speed, LayerMask ladders, float radius)
        {
            rb = rigidbody;
            animator = anim;
            ladderCheckTransform = ladderCheck;
            climbSpeed = speed;
            ladderLayer = ladders;
            ladderCheckRadius = radius;
            originalGravity = rb.gravityScale;
        }

        public bool IsLadderDetected()
{
    if (ladderCheckTransform == null)
    {
        // Calculate a position at mid-height of the entity
        Vector2 handsPosition = (Vector2)transform.position + new Vector2(0, 0.5f);
        Collider2D ladder = Physics2D.OverlapCircle(handsPosition, ladderCheckRadius, ladderLayer);
        
        return ladder != null;
    }
    
    return Physics2D.OverlapCircle(ladderCheckTransform.position, ladderCheckRadius, ladderLayer) != null;
}
        
        public void ExecuteClimb(float targetY)
        {
            if (!isClimbing && IsLadderDetected())
            {
                // Start climbing
                isClimbing = true;
                rb.gravityScale = 0; // Disable gravity while climbing
                
                if (animator != null)
                {
                    animator.SetBool("isClimbing", true);
                }
            }
            
            if (isClimbing)
            {
                if (!IsLadderDetected())
                {
                    // Stop climbing if we lost contact with ladder
                    StopClimbing();
                    return;
                }
                
                // Determine climb direction based on target position
                float verticalDirection = Mathf.Sign(targetY - transform.position.y);
                
                // Apply vertical movement for climbing
                rb.linearVelocity = new Vector2(0, verticalDirection * climbSpeed);
                
                // Animate climbing based on direction
                if (animator != null && Mathf.Abs(verticalDirection) > 0.1f)
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
                else if (animator != null)
                {
                    animator.speed = 0f; // Pause animation when not moving on ladder
                }
            }
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
            }
        }
    }
}