using UnityEngine;

public class AutoAttack : MonoBehaviour
{
    [SerializeField] private float speed = 10f; // Speed of the auto-attack projectile
    [SerializeField] private float lifetime = 5f; // Lifetime of the projectile before being destroyed
    public float damage = 2f; // Damage dealt by the projectile

    private Vector2 direction;

    [SerializeField] private AudioSource autoAttackShootSound; // AudioSource for shooting sound

    // Initialize the projectile's direction and damage
    public void Initialize(Vector2 dir, float dmg) // Changed int to float
    {
        direction = dir.normalized;
        damage = dmg;

        // Play shoot sound
        PlayShootSound();

        // Destroy the projectile after the specified lifetime
        Destroy(gameObject, lifetime);
    }

    private void Update()
    {
        // Move the projectile in the set direction
        transform.Translate(direction * speed * Time.deltaTime, Space.World);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // Check if the projectile collides with a GameObject tagged as "Player"
        if (collision.CompareTag("Player"))
        {
            PlayerHealth playerHealth = collision.GetComponent<PlayerHealth>();
            if (playerHealth != null)
            {
                playerHealth.TakeDamage(damage, "Magic"); // Deal damage to the player
                // Optionally, play hit sound here
            }

            Destroy(gameObject); // Destroy the projectile upon hitting the player
        }
        // Optionally, destroy the projectile upon colliding with other objects (e.g., walls)
        else if (collision.CompareTag("Wall") || collision.CompareTag("Ground"))
        {
            Destroy(gameObject);
        }
    }

    // Method to play the auto-attack shoot sound effect
    private void PlayShootSound()
    {
        if (autoAttackShootSound != null)
        {
            autoAttackShootSound.Play();
        }
    }
}
