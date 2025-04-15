using UnityEngine;

public class Arrow : MonoBehaviour
{
    public float destroyAfterSeconds = 10f; // Time after which the arrow is destroyed
    public float speed = 10f; // Speed at which the arrow moves
    public int damage = 1; // Damage dealt by the arrow
    private Vector2 direction; // Direction the arrow is moving

    [SerializeField] private AudioSource ArrowShoot; // AudioSource component for playing arrow shoot sounds

    // Call this method to set the arrow's direction right after instantiation
    public void SetDirection(Vector2 newDirection)
    {
        direction = newDirection.normalized; // Ensure the direction vector is normalized
    }

    private void Start()
    {
        // Play the arrow shoot sound when the arrow is instantiated
        PlayArrowShootSound();
        
        // Schedule the arrow for destruction after the specified time interval
        Destroy(gameObject, destroyAfterSeconds);
    }

    private void Update()
    {
        // Move the arrow forward in the set direction
        transform.Translate(direction * speed * Time.deltaTime, Space.World);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // Check if the arrow collides with a GameObject tagged as "Player"
        if (collision.CompareTag("Player"))
        {
            PlayerHealth playerHealth = collision.GetComponent<PlayerHealth>();
            if (playerHealth != null)
            {
                bool damageApplied = playerHealth.TakeDamage(damage, "Arrow");// Deal damage to the player

                // Play the arrow hit sound only if damage was applied
                if (damageApplied && PlayerSoundManager.Instance != null)
                {
                    PlayerSoundManager.Instance.PlayArrowHitSound();
                }
                else if (!damageApplied)
                {
                    // Attack was evaded; evasion sound already played by PlayerHealth
                    // No need to play the arrow hit sound
                }
            }

            Destroy(gameObject); // Destroy the arrow upon hitting the player
        }
        // Optionally, destroy the arrow upon colliding with other objects (e.g., walls)
        else if (collision.CompareTag("Wall") || !collision.CompareTag("Archer"))
        {
            Destroy(gameObject);
        }
    }

    // Method to play the arrow shoot sound effect
    private void PlayArrowShootSound()
    {
        if (ArrowShoot != null)
        {
            ArrowShoot.Play();
        }
        else
        {
            Debug.LogWarning("ArrowShoot AudioSource is not assigned.");
        }
    }
}
