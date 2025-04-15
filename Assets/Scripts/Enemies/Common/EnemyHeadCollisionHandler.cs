using UnityEngine;

public class EnemyHeadCollisionHandler : MonoBehaviour
{
    private MonoBehaviour parentScript;
    private Animator parentAnimator;

    private void Awake()
    {
        // Detect whether the parent has a Shielder or ElfAssassin script
        if (GetComponentInParent<Shielder>() != null)
        {
            parentScript = GetComponentInParent<Shielder>();
            parentAnimator = GetComponentInParent<Animator>(); // Get reference to the parent's animator
        }
        else if (GetComponentInParent<ElfAssassin>() != null)
        {
            parentScript = GetComponentInParent<ElfAssassin>();
            parentAnimator = GetComponentInParent<Animator>(); // Get reference to the parent's animator
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        // Check if the collision is with the player
        if (collision.collider.CompareTag("Player"))
        {
            foreach (ContactPoint2D contact in collision.contacts)
            {
                // Check if the player is colliding from above
                if (contact.point.y > transform.position.y)
                {
                    Debug.Log("Player collided on enemy head.");
                    if (parentScript != null)
                    {
                        // Call the HandleHeadCollision method dynamically
                        parentScript.Invoke("HandleHeadCollision", 0f);
                    }

                    // Trigger attack animation
                    if (parentAnimator != null)
                    {
                        parentAnimator.SetTrigger("attack");
                    }

                    break; // Exit loop after finding a valid contact point
                }
            }
        }
    }
}
