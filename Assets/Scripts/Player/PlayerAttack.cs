// PlayerAttack.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerAttack : MonoBehaviour
{
    [SerializeField] private Transform attackTransform;
    [SerializeField] private Transform crouchAttackTransform; // Assign in Inspector
    [SerializeField] private LayerMask attackableLayer;
    [SerializeField] private float timeBtwAttacks = 0.15f;
    public bool ShouldBeDamaging { get; private set; } = false;
    private bool canAttack = true; // Controls whether the player can attack

    private List<IDamageable> iDamageables = new List<IDamageable>();
    private Collider2D attackCollider; // Reference to the attack collider
    private Animator anim;
    private float attackTimeCounter;
    private AudioSource audioSource;
    private Player player;
    private Transform currentAttackTransform;

    private void Start()
    {
        anim = GetComponent<Animator>();
        audioSource = GetComponent<AudioSource>();
        player = GetComponent<Player>();

        // Initialize attack collider with the standing attack transform
        currentAttackTransform = attackTransform;
        attackCollider = currentAttackTransform.GetComponent<Collider2D>();

        // Disable the attack collider at the start to prevent unwanted collisions
        attackCollider.enabled = false;

        attackTimeCounter = timeBtwAttacks;
        SetAttackSpeed(PlayerManager.Instance.attackSpeed); // Initialize with current attack speed
    }

    private void Update()
{
    // Only allow attacking if canAttack is true
    if (canAttack && UserInput.instance.controls.Attack.Attack.WasPressedThisFrame() && attackTimeCounter >= timeBtwAttacks)
    {
        // Cancel StealthSkill if active
        if (PlayerManager.Instance.IsStealthSkillActive)
        {
            PlayerManager.Instance.DeactivateStealthSkill();
            Debug.Log("Stealth Skill cancelled due to attacking.");
        }

        attackTimeCounter = 0f;

        if (player.IsCrouching())
        {
            anim.SetTrigger("crouchAttack"); // Trigger crouch attack animation
            currentAttackTransform = crouchAttackTransform;
        }
        else
        {
            anim.SetTrigger("attack"); // Trigger normal attack animation
            currentAttackTransform = attackTransform;
        }

        // Update the attack collider reference
        attackCollider = currentAttackTransform.GetComponent<Collider2D>();

        audioSource.Play(); // Play the attack sound effect
    }

    attackTimeCounter += Time.deltaTime;
}


    // Method to disable attacks (e.g., when climbing ladders)
    public void DisableAttack()
    {
        canAttack = false;
    }

    // Method to enable attacks again
    public void EnableAttack()
    {
        canAttack = true;
    }

    // Coroutine to handle applying damage while the slash animation is active
    public IEnumerator DamageWhileSlashIsActive()
{
    ShouldBeDamaging = true;

    // Enable the attack collider only while dealing damage
    attackCollider.enabled = true;

    while (ShouldBeDamaging)
    {
        Collider2D[] hits = new Collider2D[10]; // Array to store detected objects
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(attackableLayer);
        filter.useTriggers = true; // Ensure triggers are included

        // Detect objects within the bounds of the attack collider
        int numHits = attackCollider.Overlap(filter, hits);

        for (int i = 0; i < numHits; i++)
        {
            IDamageable iDamageable = hits[i].GetComponent<IDamageable>();

            if (iDamageable != null && !iDamageable.HasTakenDamage)
            {
                // Attempt to get the EnemyHealth component to read actual health values
                EnemyHealth enemyHealth = hits[i].GetComponent<EnemyHealth>();

                float oldHealth = 0f;
                if (enemyHealth != null)
                {
                    oldHealth = enemyHealth.CurrentHealth;
                }

                // Apply damage logic
                // Include AttackSkillMultiplier here
                float damageAmount = PlayerManager.Instance.playerDamage * PlayerManager.Instance.AttackSkillMultiplier;

                // Check if AttackSkill1 is unlocked (for crit chance)
                if (PlayerManager.Instance.IsSkillUnlocked("AttackSkill1"))
                {
                    float chance = Random.value; // Random float between 0.0 and 1.0
                    if (chance <= 0.05f)
                    {
                        // Crit hit triggered
                        damageAmount *= 1.5f;
                        Debug.Log("Critical hit! Damage multiplied further to: " + damageAmount);
                    }
                }

                iDamageable.Damage(damageAmount, gameObject); // Pass the player GameObject as the damage source

                // If we can access the enemy's health after damage is applied, calculate actual damage dealt
                if (enemyHealth != null)
                {
                    float newHealth = enemyHealth.CurrentHealth;
                    float actualDamageDealt = oldHealth - newHealth;
                    // DEBUG: Log how much damage the player actually dealt based on enemy health difference
                    Debug.Log($"Player dealt {actualDamageDealt} damage to {hits[i].gameObject.name} (calculated from enemy health).");
                }
                else
                {
                    // If we don't have EnemyHealth, fallback to the damageAmount we used
                    Debug.Log($"Player dealt {damageAmount} damage to {hits[i].gameObject.name} (estimated from damage calculation).");
                }

                iDamageable.HasTakenDamage = true;
                iDamageables.Add(iDamageable);
            }
        }
        yield return null;
    }

    // Disable the attack collider after dealing damage
    attackCollider.enabled = false;

    ReturnAttackablesToDamageable();
}




    // Method to reset damageable objects after the attack finishes
    private void ReturnAttackablesToDamageable()
    {
        foreach (IDamageable thingThatWasDamaged in iDamageables)
        {
            thingThatWasDamaged.HasTakenDamage = false;
        }

        iDamageables.Clear();
    }

    // Method to set the attack speed dynamically (useful for upgrades or power-ups)
    public void SetAttackSpeed(float newAttackSpeed)
    {
        timeBtwAttacks = newAttackSpeed;
    }

    // This method allows visualization of the attack area in the Unity Editor
    private void OnDrawGizmosSelected()
    {
        if (currentAttackTransform == null) return;
        Gizmos.DrawWireSphere(currentAttackTransform.position, 1.5f); // Adjust based on your attack collider
    }

    #region Animation Triggers

    // Method called by animation event to allow damage
    public void ShouldBeDamagingToTrue()
    {
        ShouldBeDamaging = true;

        // Enable the attack collider when the attack should deal damage
        attackCollider.enabled = true;
    }

    // Method called by animation event to stop allowing damage
    public void ShouldBeDamagingtoFalse()
    {
        ShouldBeDamaging = false;

        // Disable the attack collider when the attack is done
        attackCollider.enabled = false;
    }

    #endregion
}
