using UnityEngine;

public class Winch : MonoBehaviour, IDamageable
{
    [Header("Linked Portcullis")]
    public Portcullis linkedPortcullis; // Reference to the Portcullis that this Winch controls

    public bool HasTakenDamage { get; set; } = false; // From IDamageable

    /// <summary>
    /// Applies damage to the Winch, triggering the linked Portcullis to open.
    /// </summary>
    /// <param name="damageAmount">Amount of damage dealt.</param>
    /// <param name="source">The source of the damage (e.g., player or fireball).</param>
    public void Damage(float damageAmount, GameObject source)
    {
        if (linkedPortcullis != null && !HasTakenDamage)
        {
            linkedPortcullis.OpenPortcullis(); // Trigger the Portcullis to open
            Debug.Log($"Winch triggered by {source.name}! Opening Portcullis.");

            // Prevent further interaction if needed
            HasTakenDamage = true;

            // Disable the collider if you want the Winch to stop interacting
            GetComponent<Collider2D>().enabled = false;
        }
    }

    /// <summary>
    /// Handles knockback effects (not applicable for Winch).
    /// </summary>
    /// <param name="knockbackDirection">Direction of knockback.</param>
    /// <param name="knockbackForce">Force of knockback.</param>
    public void Knockback(Vector2 knockbackDirection, float knockbackForce)
    {
        // Winch does not respond to knockback
    }
}
