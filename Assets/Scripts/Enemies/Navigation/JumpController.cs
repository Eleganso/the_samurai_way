using UnityEngine;

namespace Enemies.Navigation
{
    public class JumpController : MonoBehaviour
    {
        private Rigidbody2D rb;
        private Animator animator;
        private float jumpForce;
        private float maxJumpDistance;
        private bool isJumping = false;
        private float jumpStartTime;
        private Vector2 jumpTarget;

        public bool IsJumping => isJumping;

        public void Initialize(Rigidbody2D rigidbody, Animator anim, float force, float maxDist)
        {
            rb = rigidbody;
            animator = anim;
            jumpForce = force;
            maxJumpDistance = maxDist;
        }

        public bool CanJumpOver(bool isFacingRight)
{
    // Get the obstacle detection component
    ObstacleDetection detector = GetComponent<ObstacleDetection>();
    if (detector == null) return false;
    
    // Get the height of the obstacle
    float obstacleHeight = detector.GetObstacleHeight(isFacingRight);
    Debug.Log($"Obstacle height: {obstacleHeight}");
    
    // Calculate maximum jump height using physics formula: h = v²/(2*g)
    float gravity = Mathf.Abs(Physics2D.gravity.y);
    float maxHeight = (jumpForce * jumpForce) / (2 * gravity * rb.gravityScale);
    
    // Add a small buffer for safety (80% of theoretical max height)
    maxHeight *= 0.8f;
    
    Debug.Log($"Max jump height: {maxHeight}, Required height: {obstacleHeight}");
    
    // Return true if we can jump over the obstacle
    bool canJump = maxHeight >= obstacleHeight && obstacleHeight > 0;
    Debug.Log($"Can jump over obstacle: {canJump}");
    
    return canJump;
}

        public bool CanJumpAcross(bool isFacingRight)
        {
            // Reference to the ObstacleDetection component
            ObstacleDetection detector = GetComponent<ObstacleDetection>();
            if (detector == null) return false;

            // Get the distance to the edge
            float edgeDistance = detector.GetEdgeDistance(isFacingRight);
            
            // If no edge is detected or it's too far, we can't jump
            if (edgeDistance <= 0 || edgeDistance > maxJumpDistance)
            {
                return false;
            }
            
            return true;
        }

        public void ExecuteJump()
        {
            if (!isJumping)
            {
                // Start the jump
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
                isJumping = true;
                jumpStartTime = Time.time;
                
                if (animator != null)
                {
                    animator.SetTrigger("jump");
                }
            }
            else
            {
                // Check if we've reached the apex of the jump and should transition out
                if (rb.linearVelocity.y < 0.1f)
                {
                    isJumping = false;
                }
                
                // Maintain horizontal movement during jump
                // This keeps the momentum going in the right direction
            }
        }

        public void CalculateArcToTarget(Vector2 targetPosition)
        {
            Vector2 startPos = rb.position;
            Vector2 displacement = targetPosition - startPos;
            
            // Simple parabolic path calculation
            // For more complex jumps, you could use projectile motion equations
            
            jumpTarget = targetPosition;
        }
    }
}