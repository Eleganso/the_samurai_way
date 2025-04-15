using UnityEngine;

public class Shuriken : MonoBehaviour, IDamageable
{
    [SerializeField] private float speed = 10f;
    [SerializeField] private float damage = 1;                // Damage dealt by the shuriken
    [SerializeField] private float destroyAfterSeconds = 10f; // Time after which the shuriken is destroyed

    private Vector2 moveDirection;
    public bool HasTakenDamage { get; set; }                // Required by IDamageable

    private void Start()
    {
        // Schedule the shuriken for destruction after the specified time interval
        Destroy(gameObject, destroyAfterSeconds);
    }

    public void SetDirection(Vector2 direction)
    {
        moveDirection = direction.normalized * speed;

        // Rotate the shuriken to face the movement direction
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle);
    }

    private void Update()
    {
        // Move the shuriken in the set direction
        transform.Translate(moveDirection * Time.deltaTime, Space.World);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        Debug.Log("Shuriken collided with: " + collision.gameObject.name);

        // If the shuriken hits the player
        if (collision.CompareTag("Player"))
        {
            HandlePlayerHit(collision);
        }
        // If the shuriken hits a wall or the ground
        else if (collision.CompareTag("Wall") || collision.CompareTag("Ground"))
        {
            HandleWallOrGroundHit();
        }
       
    }

    // Handle damage logic from the player or other sources
    public void Damage(float damageAmount, GameObject damageSource)
    {
        // Play deflect sound if damage source is the player
        if (damageSource.CompareTag("Player"))
        {
            PlayerSoundManager.Instance?.PlayShurikenDeflectSound();
        }

        // Destroy the shuriken after taking damage
        Destroy(gameObject);
    }

    // Handle the logic when the shuriken hits the player
    private void HandlePlayerHit(Collider2D collision)
    {
        Debug.Log("Shuriken hit the player.");

        // Deal damage to the player
        PlayerHealth playerHealth = collision.GetComponent<PlayerHealth>();
        if (playerHealth != null)
        {
            playerHealth.TakeDamage(damage, "Shuriken");

        }
        else
        {
            Debug.LogWarning("PlayerHealth component not found on the player.");
        }

        // Play shuriken hit sound using PlayerSoundManager singleton
        PlayerSoundManager.Instance?.PlayShurikenHitSound();

        // Destroy the shuriken after hitting the player
        Destroy(gameObject);
    }

    // Handle the logic when the shuriken hits a wall or the ground
    private void HandleWallOrGroundHit()
    {
        Debug.Log("Shuriken hit a wall or the ground.");

        // Optionally, you could play a sound here or spawn some visual effects
        // PlayShurikenWallHitSound(); // Example method, if needed

        // Destroy the shuriken after hitting a wall or ground
        Destroy(gameObject);
    }

    // Handle the logic when the shuriken hits something other than the ElfAssassin
    private void HandleOtherHit()
    {
        // Optionally, play a sound or spawn visual effects
        // Debug.Log("Shuriken hit an object that's not the ElfAssassin.");

        // Destroy the shuriken after hitting any other object
        Destroy(gameObject);
    }
}
