using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using TMPro;

/// <summary>
/// Manages the player's health, mana, damage intake, and interactions with flasks and safe zones.
/// Integrates with the PlayerManager for data persistence and flask management.
/// </summary>
public class PlayerHealth : MonoBehaviour
{
    // ===========================
    // Singleton Instance
    // ===========================
    public static PlayerHealth Instance;

    // ===========================
    // Health and Mana Variables
    // ===========================
    [Header("Health and Mana")]
    public float maxHealth = 10f; // Max health
    public float health = 10f;    // Current health

    public float maxMana = 5f;    // Max mana
    public float mana = 5f;        // Current mana

    // ===========================
    // Invulnerability
    // ===========================
    [Header("Invulnerability")]
    public float invulnerabilityDuration = 2.0f;
    private bool isInvulnerable = false;

    // New StealthSkill2-specific invulnerability flag
    private bool isStealthSkill2Invulnerable = false;

    /// <summary>
    /// Sets the StealthSkill2-specific invulnerability state.
    /// </summary>
    /// <param name="state">True to make invulnerable, false to make vulnerable.</param>
    public void SetStealthSkill2Invulnerable(bool state)
    {
        isStealthSkill2Invulnerable = state;
        Debug.Log($"StealthSkill2 Invulnerability set to: {state}");
    }

    // ===========================
    // Animator and Audio
    // ===========================
    [Header("Animator and Audio")]
    private Animator animator;
    [SerializeField] private AudioSource heroDying; // Assign in Inspector for death sound

    // ===========================
    // UI Elements
    // ===========================
    [Header("UI Elements")]
    [SerializeField] private Image healthBarFill; // Assign in Inspector
    [SerializeField] private Image manaBarFill;   // Assign in Inspector

    // ===========================
    // Safe Zone Interaction
    // ===========================
    [Header("Safe Zone")]
    private SafeZone currentSafeZone; // Current SafeZone the player is near

    // ===========================
    // References to Other Components
    // ===========================
    private Rigidbody2D rb;
    private Collider2D playerCollider;

    // ===========================
    // Awake Method for Singleton Pattern
    // ===========================
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            // Optionally, make this persistent across scenes
            // DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject); // Ensure there's only one instance
        }

        // Get references to Rigidbody2D and Collider2D
        rb = GetComponent<Rigidbody2D>();
        playerCollider = GetComponent<Collider2D>();
    }

    // ===========================
    // Start Method
    // ===========================
    private void Start()
    {
        animator = GetComponent<Animator>();

        // Initialize health and mana from PlayerManager
        InitializeHealthAndMana();

        // Update the UI to reflect current health and mana
        UpdateHealthBar();
        UpdateManaBar();

        // Handle respawn positioning if necessary
        HandleRespawnPosition();
    }

    // ===========================
    // Update Method
    // ===========================
    private void Update()
    {
        // Use HP Flask
        if (UserInput.instance.IsUseHPFlaskActionTriggered())
        {
            PlayerManager.Instance.UseHpFlask();
        }

        // Use Mana Flask
        if (UserInput.instance.IsUseMPFlaskActionTriggered())
        {
            PlayerManager.Instance.UseManaFlask();
        }
    }

    // ===========================
    // Collision Methods
    // ===========================
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("SafeZone"))
        {
            currentSafeZone = collision.GetComponent<SafeZone>();
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("SafeZone"))
        {
            currentSafeZone = null;
        }
    }

    // ===========================
    // Safe Zone Usage
    // ===========================
    /// <summary>
    /// Uses the current safe zone to replenish health, mana, and flasks.
    /// </summary>
    public void UseSafeZone()
    {
        if (currentSafeZone != null && currentSafeZone.isActive)
        {
            // Set the current SafeZone ID in GameManager
            GameManager.Instance.SetCurrentSafeZoneID(currentSafeZone.safeZoneID);
            Debug.Log("Storing SafeZone ID: " + currentSafeZone.safeZoneID);

            // Refill health and mana
            RefillHealth();
            RefillMana();
            if (UserInput.instance != null)
{
    UserInput.instance.RefreshAvailableSkills();
}

            // Replenish flasks
            PlayerManager.Instance.ReplenishFlasks();

            Debug.Log("Using SafeZone at position: " + currentSafeZone.transform.position);
            GameManager.Instance.InteractWithActiveSafeZone(); // Interact with active SafeZone
        }
    }

    // ===========================
    // Damage Handling
    // ===========================
    /// <summary>
    /// Inflicts damage to the player.
    /// </summary>
    /// <param name="damageAmount">Amount of damage to inflict.</param>
    /// <param name="attackerTag">Tag of the attacker for special skill checks.</param>
    /// <returns>True if damage was applied; false if attack was evaded.</returns>
    public bool TakeDamage(float damageAmount, string attackerTag = null)
    {
        // If already invulnerable (general or StealthSkill2), return early and do not apply damage
        if (isInvulnerable || isStealthSkill2Invulnerable) return false;

        // Check if HP Skill 1 is unlocked (5% chance to evade any attack)
        if (PlayerManager.Instance.IsSkillUnlocked("HealthSkill1"))
        {
            float evadeChance = Random.value;
            if (evadeChance <= 0.05f) // 5% chance
            {
                Debug.Log("Attack evaded due to HP Skill 1!");
                PlayerSoundManager.Instance?.PlayEvadeSound();
                return false;
            }
        }

        // Check if SpeedSkill2 is unlocked and off cooldown to evade projectiles
        if ((attackerTag == "Arrow" || attackerTag == "Magic" || attackerTag == "Shuriken") 
            && PlayerManager.Instance.CanSpeedSkill2Evade())
        {
            Debug.Log("Attack evaded due to SpeedSkill2!");
            PlayerSoundManager.Instance?.PlayEvadeSound();
            PlayerManager.Instance.TriggerSpeedSkill2EvadeCooldown();
            return false;
        }

        // Apply HP Skill 2 reduction if active
        damageAmount *= PlayerManager.Instance.DamageTakenMultiplier;

        // Cancel flask usage on taking damage
        PlayerManager.Instance.CancelFlaskUsage();

        // Apply damage
        health -= damageAmount;
        health = Mathf.Clamp(health, 0f, maxHealth);
        PlayerManager.Instance.SetHealth(health); // Update health in PlayerManager
        UpdateHealthBar();

        Debug.Log($"Player took {damageAmount} damage. Current health: {health}/{maxHealth}");

        // If Stealth Skill is active, taking damage cancels it
        if (PlayerManager.Instance.IsStealthSkillActive)
        {
            PlayerManager.Instance.DeactivateStealthSkill();
            Debug.Log("Stealth Skill cancelled due to taking damage.");
        }

        // Start Invulnerability Frames (general)
        StartCoroutine(BecomeTemporarilyInvulnerable());

        // Check for Death or StealthSkill2 Trigger
        if (health <= 0f)
        {
            // Attempt to trigger StealthSkill2 if available
            if (PlayerManager.Instance.CanTriggerStealthSkill2())
            {
                PlayerManager.Instance.TriggerStealthSkill2();
                Debug.Log("StealthSkill2 triggered, death avoided!");

                // **Set health to a minimal positive value to avoid being dead**
                SetHealth(1f);
                UpdateHealthBar();

                // Do NOT call Die() here since StealthSkill2 prevented death.
                return true; // Damage was applied, but death was avoided
            }

            // If StealthSkill2 cannot trigger, proceed with normal death
            Die();
        }

        // Damage was successfully applied
        return true;
    }

    /// <summary>
    /// Sets the player's current health.
    /// </summary>
    /// <param name="health">Health value to set.</param>
    public void SetHealth(float health)
    {
        this.health = Mathf.Clamp(health, 0, maxHealth);
        PlayerPrefs.SetFloat("PlayerCurrentHealth", this.health);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Makes the player temporarily invulnerable.
    /// </summary>
    /// <returns>IEnumerator for coroutine.</returns>
    private IEnumerator BecomeTemporarilyInvulnerable()
    {
        isInvulnerable = true;
        // Optional: Add visual feedback for invulnerability (e.g., blinking)
        yield return new WaitForSeconds(invulnerabilityDuration);
        isInvulnerable = false;
    }

    // ===========================
    // UI Update Methods
    // ===========================
    /// <summary>
    /// Updates the health bar UI element.
    /// </summary>
    public void UpdateHealthBar()
    {
        if (healthBarFill != null)
        {
            healthBarFill.fillAmount = health / maxHealth;
        }
        else
        {
            Debug.LogWarning("Health bar UI element is not assigned.");
        }
    }

    /// <summary>
    /// Updates the mana bar UI element.
    /// </summary>
    public void UpdateManaBar()
    {
        if (manaBarFill != null)
        {
            manaBarFill.fillAmount = mana / maxMana;
        }
        else
        {
            Debug.LogWarning("Mana bar UI element is not assigned.");
        }
    }

    // ===========================
    // Death and Respawn Handling
    // ===========================
    /// <summary>
    /// Handles player death by triggering death animations and initiating respawn.
    /// </summary>
    private void Die()
    {
        // Check if StealthSkill2 can trigger (avoid death)
        if (PlayerManager.Instance.CanTriggerStealthSkill2())
        {
            // Trigger StealthSkill2 instead of dying
            PlayerManager.Instance.TriggerStealthSkill2();
            Debug.Log("StealthSkill2 triggered, death avoided!");
            return; // Do not proceed with normal death logic
        }

        // If StealthSkill2 cannot trigger, proceed with normal death
        Vector2 deathPosition = transform.position;
        PlayerManager.Instance.SetPlayerDeathLocation(deathPosition);
        PlayerManager.Instance.SetPlayerDeathScene(SceneManager.GetActiveScene().name);
        Debug.Log("Player died at position: " + deathPosition);

        PlayerManager.Instance.HandleDeath();

        // Play death audio
        if (heroDying != null)
        {
            heroDying.Play();
        }

        // Disable player controls
        UserInput.instance.DisableControls();

        // Trigger death animation
        if (animator != null)
        {
            animator.SetTrigger("dead");
        }

        // Disable physics and collider
        if (rb != null)
        {
            rb.isKinematic = true;
        }
        if (playerCollider != null)
        {
            playerCollider.enabled = false;
        }

        // Start respawn coroutine
        StartCoroutine(Respawn());
    }

    /// <summary>
    /// Coroutine to handle player respawn after death.
    /// </summary>
    /// <returns>IEnumerator for coroutine.</returns>
    private IEnumerator Respawn()
    {
        // Wait for death animation and any other effects
        yield return new WaitForSeconds(2f);

        // Get the SafeZone ID to respawn at
        string safeZoneID = GameManager.Instance.GetCurrentSafeZoneID();
        Debug.Log("Respawning at SafeZone ID: " + safeZoneID);

        // Trigger GameManager's respawn logic
        GameManager.Instance.RespawnPlayer();

        // Replenish flasks based on saved data
        PlayerManager.Instance.ReplenishFlasks();
        if (UserInput.instance != null)
{
    UserInput.instance.RefreshAvailableSkills();
}

        // Reset player state
        if (animator != null)
        {
            animator.Play("Idle");
            RefillHealth();
            RefillMana();
        }
        if (rb != null)
        {
            rb.isKinematic = false;
        }
        if (playerCollider != null)
        {
            playerCollider.enabled = true;
        }

        // Enable player controls
        UserInput.instance.EnableControls();
    }

    // ===========================
    // Health and Mana Refill Methods
    // ===========================
    /// <summary>
    /// Refills the player's health to maximum.
    /// </summary>
    public void RefillHealth()
    {
        health = maxHealth;
        PlayerManager.Instance.SetHealth(health);
        UpdateHealthBar();
    }

    /// <summary>
    /// Refills the player's mana to maximum.
    /// </summary>
    public void RefillMana()
    {
        mana = maxMana;
        PlayerManager.Instance.SetMana(mana);
        UpdateManaBar();
    }

    // ===========================
    // Initialization Methods
    // ===========================
    /// <summary>
    /// Initializes health and mana values from PlayerManager.
    /// </summary>
    private void InitializeHealthAndMana()
    {
        // Get max health and current health from PlayerManager
        maxHealth = PlayerManager.Instance.maxHealth;
        health = PlayerManager.Instance.currentHealth;

        // Get max mana and current mana from PlayerManager
        maxMana = PlayerManager.Instance.maxMana;
        mana = PlayerManager.Instance.currentMana;
    }

    /// <summary>
    /// Handles player positioning upon respawn based on SafeZone.
    /// </summary>
    private void HandleRespawnPosition()
    {
        // Check if we should use the door point set by the door
        if (GameManager.Instance.ShouldUseDoorPoint())
        {
            string doorPointID = GameManager.Instance.GetDoorPointID();
            DoorPoint[] doorPoints = FindObjectsOfType<DoorPoint>();
            foreach (var dp in doorPoints)
            {
                if (dp.doorPointID == doorPointID)
                {
                    transform.position = dp.transform.position;
                    break;
                }
            }
            GameManager.Instance.ResetDoorPoint(); // Reset the flag after using the door point
        }
    }

    // ===========================
    // Health and Mana Update Methods
    // ===========================
    /// <summary>
    /// Updates the player's health based on PlayerManager's data.
    /// </summary>
    /// <summary>
/// Updates the player's health based on PlayerManager's data.
/// </summary>
public void UpdateHealthFromManager()
{
    maxHealth = PlayerManager.Instance.maxHealth; // Get the updated max health
    health = PlayerManager.Instance.GetHealth();   // Get the current health value
    UpdateHealthBar();                             // Update the health bar UI
}

    /// <summary>
    /// Updates the player's mana based on PlayerManager's data.
    /// </summary>
    public void UpdateManaFromManager()
    {
        maxMana = PlayerManager.Instance.maxMana; // Get the updated max mana
        mana = PlayerManager.Instance.GetMana();   // Get the current mana value
        UpdateManaBar();                           // Update the mana bar UI
    }

    // ===========================
    // Flask Usage Methods
    // ===========================
    #region FlaskUsage

    /// <summary>
    /// Uses an HP flask to heal the player gradually over time.
    /// </summary>
    public void UseHpFlask()
    {
        PlayerManager.Instance.UseHpFlask();
    }

    /// <summary>
    /// Uses a Mana flask to refill the player's mana gradually over time.
    /// </summary>
    public void UseManaFlask()
    {
        PlayerManager.Instance.UseManaFlask();
    }

    #endregion

    // ===========================
    // Additional Methods
    // ===========================
    #region AdditionalMethods

    /// <summary>
    /// Uses mana for a spell or ability.
    /// </summary>
    /// <param name="amount">Amount of mana to use.</param>
    public void UseMana(float amount)
    {
        mana -= amount;
        mana = Mathf.Clamp(mana, 0f, maxMana);
        PlayerManager.Instance.SetMana(mana);
        UpdateManaBar();
    }

    /// <summary>
    /// Gets the player's current mana.
    /// </summary>
    /// <returns>Current mana value.</returns>
    public float GetMana()
    {
        return mana;
    }

    #endregion
}
