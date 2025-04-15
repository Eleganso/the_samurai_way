using UnityEngine;
using System.Collections;

public class Portcullis : MonoBehaviour, IDamageable
{
    [Header("Portcullis Settings")]
    public string portcullisID; // Unique ID for saving/loading

    [Header("Components")]
    public Animator animator; // Animator for Portcullis
    public Collider2D blockingCollider; // Collider that blocks the player
    public Collider2D winchCollider; // Collider on the Winch (child)

    [Header("Portcullis State")]
    public bool isOpen = false; // Current state of the Portcullis
    public bool HasTakenDamage { get; set; } = false; // From IDamageable

    private void Start()
    {
        if (animator == null)
            animator = GetComponent<Animator>();

        if (blockingCollider == null)
            blockingCollider = GetComponent<Collider2D>();

        if (winchCollider == null)
        {
            // Assume the Winch is the first child
            winchCollider = transform.GetChild(0).GetComponent<Collider2D>();
        }

        // Initialize the Portcullis state based on saved data
        InitializePortcullis();
    }

    /// <summary>
    /// Initializes the Portcullis based on saved state.
    /// </summary>
    public void InitializePortcullis()
    {
        isOpen = GameManager.Instance.IsPortcullisOpen(portcullisID);

        if (isOpen)
        {
            // Directly set the Portcullis to its open state
            animator.Play("Opening", 0, 1f); // Play the "Opening" animation at the last frame
            blockingCollider.enabled = false;
        }
        else
        {
            // Set to closed state
            animator.Play("Closed"); // Play the "Closed" animation
            blockingCollider.enabled = true;
        }
    }

    /// <summary>
    /// Opens the Portcullis by playing the opening animation.
    /// </summary>
    public void OpenPortcullis()
    {
        if (isOpen) return; // Already open

        isOpen = true;
        GameManager.Instance.SetPortcullisState(portcullisID, true);

        // Trigger the opening animation
        animator.SetTrigger("OpenTrigger");

        // Disable the blocking collider after the animation finishes
        StartCoroutine(DisableColliderAfterAnimation());
    }

    private IEnumerator DisableColliderAfterAnimation()
    {
        // Wait for the length of the "Opening" animation
        yield return new WaitForSeconds(animator.GetCurrentAnimatorStateInfo(0).length);

        // Disable the blocking collider to allow the player to pass
        if (blockingCollider != null)
            blockingCollider.enabled = false;
    }

    // IDamageable Implementation
    public void Damage(float damageAmount, GameObject source)
    {
        // The Portcullis is activated when the Winch takes damage

        if (!isOpen)
        {
            OpenPortcullis();

            // Optional: Play an animation or effect on the Winch

            // Disable the Winch collider to prevent further interactions
            if (winchCollider != null)
                winchCollider.enabled = false;
        }
    }

    public void Knockback(Vector2 knockbackDirection, float knockbackForce)
    {
        // The Portcullis does not react to knockback
    }
}
