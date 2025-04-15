using UnityEngine;

public class LadderMovement : MonoBehaviour
{
    private float vertical;
    public float speed = 8f;
    private bool isLadder;
    private bool isClimbing;

    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private Animator animator; // Reference to Animator
    private PlayerAttack playerAttack; // Reference to PlayerAttack script

    void Start()
    {
        playerAttack = GetComponent<PlayerAttack>(); // Get the PlayerAttack component
    }

    void Update()
    {
        vertical = Input.GetAxisRaw("Vertical");

        if (isLadder && Mathf.Abs(vertical) > 0f)
        {
            isClimbing = true;
            playerAttack.DisableAttack(); // Disable attack when climbing
        }
        else if (isLadder && Mathf.Abs(vertical) == 0f)
        {
            isClimbing = true; // Keep player in climbing state but stop movement

            // Pause the animator only if the current animation is ClimbUp or ClimbDown
            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            if (stateInfo.IsName("ClimbUp") || stateInfo.IsName("ClimbDown"))
            {
                animator.speed = 0f; // Freeze on the current frame if not moving
            }
        }
        else
        {
            isClimbing = false;
            playerAttack.EnableAttack(); // Enable attack when not climbing
        }
    }

    private void FixedUpdate()
    {
        if (isLadder)
        {
            if (isClimbing)
            {
                rb.gravityScale = 0f;
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, vertical * speed);

                // Set climbing animations
                if (vertical > 0)
                {
                    animator.speed = 1f; // Ensure animator speed is reset
                    animator.Play("ClimbUp");
                }
                else if (vertical < 0)
                {
                    animator.speed = 1f; // Ensure animator speed is reset
                    animator.Play("ClimbDown");
                }
            }
            else
            {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f); // Stop vertical movement when not climbing
                // No need to freeze here, it's handled in Update
            }
        }
        else
        {
            rb.gravityScale = 7f;
            animator.speed = 1f; // Reset animator speed
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Ladder"))
        {
            isLadder = true;
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("Ladder"))
        {
            isLadder = false;
            isClimbing = false;
            rb.gravityScale = 7f; // Reset gravity scale when exiting ladder
            animator.speed = 1f; // Reset animator speed when exiting ladder
            animator.Play("Idle"); // Set animation to Idle when exiting ladder
            playerAttack.EnableAttack(); // Enable attack when exiting ladder
        }
    }
}
