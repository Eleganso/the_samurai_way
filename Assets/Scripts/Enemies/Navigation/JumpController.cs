using UnityEngine;

namespace Enemies.Navigation
{
    public class JumpController : MonoBehaviour
    {
        [Header("Jump Settings")]
        [SerializeField] private float jumpForce = 8f;
        [SerializeField] private float maxJumpDistance = 3f;
        [SerializeField] private float jumpForwardSpeed = 2f;    // New tunable forward jump speed

        private Rigidbody2D rb;
        private Animator animator;
        private bool isJumping = false;
        private float jumpStartTime;

        public bool IsJumping => isJumping;

        /// <summary>
        /// Initialize the jump controller with movement parameters.
        /// </summary>
        public void Initialize(
            Rigidbody2D rigidbody,
            Animator anim,
            float force,
            float maxDist,
            float forwardSpeed       // New parameter
        )
        {
            rb = rigidbody;
            animator = anim;
            jumpForce = force;
            maxJumpDistance = maxDist;
            jumpForwardSpeed = forwardSpeed;
        }

        /// <summary>
        /// Determines if the enemy can clear the obstacle height.
        /// </summary>
        public bool CanJumpOver(bool isFacingRight)
        {
            ObstacleDetection detector = GetComponent<ObstacleDetection>();
            if (detector == null)
                return false;

            float obstacleHeight = detector.GetObstacleHeight(isFacingRight);
            float gravity = Mathf.Abs(Physics2D.gravity.y);
            float maxHeight = (jumpForce * jumpForce) / (2 * gravity * rb.gravityScale);
            maxHeight *= 0.8f;  // Safety buffer

            return obstacleHeight > 0 && maxHeight >= obstacleHeight;
        }

        /// <summary>
        /// Determines if the enemy can jump across an edge gap.
        /// </summary>
        public bool CanJumpAcross(bool isFacingRight)
        {
            ObstacleDetection detector = GetComponent<ObstacleDetection>();
            if (detector == null)
                return false;

            float edgeDistance = detector.GetEdgeDistance(isFacingRight);
            return edgeDistance > 0 && edgeDistance <= maxJumpDistance;
        }

        /// <summary>
        /// Execute the jump: applies both vertical and forward velocity.
        /// </summary>
        public void ExecuteJump()
        {
            if (!isJumping)
            {
                bool facingRight = transform.localScale.x > 0;
                float hDir = facingRight ? 1f : -1f;

                // Launch with tunable forward and upward components
                rb.linearVelocity = new Vector2(hDir * jumpForwardSpeed, jumpForce);
                isJumping = true;
                jumpStartTime = Time.time;

                if (animator != null)
                    animator.SetTrigger("jump");
            }
            else
            {
                // Once past apex, allow landing
                if (rb.linearVelocity.y < 0.1f)
                    isJumping = false;
            }
        }

        /// <summary>
        /// (Optional) Calculate a parabolic arc to target position.
        /// </summary>
        public void CalculateArcToTarget(Vector2 targetPosition)
        {
            Vector2 startPos = rb.position;
            Vector2 displacement = targetPosition - startPos;
            // Future projectile motion calculations could go here
        }
    }
}
