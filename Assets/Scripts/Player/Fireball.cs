using System.Collections;
using UnityEngine;

public class Fireball : MonoBehaviour
{
    [SerializeField] private float fireballSpeed = 10f; // Speed of the fireball
    [SerializeField] private float lifeTime = 5f; // Time before the fireball is automatically destroyed
    [SerializeField] private GameObject hitEffectPrefab; // Optional: effect when the fireball hits something

    [SerializeField] private LayerMask attackableLayer; // Layer for enemies
    [SerializeField] private LayerMask wallAndGroundLayer; // Layer for walls and ground

    private Rigidbody2D rb;
    private bool hasHit = false; // To prevent multiple hits
    private float direction = 1f; // Default direction is right
    private float damage = 1f; // Damage of the fireball

    private void Start()
    {
        rb = GetComponent<Rigidbody2D>();

        // Get the base fireball damage from PlayerManager
        damage = PlayerManager.Instance.fireballDamage;

        // Automatically destroy the fireball after a certain time to prevent it from lingering
        Destroy(gameObject, lifeTime);
    }

    private void FixedUpdate()
    {
        // Move the fireball forward based on direction
        rb.linearVelocity = new Vector2(direction * fireballSpeed, 0f);
    }

    public void SetDirection(float direction)
    {
        // Set the direction of the fireball
        this.direction = direction;

        // Flip the fireball animation if it's moving left
        if (direction < 0)
        {
            transform.localScale = new Vector3(-1f, 1f, 1f);
        }
    }

    public void SetDamage(float damage)
    {
        // Set the damage of the fireball (in case we need to override it)
        this.damage = damage;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (hasHit) return; // Prevent multiple collisions
        hasHit = true;

        Debug.Log("Fireball collided with: " + collision.gameObject.name);

        // Check if the fireball hit something in the Attackable layer
        if (IsInLayerMask(collision.gameObject, attackableLayer))
        {
            HandleHit(collision.gameObject);
            Debug.Log("Fireball hit an attackable target: " + collision.gameObject.name);
        }
        // Check if the fireball hit a wall or the ground
        else if (IsInLayerMask(collision.gameObject, wallAndGroundLayer))
        {
            HandleHit(null); // Just destroy the fireball
            Debug.Log("Fireball hit a wall or the ground: " + collision.gameObject.name);
        }
        else
        {
            Debug.Log("Fireball collided with something unexpected: " + collision.gameObject.name);
            HandleHit(null); // Destroy the fireball in any case
        }
    }

    private void HandleHit(GameObject hitObject)
    {
        // Play fireball hit sound using PlayerSoundManager
        PlayerSoundManager.Instance?.PlayFireballHitSound();

        // Optional: Instantiate a hit effect at the point of collision
        if (hitEffectPrefab != null)
        {
            Instantiate(hitEffectPrefab, transform.position, transform.rotation);
        }

        // Handle damage if an enemy is hit
        if (hitObject != null)
        {
            IDamageable damageable = hitObject.GetComponent<IDamageable>();
            if (damageable != null)
            {
                // Apply magic damage multiplier (e.g., from PowerSkill2 if active)
                damage *= PlayerManager.Instance.MagicDamageMultiplier;

                // Apply damage to the enemy
                damageable.Damage(damage, gameObject);
                Debug.Log("Fireball dealt " + damage + " damage to: " + hitObject.name);
            }
            else
            {
                Debug.LogWarning("Hit object does not implement IDamageable interface: " + hitObject.name);
            }
        }

        // Destroy the fireball after handling the hit
        Destroy(gameObject);
    }

    // Helper method to check if a GameObject is in a specific LayerMask
    private bool IsInLayerMask(GameObject obj, LayerMask layerMask)
    {
        return ((layerMask.value & (1 << obj.layer)) > 0);
    }
}
