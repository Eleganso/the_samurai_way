using UnityEngine;

public class Spell1 : MonoBehaviour
{
    [SerializeField] private float speed = 12f; // Speed of the spell projectile
    [SerializeField] private float lifetime = 5f; // Lifetime of the spell before being destroyed
    public float damage = 4f; // Damage dealt by Spell1

    private Vector2 direction;

    [SerializeField] private AudioSource spell1ShootSound; // AudioSource for shooting sound

    // Initialize the spell's direction and damage
    public void Initialize(Vector2 dir, float dmg) // Changed int to float
    {
        direction = dir.normalized;
        damage = dmg;

        // Play shoot sound
        PlayShootSound();

        // Destroy the spell after the specified lifetime
        Destroy(gameObject, lifetime);
    }

    private void Update()
    {
        // Move the spell in the set direction
        transform.Translate(direction * speed * Time.deltaTime, Space.World);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // Check if the spell collides with a GameObject tagged as "Player"
        if (collision.CompareTag("Player"))
        {
            PlayerHealth playerHealth = collision.GetComponent<PlayerHealth>();
            if (playerHealth != null)
            {
                playerHealth.TakeDamage(damage, "Magic"); // Deal damage to the player
                // Optionally, play hit sound here
            }

            Destroy(gameObject); // Destroy the spell upon hitting the player
        }
        // Optionally, destroy the spell upon colliding with other objects (e.g., walls)
        else if (collision.CompareTag("Wall") || collision.CompareTag("Ground"))
        {
            Destroy(gameObject);
        }
    }

    // Method to play the Spell1 shoot sound effect
    private void PlayShootSound()
    {
        if (spell1ShootSound != null)
        {
            spell1ShootSound.Play();
        }
    }
}
