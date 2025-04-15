using UnityEngine;
using System.Collections;

public class Spell2 : MonoBehaviour
{
    [SerializeField] private float rotationSpeed = 100f; // Rotation speed of the aura
    [SerializeField] private float damage = 1f; // Damage dealt to the player on contact
    
    [SerializeField] private AudioSource auraSoundSource; // Sound for the aura

    private ElfMage mage;
    private float duration;

    // Initialize the aura with reference to the Mage and its duration
    public void Initialize(ElfMage mageInstance, float spellDuration) // Accepts float
    {
        mage = mageInstance;
        duration = spellDuration;

        // Play aura sound
        PlayAuraSound();

        // Start the coroutine to control aura's lifetime and duration
        StartCoroutine(AuraDuration());
    }

    private void Update()
    {
        // Rotate the aura around the Mage
        transform.Rotate(Vector3.forward, rotationSpeed * Time.deltaTime);
    }

    private IEnumerator AuraDuration()
    {
        yield return new WaitForSeconds(duration);
        Destroy(gameObject); // Destroy the object after the spell duration
    }

    // Animation Event method to apply damage at specific frames
    public void ApplyDamage()
    {
        Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, 1.0f); // Adjust radius as necessary
        foreach (var collider in colliders)
        {
            if (collider.CompareTag("Player"))
            {
                PlayerHealth playerHealth = collider.GetComponent<PlayerHealth>();
                if (playerHealth != null)
                {
                    playerHealth.TakeDamage(damage); // Apply damage to the player
                }
            }
        }
    }

    // Method to play the aura sound effect
    private void PlayAuraSound()
    {
        if (auraSoundSource != null)
        {
            auraSoundSource.Play();
        }
    }
}
