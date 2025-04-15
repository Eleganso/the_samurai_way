// Assets/Scripts/Player/PlayerManager.cs
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using TMPro;
using System.IO;
using System.Collections.Generic; // Added for List<>

public enum SelectableSkill
{
    Fireball,        // Corresponds to Spell
    AttackSkill2,
    HPSkill2,
    MPSkill2,
    StealthSkill1,
    FireballSkill2   // If you mean the upgraded version of fireball
}

/// <summary>
/// Manages player stats, upgrades, honor points, and flask systems.
/// Implements singleton pattern to ensure only one instance exists.
/// </summary>
public class PlayerManager : MonoBehaviour
{
    // Singleton Instance
    public static PlayerManager Instance;
    private Coroutine hpFlaskCoroutine;
    private Coroutine manaFlaskCoroutine;
    public float PlayerAdjustedDodgeCooldown { get; set; } = 5f;

    // A small input debounce for skill activation
private float skillInputDebounce = 0.2f;

// Next allowed input times for each skill:
private float nextAttackSkillAllowedTime = 0f;
private float nextHPSkillAllowedTime = 0f;
private float nextMPSkillAllowedTime = 0f;
private float nextPowerSkillAllowedTime = 0f;
private float nextStealthSkillAllowedTime = 0f;  // For StealthSkill1


   // For Attack Skill 2 
private bool attackSkillActive = false;
private float attackSkillMultiplier = 1f; // default 1x damage
public float AttackSkillMultiplier => attackSkillMultiplier; // public read-only property

private float attackSkillDuration = 30f; // 30 seconds active duration
private float attackSkillCooldown = 300f; // 5 minutes = 300 seconds cooldown
private float attackSkillCooldownTimer = 0f; // tracks cooldown
private bool attackSkillOnCooldown = false;

// For HP Skill 2
private bool hpSkillActive = false;
private float hpSkillMultiplier = 1f; // default 1x (no reduction)
public float DamageTakenMultiplier => hpSkillMultiplier; // Public read-only property

private float hpSkillDuration = 30f; // 30 seconds active duration
private float hpSkillCooldown = 600f; // 10 minutes = 600 seconds
private float hpSkillCooldownTimer = 0f; // tracks cooldown
private bool hpSkillOnCooldown = false;

// For Mana Skill 2
private bool mpSkillActive = false;
private float mpSkillMultiplier = 1f; // default 1x (no reduction)
public float ManaCostMultiplier => mpSkillMultiplier; // Public read-only property

private float mpSkillDuration = 30f; // 30 seconds active duration
private float mpSkillCooldown = 600f; // 10 minutes = 600 seconds
private float mpSkillCooldownTimer = 0f; // tracks cooldown
private bool mpSkillOnCooldown = false;

// For Power Skill 2
private bool powerSkillActive = false;
private float powerSkillMultiplier = 1f; // Default normal damage
public float MagicDamageMultiplier => powerSkillMultiplier; // Public read-only property

private float powerSkillDuration = 30f; // 30 seconds
private float powerSkillCooldown = 600f; // 10 minutes
private float powerSkillCooldownTimer = 0f;
private bool powerSkillOnCooldown = false;

// For SpeedSkill2 Evasion
private bool speedSkill2EvadeOffCooldown = true; 
private float speedSkill2EvadeCooldownTime = 30f; // Evade once every 30 seconds if projectile is Arrow/Magic/Shuriken

// For StealthSkill1 
private bool stealthSkillActive = false;
private float stealthSkillDuration = 20f;
private float stealthSkillCooldown = 600f; // 10 minutes
private float stealthSkillCooldownTimer = 0f; 
private bool stealthSkillOnCooldown = false;
public bool IsStealthSkillActive => stealthSkillActive;

// For StealthSkill2
private bool stealthSkill2Active = false;
private float stealthSkill2Duration = 5f; // Player invisible for 5 seconds after avoiding death
private float stealthSkill2InvulTime = 2f; // Player invulnerable for 2 seconds before invisibility
private float stealthSkill2Cooldown = 900f; // 15 minutes = 900 seconds
private float stealthSkill2CooldownTimer = 0f;
private bool stealthSkill2OnCooldown = false;
public bool IsStealthSkill2Active => stealthSkill2Active;

// New StealthSkill2-specific invulnerability flag
private bool isStealthSkill2Invulnerable = false;

// Method to set StealthSkill2-specific invulnerability
public void SetStealthSkill2Invulnerable(bool state)
{
    isStealthSkill2Invulnerable = state;
}

   
    // ===========================
    // Health and Mana Variables
    // ===========================
    [Header("Health and Mana")]
    public float currentHealth;
    public float maxHealth = 10; // Default max health

    public float currentMana;
    public float maxMana = 5; // Default max mana

    // ===========================
    // Player Death Location
    // ===========================
    [Header("Death Tracking")]
    public Vector2 playerDeathLocation;

    // ===========================
    // Honor Points and Recovery
    // ===========================
    [Header("Honor Points")]
    [SerializeField] private TextMeshProUGUI honorPointsText; // UI element to display honor points
    [SerializeField] private GameObject honorRecoveryPrefab; // Prefab for the honor recovery object

    private int honorPoints = 0;
    private int lostHonorPoints = 0; // Track honor points lost on death
    public bool hasHonorRecoveryObject = false; // Changed to public
    private GameObject spawnedHonorRecoveryObject; // Reference to the currently spawned HonorRecovery object
    public string deathSceneName; // Changed to public

    // ===========================
    // Flask System Fields
    // ===========================
    [Header("Flask System")]
    public FlaskData flaskData;

    [Header("Flask Cooldown")]
    [SerializeField] private float flaskCooldownDuration = 2f; // Cooldown duration in seconds
    private float flaskCooldownTimer = 0f; // Timer to track cooldown
    private bool isFlaskOnCooldown = false; // Flag to indicate if cooldown is active

    // ===========================
    // Upgrade Details
    // ===========================
    [Header("Upgrade Details")]
    private int[] healthUpgradeLevels = { 2, 2, 3, 3, 4, 4, 5, 5, 6, 6 }; // Health increases per level

    private float[] damageUpgradeLevels = { 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f }; // Damage increases per level

    private float[] speedUpgradeLevels = { 0.05f, 0.05f, 0.05f, 0.05f, 0.05f, 0.05f, 0.05f, 0.05f, 0.05f, 0.05f }; // Speed reductions per level

    private int[] maxManaLevels = { 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 }; // Max mana per level
    private float[] fireballDamageLevels = { 1.5f, 2f, 2.5f, 3f, 3.5f, 4f, 4.5f, 5f, 5.5f, 6f }; // Fireball damage per level
    private float[] manaCostLevels = { 2f, 2f, 2.5f, 2.5f, 3f, 3f, 3f, 3.5f, 3.5f, 4f }; // Mana cost per spell per level

    // Stealth Upgrade Details
    private float[] hidingDelayLevels = { 2.8f, 2.6f, 2.4f, 2.2f, 2.0f, 1.8f, 1.6f, 1.4f, 1.2f, 1.0f }; // Hiding delay per level
    private float[] damageMultiplierLevels = { 1.6f, 1.7f, 1.8f, 1.9f, 2.0f, 2.1f, 2.2f, 2.3f, 2.4f, 2.5f }; // Damage multiplier per level

    private int[] upgradeCosts = { 300, 400, 550, 750, 1000, 1300, 1650, 2050, 2500, 3000 }; // Upgrade costs per level

    // Current Upgrade Levels
    public int healthLevel = 0;
    public int damageLevel = 0;
    public int speedLevel = 0;
    public int manaLevel = 0; // For max mana upgrades
    public int fireballDamageLevel = 0; // For fireball damage upgrades
    public int stealthLevel = 0; // For stealth upgrades

    // Player Stats
    public float playerDamage = 1f; // Default player damage
    public float attackSpeed = 1.4f; // Default attack speed

    public float fireballDamage = 1f; // Default fireball damage
    public float manaCostPerSpell = 2f; // Default mana cost per spell

    public float hidingDelay = 3f; // Default hiding delay
    public float damageMultiplier = 1.5f; // Default damage multiplier when hitting non-aggroed enemy

    // Skill Unlock Flags
    [Header("Skill Unlock Flags")]
    private bool healthSkill1Unlocked = false;
    private bool healthSkill2Unlocked = false;
    private bool attackSkill1Unlocked = false;
    private bool attackSkill2Unlocked = false;
    private bool speedSkill1Unlocked = false;
    private bool speedSkill2Unlocked = false;
    private bool manaSkill1Unlocked = false;
    private bool manaSkill2Unlocked = false;
    private bool fireballSkill1Unlocked = false;
    private bool fireballSkill2Unlocked = false;
    private bool stealthSkill1Unlocked = false;
    private bool stealthSkill2Unlocked = false;

    // ===========================
    // UI Elements for Flasks
    // ===========================
    [Header("Flask UI Elements")]
    [SerializeField] private TextMeshProUGUI hpFlaskCountText;   // Assign in Inspector
    [SerializeField] private TextMeshProUGUI manaFlaskCountText; // Assign in Inspector

    // ===========================
    // Save System Variables
    // ===========================
    [Header("Save System")]
    public int currentSlotNumber = 0; // To track which slot is being used

    private bool isLoadingGame = false;
    private Vector3 loadedPlayerPosition;
    
    // New variable to track if the player has the grappling hook
    public bool hasGrapplingHook = false;
    

    // ===========================
    // Awake Method for Singleton Pattern
    // ===========================
    private void Awake()
{
    if (Instance == null)
    {
        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;

        InitializePlayerStats();
        LoadPlayerStats();
        LoadSkillUnlocks();
        LoadFlaskData();
        LoadHonorPoints();
        LoadHonorRecoveryData();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.InitializeEnemyData();
        }
    }
    else if (Instance != this)
    {
        Destroy(gameObject); // Ensure only one instance exists
        Debug.LogWarning("Duplicate PlayerManager instance destroyed.");
    }
}


    private void OnDestroy()
    {
        // Unsubscribe from event when the object is destroyed
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // ===========================
    // Start Method
    // ===========================
    private void Start()
    {
        UpdateHonorPointsUI(); // Update the UI to reflect the loaded honor points
        UpdateFlaskUI(); // Update the flask UI
    }

    // ===========================
    // Scene Loaded Event Handler
    // ===========================
    /// <summary>
    /// Called when a new scene is loaded.
    /// Reassigns UI elements and updates player stats.
    /// </summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"Scene {scene.name} loaded. Reassigning UI elements and updating UI.");

        // Reassign UI elements
        AssignUIElements();

        // Update the UI elements
        UpdateHonorPointsUI();
        UpdateFlaskUI();

        // Update player health and mana UI
        PlayerHealth playerHealthComponent = FindObjectOfType<PlayerHealth>();
        if (playerHealthComponent != null)
        {
            playerHealthComponent.UpdateHealthFromManager();
            playerHealthComponent.UpdateManaFromManager();
        }
        else
        {
            Debug.LogWarning("PlayerHealth component not found in the scene.");
        }

        // Handle spawning Honor Recovery Object if necessary
        OnSceneLoadComplete();

        // Set player position if loading a game
        if (isLoadingGame)
        {
            StartCoroutine(SetPlayerPositionAfterLoad(loadedPlayerPosition));
            isLoadingGame = false; // Reset the flag after starting the coroutine
        }
    }

    /// <summary>
    /// Reassigns UI elements after a scene load.
    /// </summary>
    private void AssignUIElements()
    {
        // Find the Canvas in the scene
        GameObject canvas = GameObject.Find("CanvasHPAndDodgeAndMana");
        if (canvas != null)
        {
            // Find child game objects by name
            Transform hpFlaskTextTransform = canvas.transform.Find("HpFlaskText");
            Transform mpFlaskTextTransform = canvas.transform.Find("MpFlaskText");
            Transform honorPointsTextTransform = canvas.transform.Find("HonorPointsText");

            if (hpFlaskTextTransform != null)
            {
                hpFlaskCountText = hpFlaskTextTransform.GetComponent<TextMeshProUGUI>();
            }
            else
            {
                Debug.LogWarning("HpFlaskText not found under CanvasHPAndDodgeAndMana.");
            }

            if (mpFlaskTextTransform != null)
            {
                manaFlaskCountText = mpFlaskTextTransform.GetComponent<TextMeshProUGUI>();
            }
            else
            {
                Debug.LogWarning("MpFlaskText not found under CanvasHPAndDodgeAndMana.");
            }

            if (honorPointsTextTransform != null)
            {
                honorPointsText = honorPointsTextTransform.GetComponent<TextMeshProUGUI>();
            }
            else
            {
                Debug.LogWarning("HonorPointsText not found under CanvasHPAndDodgeAndMana.");
            }
        }
        else
        {
            Debug.LogWarning("CanvasHPAndDodgeAndMana not found in the scene.");
        }
    }

    // ===========================
    // Initialization Methods
    // ===========================
    /// <summary>
    /// Initializes player stats such as health, mana, and flask data.
    /// </summary>
    private void InitializePlayerStats()
    {
        // Set max health and current health
        maxHealth = 10; // Ensure starting health is set to 10
        currentHealth = maxHealth;

        // Set max mana and current mana
        maxMana = 5; // Default max mana
        currentMana = maxMana;

        // Set default player damage
        playerDamage = 1f; // Default player damage

        // Set default fireball damage and mana cost per spell
        fireballDamage = 1f; // Default fireball damage
        manaCostPerSpell = 2f; // Default mana cost per spell

        // Set default hiding delay and damage multiplier for stealth
        hidingDelay = 3f; // Default hiding delay
        damageMultiplier = 1.5f; // Default damage multiplier when hitting non-aggroed enemy

        // Initialize Flask Data
        flaskData = new FlaskData();
    }

    // ===========================
    // FlaskData Class
    // ===========================
    [System.Serializable]
    public class FlaskData
    {
        // ===========================
        // Flask Upgrade Levels and Amounts
        // ===========================
        public int capacityLevel = 0;
        public int hpHealingLevel = 0;
        public int manaRefillLevel = 0;

        // Healing and Refill Amounts
        public int hpHealingAmount = 5; // Starts at 5, increases with upgrades
        public int manaRefillAmount = 3; // Starts at 3, increases with upgrades

        // ===========================
        // Allocated and Current Flask Counts
        // ===========================
        public int allocatedHpFlasks = 3;
        public int allocatedManaFlasks = 2;

        public int currentHpFlasks = 3;
        public int currentManaFlasks = 2;

        // ===========================
        // Max Flasks
        // ===========================
        public int maxFlasks = 5; // Starts at 5, increases with upgrades

        // ===========================
        // Methods to Update Flask Properties
        // ===========================
        public void UpdateMaxFlasks()
        {
            maxFlasks = 5 + capacityLevel * 1; // Example: +1 per level
        }

        public void UpdateHpHealingAmount()
        {
            hpHealingAmount = 5 + (hpHealingLevel * 5); // Example: +5 per level
        }

        public void UpdateManaRefillAmount()
        {
            manaRefillAmount = 3 + manaRefillLevel * 1; // Example: +1 per level
        }

        /// <summary>
        /// Sets the flask distribution between HP and MP flasks.
        /// </summary>
        /// <param name="hpFlasks">Number of HP flasks.</param>
        /// <param name="manaFlasks">Number of MP flasks.</param>
        /// <returns>True if distribution is valid and set, else false.</returns>
        public bool SetFlaskDistribution(int hpFlasks, int manaFlasks)
        {
            if (hpFlasks + manaFlasks > maxFlasks)
            {
                Debug.LogError("Total flasks exceed maximum capacity.");
                return false;
            }

            allocatedHpFlasks = hpFlasks;
            allocatedManaFlasks = manaFlasks;

            // Reset current flasks to match new allocations
            currentHpFlasks = allocatedHpFlasks;
            currentManaFlasks = allocatedManaFlasks;

            return true;
        }

        /// <summary>
        /// Automatically distributes flasks evenly or based on specific logic.
        /// Resets current flasks to match allocated counts.
        /// </summary>
        public void AutoDistributeFlasks()
        {
            allocatedHpFlasks = maxFlasks / 2;
            allocatedManaFlasks = maxFlasks - allocatedHpFlasks;
            currentHpFlasks = allocatedHpFlasks;
            currentManaFlasks = allocatedManaFlasks;
        }

        /// <summary>
        /// Replenishes current flasks to their allocated maximums.
        /// </summary>
        public void ReplenishFlasks()
        {
            currentHpFlasks = allocatedHpFlasks;
            currentManaFlasks = allocatedManaFlasks;
        }
    }

    // ===========================
    // Flask Management Methods
    // ===========================
    #region FlaskManagement

    /// <summary>
    /// Coroutine to restore HP or Mana gradually over a set duration.
    /// </summary>
    /// <param name="type">"HP" for Health Flask, "Mana" for Mana Flask.</param>
    /// <returns>IEnumerator for coroutine.</returns>
    private IEnumerator RestoreFlaskEffect(string type)
    {
        int totalRestores = 2; // Number of increments
        float interval = 1f; // Time between each increment in seconds
        float restoreAmount = 0f;

        if (type == "HP")
        {
            restoreAmount = flaskData.hpHealingAmount / totalRestores;
        }
        else if (type == "Mana")
        {
            restoreAmount = flaskData.manaRefillAmount / totalRestores;
        }
        else
        {
            Debug.LogError("Invalid flask type for restoration.");
            yield break;
        }

        for (int i = 0; i < totalRestores; i++)
        {
            yield return new WaitForSeconds(interval);

            // Check if the coroutine has been canceled
            if ((type == "HP" && hpFlaskCoroutine == null) || (type == "Mana" && manaFlaskCoroutine == null))
            {
                Debug.Log($"{type} Flask restoration was cancelled.");
                yield break;
            }

            if (type == "HP")
            {
                currentHealth += restoreAmount;
                currentHealth = Mathf.Min(currentHealth, maxHealth);
                SetHealth(currentHealth); // Update PlayerManager's health
                PlayerHealth.Instance.UpdateHealthFromManager(); // Update UI
                Debug.Log($"Restored {restoreAmount} HP. Current Health: {currentHealth}/{maxHealth}");
            }
            else if (type == "Mana")
            {
                currentMana += restoreAmount;
                currentMana = Mathf.Min(currentMana, maxMana);
                SetMana(currentMana); // Update PlayerManager's mana
                PlayerHealth.Instance.UpdateManaFromManager(); // Update UI
                Debug.Log($"Restored {restoreAmount} Mana. Current Mana: {currentMana}/{maxMana}");
            }
        }

        Debug.Log($"{type} restoration complete.");
    }

    /// <summary>
    /// Starts the flask usage cooldown.
    /// </summary>
    private void StartFlaskCooldown()
    {
        isFlaskOnCooldown = true;
        flaskCooldownTimer = flaskCooldownDuration;
        Debug.Log($"Flask cooldown started for {flaskCooldownDuration} seconds.");
    }

    /// <summary>
    /// Resets flask upgrades to level 0 and sets default flask counts.
    /// </summary>
    public void ResetFlaskUpgrades()
    {
        flaskData.capacityLevel = 0;
        flaskData.hpHealingLevel = 0;
        flaskData.manaRefillLevel = 0;
        flaskData.UpdateMaxFlasks();
        flaskData.UpdateHpHealingAmount();
        flaskData.UpdateManaRefillAmount();
        flaskData.allocatedHpFlasks = 3; // Reset to default allocation
        flaskData.allocatedManaFlasks = 2; // Reset to default allocation
        flaskData.currentHpFlasks = flaskData.allocatedHpFlasks; // Reset current counts
        flaskData.currentManaFlasks = flaskData.allocatedManaFlasks; // Reset current counts

        SaveFlaskData();
        UpdateFlaskUI();

        Debug.Log("Flask upgrades have been reset to level 0 and counts set to default.");
    }

    /// <summary>
    /// Upgrades flask capacity by increasing the max number of flasks.
    /// </summary>
    public void UpgradeFlaskCapacity()
    {
        if (flaskData.capacityLevel < 5)
        {
            flaskData.capacityLevel++;
            flaskData.UpdateMaxFlasks();
            Debug.Log($"Flask capacity upgraded to level {flaskData.capacityLevel}. Max flasks: {flaskData.maxFlasks}");
            SaveFlaskData(); // Save after upgrade

            // Optionally, redistribute flasks if necessary
            flaskData.AutoDistributeFlasks();
            SaveFlaskData();
            UpdateFlaskUI();
        }
        else
        {
            Debug.Log("Flask capacity is already at maximum level.");
        }
    }

    /// <summary>
    /// Upgrades HP flask healing amount.
    /// </summary>
    public void UpgradeHpFlask()
    {
        if (flaskData.hpHealingLevel < 5)
        {
            flaskData.hpHealingLevel++;
            flaskData.UpdateHpHealingAmount();
            Debug.Log($"HP Flask healing upgraded to level {flaskData.hpHealingLevel}. Healing amount: {flaskData.hpHealingAmount}");
            SaveFlaskData(); // Save after upgrade
        }
        else
        {
            Debug.Log("HP Flask healing is already at maximum level.");
        }
    }

    /// <summary>
    /// Upgrades Mana flask refill amount.
    /// </summary>
    public void UpgradeManaFlask()
    {
        if (flaskData.manaRefillLevel < 5)
        {
            flaskData.manaRefillLevel++;
            flaskData.UpdateManaRefillAmount();
            Debug.Log($"Mana Flask refill upgraded to level {flaskData.manaRefillLevel}. Refill amount: {flaskData.manaRefillAmount}");
            SaveFlaskData(); // Save after upgrade
        }
        else
        {
            Debug.Log("Mana Flask refill is already at maximum level.");
        }
    }

    /// <summary>
    /// Sets the flask distribution between HP and MP flasks.
    /// </summary>
    /// <param name="hpFlasks">Number of HP flasks.</param>
    /// <param name="manaFlasks">Number of MP flasks.</param>
    /// <returns>True if distribution is valid and set, else false.</returns>
    public bool SetFlaskDistribution(int hpFlasks, int manaFlasks)
    {
        bool success = flaskData.SetFlaskDistribution(hpFlasks, manaFlasks);
        if (success)
        {
            SaveFlaskData();
            UpdateFlaskUI();
            Debug.Log($"Flask distribution set. Allocated HP Flasks: {flaskData.allocatedHpFlasks}, Allocated Mana Flasks: {flaskData.allocatedManaFlasks}");
        }
        else
        {
            Debug.LogError("Failed to set flask distribution due to exceeding max capacity.");
        }
        return success;
    }

    /// <summary>
    /// Replenishes flasks by resetting current counts to allocated maximums.
    /// </summary>
    public void ReplenishFlasks()
    {
        flaskData.ReplenishFlasks();
        UpdateFlaskUI();
        SaveFlaskData();

        Debug.Log("Flasks have been replenished to their allocated counts.");
    }

    /// <summary>
    /// Saves flask data to PlayerPrefs.
    /// </summary>
    public void SaveFlaskData()
    {
        PlayerPrefs.SetInt("FlaskCapacityLevel", flaskData.capacityLevel);
        PlayerPrefs.SetInt("HpHealingLevel", flaskData.hpHealingLevel);
        PlayerPrefs.SetInt("ManaRefillLevel", flaskData.manaRefillLevel);

        // Save Allocated Counts
        PlayerPrefs.SetInt("AllocatedHpFlasks", flaskData.allocatedHpFlasks);
        PlayerPrefs.SetInt("AllocatedManaFlasks", flaskData.allocatedManaFlasks);

        // Save Current Counts
        PlayerPrefs.SetInt("CurrentHpFlasks", flaskData.currentHpFlasks);
        PlayerPrefs.SetInt("CurrentManaFlasks", flaskData.currentManaFlasks);

        PlayerPrefs.SetInt("MaxFlasks", flaskData.maxFlasks);

        // Save Cooldown State
        PlayerPrefs.SetFloat("FlaskCooldownTimer", flaskCooldownTimer);
        PlayerPrefs.SetInt("IsFlaskOnCooldown", isFlaskOnCooldown ? 1 : 0);

        PlayerPrefs.Save();
    }

    /// <summary>
    /// Loads flask data from PlayerPrefs.
    /// </summary>
    public void LoadFlaskData()
    {
        flaskData.capacityLevel = PlayerPrefs.GetInt("FlaskCapacityLevel", 0);
        flaskData.hpHealingLevel = PlayerPrefs.GetInt("HpHealingLevel", 0);
        flaskData.manaRefillLevel = PlayerPrefs.GetInt("ManaRefillLevel", 0);

        // Load Allocated Counts
        flaskData.allocatedHpFlasks = PlayerPrefs.GetInt("AllocatedHpFlasks", 3);
        flaskData.allocatedManaFlasks = PlayerPrefs.GetInt("AllocatedManaFlasks", 2);

        // Load Current Counts
        flaskData.currentHpFlasks = PlayerPrefs.GetInt("CurrentHpFlasks", flaskData.allocatedHpFlasks);
        flaskData.currentManaFlasks = PlayerPrefs.GetInt("CurrentManaFlasks", flaskData.allocatedManaFlasks);

        flaskData.UpdateMaxFlasks();
        flaskData.UpdateHpHealingAmount();
        flaskData.UpdateManaRefillAmount();

        // Ensure that current flask counts do not exceed allocated
        if (flaskData.currentHpFlasks > flaskData.allocatedHpFlasks || flaskData.currentManaFlasks > flaskData.allocatedManaFlasks)
        {
            flaskData.currentHpFlasks = flaskData.allocatedHpFlasks;
            flaskData.currentManaFlasks = flaskData.allocatedManaFlasks;
        }

        // Load Cooldown State
        flaskCooldownTimer = PlayerPrefs.GetFloat("FlaskCooldownTimer", 0f);
        isFlaskOnCooldown = PlayerPrefs.GetInt("IsFlaskOnCooldown", 0) == 1;

        if (isFlaskOnCooldown)
        {
            Debug.Log($"Flask is on cooldown. Timer: {flaskCooldownTimer} seconds remaining.");
        }

        SaveFlaskData(); // Save to ensure consistency
        UpdateFlaskUI();

        Debug.Log("Flask data loaded successfully.");
    }

    /// <summary>
    /// Updates the Flask UI elements to reflect current counts.
    /// </summary>
    public void UpdateFlaskUI()
    {
        if (hpFlaskCountText != null)
        {
            hpFlaskCountText.text = flaskData.currentHpFlasks.ToString();
        }
        else
        {
            Debug.LogWarning("HP Flask Count Text is not assigned.");
        }

        if (manaFlaskCountText != null)
        {
            manaFlaskCountText.text = flaskData.currentManaFlasks.ToString();
        }
        else
        {
            Debug.LogWarning("Mana Flask Count Text is not assigned.");
        }
    }

    #endregion

    // ===========================
    // Flask Usage Methods
    // ===========================
    #region FlaskUsage

    /// <summary>
    /// Uses an HP flask to heal the player gradually over time.
    /// </summary>
    public void UseHpFlask()
    {
        if (isFlaskOnCooldown)
        {
            Debug.Log("Flask is on cooldown. Please wait.");
            return;
        }

        if (flaskData.currentHpFlasks > 0 && currentHealth < maxHealth)
        {
            flaskData.currentHpFlasks--;
            UpdateFlaskUI();
            SaveFlaskData();

            // Start the cooldown
            StartFlaskCooldown();

            // Start the gradual restoration coroutine and store the reference
            hpFlaskCoroutine = StartCoroutine(RestoreFlaskEffect("HP"));

            Debug.Log($"Used HP Flask. HP Flasks remaining: {flaskData.currentHpFlasks}");

            // **Trigger Flask Animation and Sound**
            Player player = FindObjectOfType<Player>();
            if (player != null)
            {
                player.PlayUseFlaskAnimation();
            }
            else
            {
                Debug.LogWarning("Player instance not found in the scene.");
            }

            PlayerSoundManager.Instance?.PlayFlaskUseSound();
        }
        else
        {
            Debug.Log("No HP flasks left or health is full.");
        }
    }

    /// <summary>
    /// Uses a Mana flask to refill the player's mana gradually over time.
    /// </summary>
    public void UseManaFlask()
    {
        if (isFlaskOnCooldown)
        {
            Debug.Log("Flask is on cooldown. Please wait.");
            return;
        }

        if (flaskData.currentManaFlasks > 0 && currentMana < maxMana)
        {
            flaskData.currentManaFlasks--;
            UpdateFlaskUI();
            SaveFlaskData();

            // Start the cooldown
            StartFlaskCooldown();

            // Start the gradual restoration coroutine and store the reference
            manaFlaskCoroutine = StartCoroutine(RestoreFlaskEffect("Mana"));

            Debug.Log($"Used Mana Flask. Mana Flasks remaining: {flaskData.currentManaFlasks}");

            // **Trigger Flask Animation and Sound**
            Player player = FindObjectOfType<Player>();
            if (player != null)
            {
                player.PlayUseFlaskAnimation();
            }
            else
            {
                Debug.LogWarning("Player instance not found in the scene.");
            }

            PlayerSoundManager.Instance?.PlayFlaskUseSound();
        }
        else
        {
            Debug.Log("No Mana flasks left or mana is full.");
        }
    }

    /// <summary>
    /// Cancels any ongoing flask usage by stopping coroutines, animations, and sounds.
    /// Re-enables player controls.
    /// </summary>
    public void CancelFlaskUsage()
    {
        Debug.Log("Cancelling flask usage...");

        // Stop HP Flask Coroutine if running
        if (hpFlaskCoroutine != null)
        {
            StopCoroutine(hpFlaskCoroutine);
            hpFlaskCoroutine = null;
            Debug.Log("HP Flask restoration coroutine stopped.");
        }

        // Stop Mana Flask Coroutine if running
        if (manaFlaskCoroutine != null)
        {
            StopCoroutine(manaFlaskCoroutine);
            manaFlaskCoroutine = null;
            Debug.Log("Mana Flask restoration coroutine stopped.");
        }

        // Reset flask cooldown
        if (isFlaskOnCooldown)
        {
            isFlaskOnCooldown = false;
            flaskCooldownTimer = 0f;
            UpdateFlaskUI();
            Debug.Log("Flask cooldown reset.");
        }

        // **Play Idle Animation Instead of Stopping Flask Animation**
        Player player = FindObjectOfType<Player>();
        if (player != null)
        {
            Animator playerAnimator = player.GetComponent<Animator>();
            if (playerAnimator != null)
            {
                playerAnimator.Play("Idle"); // Ensure "Idle" matches your Animator state's name
                Debug.Log("Player Idle animation played.");
            }
            else
            {
                Debug.LogWarning("Player Animator not found.");
            }
        }
        else
        {
            Debug.LogWarning("Player instance not found in the scene.");
        }

        // **Stop Flask Use Sound**
        PlayerSoundManager.Instance?.StopFlaskUseSound();

        // Re-enable Player Controls
        UserInput.instance.EnableControls();
//        Debug.Log("Player controls re-enabled after flask cancellation.");
    }

    #endregion

    // ===========================
    // Health and Mana Upgrade Methods
    // ===========================
    #region HealthManaUpgrades

    /// <summary>
    /// Upgrades the player's maximum health.
    /// </summary>
    /// <param name="level">The level to upgrade to.</param>
    public void UpgradeHealth(int level)
    {
        if (healthLevel < level && level <= healthUpgradeLevels.Length)
        {
            // Calculate the current health percentage before the upgrade
            float healthPercentage = (float)currentHealth / maxHealth;

            // Upgrade max health
            maxHealth += healthUpgradeLevels[level - 1];
            healthLevel = level; // Set the new health level

            // Adjust current health to maintain the same health percentage
            currentHealth = Mathf.RoundToInt(maxHealth * healthPercentage);
            PlayerPrefs.SetFloat("PlayerMaxHealth", maxHealth);
            PlayerPrefs.SetFloat("PlayerCurrentHealth", currentHealth);
            PlayerPrefs.Save();

            Debug.Log("Health upgraded to level: " + healthLevel + ", new max health: " + maxHealth);

            // Delegate UI update to PlayerHealth
            PlayerHealth.Instance.UpdateHealthFromManager();
        }
        else
        {
            Debug.Log("Health level not available or max health level reached.");
        }
    }

    /// <summary>
    /// Upgrades the player's damage.
    /// </summary>
    /// <param name="level">The level to upgrade to.</param>
    public void UpgradeDamage(int level)
    {
        if (damageLevel < level && level <= damageUpgradeLevels.Length)
        {
            playerDamage += damageUpgradeLevels[level - 1];
            damageLevel = level; // Set the new damage level
            Debug.Log("Damage upgraded to level: " + damageLevel + ", new damage: " + playerDamage);
        }
        else
        {
            Debug.Log("Damage level not available or max damage level reached.");
        }
    }

    /// <summary>
    /// Upgrades the player's attack speed.
    /// </summary>
    /// <param name="level">The level to upgrade to.</param>
    public void UpgradeSpeed(int level)
    {
        if (speedLevel < level && level <= speedUpgradeLevels.Length)
        {
            attackSpeed -= speedUpgradeLevels[level - 1];
            speedLevel = level; // Set the new speed level
            Debug.Log("Speed upgraded to level: " + speedLevel + ", new attack speed: " + attackSpeed);

            // Update the player attack speed
            PlayerAttack playerAttack = FindObjectOfType<PlayerAttack>();
            if (playerAttack != null)
            {
                playerAttack.SetAttackSpeed(attackSpeed);
            }
        }
        else
        {
            Debug.Log("Speed level not available or max speed level reached.");
        }
    }

    /// <summary>
    /// Upgrades the player's maximum mana.
    /// </summary>
    /// <param name="level">The level to upgrade to.</param>
    public void UpgradeMana(int level)
    {
        if (manaLevel < level && level <= maxManaLevels.Length)
        {
            // Calculate the current mana percentage before the upgrade
            float manaPercentage = (float)currentMana / maxMana;

            // Upgrade max mana
            maxMana = maxManaLevels[level - 1];
            manaLevel = level; // Set the new mana level

            // Adjust current mana to maintain the same mana percentage
            currentMana = Mathf.RoundToInt(maxMana * manaPercentage);
            PlayerPrefs.SetFloat("PlayerMaxMana", maxMana);
            PlayerPrefs.SetFloat("PlayerCurrentMana", currentMana);
            PlayerPrefs.Save();

            Debug.Log("Mana upgraded to level: " + manaLevel + ", new max mana: " + maxMana);

            // Delegate UI update to PlayerHealth
            PlayerHealth.Instance.UpdateManaFromManager();
        }
        else
        {
            Debug.Log("Mana level not available or max mana level reached.");
        }
    }

    /// <summary>
    /// Upgrades the player's fireball damage and mana cost.
    /// </summary>
    /// <param name="level">The level to upgrade to.</param>
    public void UpgradeFireballDamage(int level)
    {
        if (fireballDamageLevel < level && level <= fireballDamageLevels.Length)
        {
            fireballDamage = fireballDamageLevels[level - 1];
            manaCostPerSpell = manaCostLevels[level - 1];
            fireballDamageLevel = level; // Set the new fireball damage level

            Debug.Log($"Fireball Damage upgraded to level: {fireballDamageLevel}, new damage: {fireballDamage}, new mana cost: {manaCostPerSpell}");
        }
        else
        {
            Debug.Log("Fireball Damage level not available or max level reached.");
        }
    }

    /// <summary>
    /// Upgrades the player's stealth abilities.
    /// </summary>
    /// <param name="level">The level to upgrade to.</param>
    public void UpgradeStealth(int level)
    {
        if (stealthLevel < level && level <= hidingDelayLevels.Length)
        {
            hidingDelay = hidingDelayLevels[level - 1];
            damageMultiplier = damageMultiplierLevels[level - 1];
            stealthLevel = level; // Set the new stealth level

            Debug.Log($"Stealth upgraded to level: {stealthLevel}, new hiding delay: {hidingDelay}, new damage multiplier: {damageMultiplier}");
        }
        else
        {
            Debug.Log("Stealth level not available or max level reached.");
        }
    }

    #endregion

    // ===========================
    // Skill Unlock Methods
    // ===========================
    #region SkillUnlocking

    /// <summary>
    /// Unlocks a skill based on its name and required level.
    /// </summary>
    /// <param name="skillName">The name of the skill to unlock.</param>
    /// <param name="skillLevel">The level required to unlock the skill.</param>
    public void UnlockSkill(string skillName, int skillLevel)
{
    if (IsSkillUnlocked(skillName))
    {
        Debug.Log("Skill already unlocked: " + skillName);
        return;
    }

    bool requirementsMet = false;

    switch (skillName)
    {
        case "HealthSkill1":
            if (healthLevel >= 5)
            {
                healthSkill1Unlocked = true;
                requirementsMet = true;
                Debug.Log("Health Skill 1 unlocked!");
            }
            break;
        case "HealthSkill2":
            if (healthLevel >= 10)
            {
                healthSkill2Unlocked = true;
                requirementsMet = true;
                Debug.Log("Health Skill 2 unlocked!");
            }
            break;
        case "AttackSkill1":
            if (damageLevel >= 5)
            {
                attackSkill1Unlocked = true;
                requirementsMet = true;
                Debug.Log("Attack Skill 1 unlocked!");
            }
            break;
        case "AttackSkill2":
            if (damageLevel >= 10)
            {
                attackSkill2Unlocked = true;
                requirementsMet = true;
                Debug.Log("Attack Skill 2 unlocked!");
            }
            break;
        case "SpeedSkill1":
            if (speedLevel >= 5)
            {
                speedSkill1Unlocked = true;
                requirementsMet = true;
                Debug.Log("Speed Skill 1 unlocked!");
            }
            break;
        case "SpeedSkill2":
            if (speedLevel >= 10)
            {
                speedSkill2Unlocked = true;
                requirementsMet = true;
                Debug.Log("Speed Skill 2 unlocked!");
            }
            break;
        case "ManaSkill1":
            if (manaLevel >= 5)
            {
                manaSkill1Unlocked = true;
                requirementsMet = true;
                Debug.Log("Mana Skill 1 unlocked!");
            }
            break;
        case "ManaSkill2":
            if (manaLevel >= 10)
            {
                manaSkill2Unlocked = true;
                requirementsMet = true;
                Debug.Log("Mana Skill 2 unlocked!");
            }
            break;
        case "FireballSkill1":
            if (fireballDamageLevel >= 5)
            {
                fireballSkill1Unlocked = true;
                requirementsMet = true;
                Debug.Log("Fireball Skill 1 unlocked!");
            }
            break;
        case "FireballSkill2":
            if (fireballDamageLevel >= 10)
            {
                fireballSkill2Unlocked = true;
                requirementsMet = true;
                Debug.Log("Fireball Skill 2 unlocked!");
            }
            break;
        case "StealthSkill1":
            if (stealthLevel >= 5)
            {
                stealthSkill1Unlocked = true;
                requirementsMet = true;
                Debug.Log("Stealth Skill 1 unlocked!");
            }
            break;
        case "StealthSkill2":
            if (stealthLevel >= 10)
            {
                stealthSkill2Unlocked = true;
                requirementsMet = true;
                Debug.Log("Stealth Skill 2 unlocked!");
            }
            break;
        default:
            Debug.Log("Unknown skill name: " + skillName);
            break;
    }

    if (requirementsMet)
    {
        // Save the unlocked skill state
        PlayerPrefs.SetInt(skillName, 1);
        PlayerPrefs.Save();
        Debug.Log($"Skill '{skillName}' has been saved as unlocked.");

        // >>> ADD THIS <<<
        if (UserInput.instance != null)
        {
            UserInput.instance.RefreshAvailableSkills();
            Debug.Log("Skill UI refreshed after unlocking a new skill.");
        }
        else
        {
            Debug.LogWarning("UserInput.instance is null. Skill UI was not refreshed.");
        }
    }
    else
    {
        Debug.Log($"Requirements not met to unlock skill: {skillName}");
    }
}



    /// <summary>
    /// Checks if a skill is already unlocked.
    /// </summary>
    /// <param name="skillName">The name of the skill to check.</param>
    /// <returns>True if unlocked, else false.</returns>
    public bool IsSkillUnlocked(string skillName)
    {
        switch (skillName)
        {
            case "HealthSkill1":
                return healthSkill1Unlocked;
            case "HealthSkill2":
                return healthSkill2Unlocked;
            case "AttackSkill1":
                return attackSkill1Unlocked;
            case "AttackSkill2":
                return attackSkill2Unlocked;
            case "SpeedSkill1":
                return speedSkill1Unlocked;
            case "SpeedSkill2":
                return speedSkill2Unlocked;
            case "ManaSkill1":
                return manaSkill1Unlocked;
            case "ManaSkill2":
                return manaSkill2Unlocked;
            case "FireballSkill1":
                return fireballSkill1Unlocked;
            case "FireballSkill2":
                return fireballSkill2Unlocked;
            case "StealthSkill1":
                return stealthSkill1Unlocked;
            case "StealthSkill2":
                return stealthSkill2Unlocked;
            default:
                return false;
        }
    }

    /// <summary>
    /// Loads skill unlock states from PlayerPrefs.
    /// </summary>
    private void LoadSkillUnlocks()
    {
        healthSkill1Unlocked = PlayerPrefs.GetInt("HealthSkill1", 0) == 1;
        healthSkill2Unlocked = PlayerPrefs.GetInt("HealthSkill2", 0) == 1;
        attackSkill1Unlocked = PlayerPrefs.GetInt("AttackSkill1", 0) == 1;
        attackSkill2Unlocked = PlayerPrefs.GetInt("AttackSkill2", 0) == 1;
        speedSkill1Unlocked = PlayerPrefs.GetInt("SpeedSkill1", 0) == 1;
        speedSkill2Unlocked = PlayerPrefs.GetInt("SpeedSkill2", 0) == 1;
        manaSkill1Unlocked = PlayerPrefs.GetInt("ManaSkill1", 0) == 1;
        manaSkill2Unlocked = PlayerPrefs.GetInt("ManaSkill2", 0) == 1;
        fireballSkill1Unlocked = PlayerPrefs.GetInt("FireballSkill1", 0) == 1;
        fireballSkill2Unlocked = PlayerPrefs.GetInt("FireballSkill2", 0) == 1;
        stealthSkill1Unlocked = PlayerPrefs.GetInt("StealthSkill1", 0) == 1;
        stealthSkill2Unlocked = PlayerPrefs.GetInt("StealthSkill2", 0) == 1;
    }

    #endregion

    // ===========================
    // Save and Load Player Stats
    // ===========================
    #region SaveLoadPlayerStats

    /// <summary>
    /// Saves player upgrade levels to PlayerPrefs.
    /// </summary>
    private void SavePlayerStats()
{
    PlayerPrefs.SetInt("HealthLevel", healthLevel);
    PlayerPrefs.SetInt("DamageLevel", damageLevel);
    PlayerPrefs.SetInt("SpeedLevel", speedLevel);
    PlayerPrefs.SetInt("ManaLevel", manaLevel);
    PlayerPrefs.SetInt("FireballDamageLevel", fireballDamageLevel);
    PlayerPrefs.SetInt("StealthLevel", stealthLevel);
    PlayerPrefs.Save();
    Debug.Log("[PlayerManager] Player stats saved.");
}


    /// <summary>
    /// Loads player upgrade levels from PlayerPrefs.
    /// </summary>
    private void LoadPlayerStats()
{
    healthLevel = PlayerPrefs.GetInt("HealthLevel", 0);
    damageLevel = PlayerPrefs.GetInt("DamageLevel", 0);
    speedLevel = PlayerPrefs.GetInt("SpeedLevel", 0);
    manaLevel = PlayerPrefs.GetInt("ManaLevel", 0);
    fireballDamageLevel = PlayerPrefs.GetInt("FireballDamageLevel", 0);
    stealthLevel = PlayerPrefs.GetInt("StealthLevel", 0);
    // ... other loading code
}


    #endregion

    // ===========================
    // Health and Mana Set/Get Methods
    // ===========================
    #region HealthManaSetGet

    /// <summary>
    /// Sets the player's current health.
    /// </summary>
    /// <param name="health">Health value to set.</param>
    public void SetHealth(float health)
    {
        currentHealth = Mathf.Clamp(health, 0, maxHealth);
        PlayerPrefs.SetFloat("PlayerCurrentHealth", currentHealth);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Gets the player's current health.
    /// </summary>
    /// <returns>Current health value.</returns>
    public float GetHealth()
    {
        return currentHealth;
    }

    /// <summary>
    /// Sets the player's current mana.
    /// </summary>
    /// <param name="mana">Mana value to set.</param>
    public void SetMana(float mana)
    {
        currentMana = Mathf.Clamp(mana, 0, maxMana);
        PlayerPrefs.SetFloat("PlayerCurrentMana", currentMana);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Gets the player's current mana.
    /// </summary>
    /// <returns>Current mana value.</returns>
    public float GetMana()
    {
        return currentMana;
    }

    #endregion

    // ===========================
    // Player Death and Respawn Methods
    // ===========================
    #region DeathRespawn

    /// <summary>
    /// Sets the player's death location.
    /// </summary>
    /// <param name="location">Death location vector.</param>
    public void SetPlayerDeathLocation(Vector2 location)
    {
        playerDeathLocation = location;
        Debug.Log($"Player death location set to: {location}");
    }

    /// <summary>
    /// Gets the player's death location.
    /// </summary>
    /// <returns>Death location vector.</returns>
    public Vector2 GetPlayerDeathLocation()
    {
        return playerDeathLocation;
    }

    /// <summary>
    /// Sets the scene name where the player died.
    /// </summary>
    /// <param name="sceneName">Scene name.</param>
    public void SetPlayerDeathScene(string sceneName)
    {
        deathSceneName = sceneName;
        Debug.Log($"Player death scene set to: {deathSceneName}");
    }

    /// <summary>
    /// Handles player death by resetting honor points and tracking recovery.
    /// </summary>
    public void HandleDeath()
    {
        Vector2 deathPosition = GetPlayerDeathLocation();
        string currentScene = SceneManager.GetActiveScene().name;
        SetPlayerDeathScene(currentScene);
        Debug.Log("Player died at position: " + deathPosition + " in scene: " + currentScene);

        lostHonorPoints = honorPoints;
        honorPoints = 0;
        UpdateHonorPointsUI();

        TrackHonorRecovery(deathPosition, lostHonorPoints);
        Debug.Log("HandleDeath called. Position passed to TrackHonorRecovery: " + deathPosition);
    }

    /// <summary>
    /// Recovers lost honor points by 70%.
    /// </summary>
    public void RecoverLostHonorPoints()
    {
        int pointsToRecover = Mathf.FloorToInt(lostHonorPoints * 0.70f); // Recover 70% of lost honor points
        AddHonorPoints(pointsToRecover);
        lostHonorPoints = 0;
        hasHonorRecoveryObject = false;

        if (spawnedHonorRecoveryObject != null)
        {
            Destroy(spawnedHonorRecoveryObject); // Destroy the recovery object after recovery
            spawnedHonorRecoveryObject = null;
        }

        SaveHonorRecoveryData();
        Debug.Log($"Recovered {pointsToRecover} honor points. Total honor points: {honorPoints}");
    }

    #endregion

    // ===========================
    // Honor Points Methods
    // ===========================
    #region HonorPoints

    /// <summary>
    /// Adds honor points to the player.
    /// </summary>
    /// <param name="points">Points to add.</param>
    public void AddHonorPoints(int points)
    {
        honorPoints += points;
        UpdateHonorPointsUI();
        SaveHonorPoints();
        Debug.Log($"Added {points} honor points. Total honor points: {honorPoints}");
    }

    /// <summary>
    /// Deducts honor points from the player.
    /// </summary>
    /// <param name="points">Points to deduct.</param>
    public void DeductHonorPoints(int points)
    {
        honorPoints -= points;
        honorPoints = Mathf.Max(honorPoints, 0);
        UpdateHonorPointsUI();
        SaveHonorPoints();
        Debug.Log($"Deducted {points} honor points. Total honor points: {honorPoints}");
    }

    /// <summary>
    /// Gets the player's current honor points.
    /// </summary>
    /// <returns>Honor points.</returns>
    public int GetHonorPoints()
    {
        return honorPoints;
    }

    /// <summary>
    /// Sets the player's honor points.
    /// </summary>
    /// <param name="points">Honor points to set.</param>
    public void SetHonorPoints(int points)
    {
        honorPoints = Mathf.Max(points, 0);
        UpdateHonorPointsUI();
        SaveHonorPoints();
        Debug.Log($"Set honor points to: {honorPoints}");
    }

    /// <summary>
    /// Updates the Honor Points UI element.
    /// </summary>
    public void UpdateHonorPointsUI()
    {
        if (honorPointsText != null)
        {
            honorPointsText.text = honorPoints.ToString();
        }
        else
        {
            Debug.LogWarning("Honor points text UI element is not assigned.");
        }
    }

    /// <summary>
    /// Saves honor points to PlayerPrefs.
    /// </summary>
    public void SaveHonorPoints()
    {
        PlayerPrefs.SetInt("HonorPoints", honorPoints);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Loads honor points from PlayerPrefs.
    /// </summary>
    public void LoadHonorPoints()
    {
        if (PlayerPrefs.HasKey("HonorPoints"))
        {
            honorPoints = PlayerPrefs.GetInt("HonorPoints");
        }
        else
        {
            honorPoints = 0; // Start with 0 if no saved honor points exist
        }
        UpdateHonorPointsUI(); // Update the UI to reflect the loaded honor points
        Debug.Log($"Loaded honor points: {honorPoints}");
    }

    #endregion

    // ===========================
    // Honor Recovery Methods
    // ===========================
    #region HonorRecovery

    /// <summary>
    /// Saves honor recovery data to PlayerPrefs.
    /// </summary>
    public void SaveHonorRecoveryData()
    {
        PlayerPrefs.SetFloat("HonorRecoveryPosX", playerDeathLocation.x);
        PlayerPrefs.SetFloat("HonorRecoveryPosY", playerDeathLocation.y);
        PlayerPrefs.SetInt("HonorRecoveryPoints", lostHonorPoints);
        PlayerPrefs.SetInt("HasHonorRecoveryObject", hasHonorRecoveryObject ? 1 : 0);
        PlayerPrefs.SetString("DeathSceneName", deathSceneName);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Loads honor recovery data from PlayerPrefs.
    /// </summary>
    public void LoadHonorRecoveryData()
    {
        if (PlayerPrefs.GetInt("HasHonorRecoveryObject", 0) == 1)
        {
            float posX = PlayerPrefs.GetFloat("HonorRecoveryPosX", 0);
            float posY = PlayerPrefs.GetFloat("HonorRecoveryPosY", 0);
            lostHonorPoints = PlayerPrefs.GetInt("HonorRecoveryPoints", 0);
            playerDeathLocation = new Vector2(posX, posY);
            deathSceneName = PlayerPrefs.GetString("DeathSceneName", "");
            hasHonorRecoveryObject = true;
            Debug.Log("HonorRecovery data loaded, checking for spawn condition...");
            if (SceneManager.GetActiveScene().name == deathSceneName)
            {
                SpawnHonorRecoveryObject();
            }
        }
        else
        {
            Debug.Log("No HonorRecovery object found to spawn on load.");
        }
    }

    /// <summary>
    /// Sets the honor recovery flag.
    /// </summary>
    /// <param name="flag">True to enable honor recovery, false to disable.</param>
    public void SetHonorRecoveryFlag(bool flag)
    {
        hasHonorRecoveryObject = flag;
        if (!flag && spawnedHonorRecoveryObject != null)
        {
            Destroy(spawnedHonorRecoveryObject); // Remove the spawned object if the flag is set to false
            spawnedHonorRecoveryObject = null;
        }
        Debug.Log($"Honor recovery flag set to {flag}");
    }

    /// <summary>
    /// Spawns the honor recovery object at the player's death location.
    /// </summary>
    private void SpawnHonorRecoveryObject()
    {
        if (!hasHonorRecoveryObject)
        {
            Debug.Log("HonorRecovery object spawn not required.");
            return;
        }

        if (SceneManager.GetActiveScene().name != deathSceneName)
        {
            Debug.Log("Not spawning HonorRecovery object because current scene is not the death scene.");
            return;
        }

        Debug.Log("Honor Recovery Prefab Assigned: " + (honorRecoveryPrefab != null));
        Debug.Log("Honor Recovery Position Before Spawning: " + playerDeathLocation);

        if (honorRecoveryPrefab != null)
        {
            if (spawnedHonorRecoveryObject != null)
            {
                Destroy(spawnedHonorRecoveryObject);
            }

            spawnedHonorRecoveryObject = Instantiate(honorRecoveryPrefab, playerDeathLocation, Quaternion.identity);

            if (spawnedHonorRecoveryObject != null)
            {
                Debug.Log("Honor Recovery Object successfully spawned at position: " + playerDeathLocation);
                HonorRecovery recoveryScript = spawnedHonorRecoveryObject.GetComponent<HonorRecovery>();
                if (recoveryScript != null)
                {
                    recoveryScript.SetHonorPoints(lostHonorPoints);
                }
            }
            else
            {
                Debug.LogError("Honor Recovery Object failed to spawn.");
            }
        }
        else
        {
            Debug.LogWarning("Honor Recovery Object could not be spawned. Check prefab assignment.");
        }
    }

    /// <summary>
    /// Tracks honor recovery after player death.
    /// </summary>
    /// <param name="position">Death position.</param>
    /// <param name="lostHonorPoints">Lost honor points.</param>
    private void TrackHonorRecovery(Vector2 position, int lostHonorPoints)
    {
        Debug.Log("TrackHonorRecovery called with position: " + position);

        playerDeathLocation = position;
        this.lostHonorPoints = lostHonorPoints;

        hasHonorRecoveryObject = true;

        SaveHonorRecoveryData();
    }

    /// <summary>
    /// Handles actions after scene load is complete.
    /// </summary>
    public void OnSceneLoadComplete()
    {
        Debug.Log("Scene load complete, checking if HonorRecovery object should spawn...");
        if (hasHonorRecoveryObject && SceneManager.GetActiveScene().name == deathSceneName)
        {
            StartCoroutine(DelayedSpawnHonorRecoveryObject());
        }
    }

    /// <summary>
    /// Delays the spawning of the HonorRecovery object to ensure all components are loaded.
    /// </summary>
    /// <returns>IEnumerator for coroutine.</returns>
    public IEnumerator DelayedSpawnHonorRecoveryObject() // Changed to public
    {
        Debug.Log("Starting delay before spawning HonorRecovery object...");
        yield return new WaitForSeconds(0.5f);
        Debug.Log("Spawning HonorRecovery object after delay.");
        SpawnHonorRecoveryObject();
    }

    #endregion

    // ===========================
    // Safe Zone Honor Deduction
    // ===========================
    #region SafeZoneHonorDeduction

    /// <summary>
    /// Deducts honor points by 10% when using a safe zone.
    /// </summary>
    public void DeductHonorPointsForSafeZone()
    {
        int pointsToDeduct = Mathf.FloorToInt(honorPoints * 0.10f); // Deduct 10% of current honor points
        DeductHonorPoints(pointsToDeduct); // This method should be called only once
        Debug.Log($"10% of honor points ({pointsToDeduct}) deducted for using a safe zone.");
    }

    #endregion

    // ===========================
    // Application Quit
    // ===========================
    #region ApplicationQuit

    /// <summary>
    /// Saves all necessary data when the application quits.
    /// </summary>
    private void OnApplicationQuit()
    {
        SaveHonorPoints();
        SaveFlaskData();
        SaveHonorRecoveryData();
        SavePlayerStats(); // Save upgrade levels
        SaveGame(); // Save the game
        Debug.Log("All player data saved on application quit.");
    }

    #endregion

    // ===========================
    // Additional Methods
    // ===========================
    #region AdditionalMethods

    // Placeholder for additional methods related to flask upgrades, health, mana, etc.
    // Ensure that any new methods are properly integrated with saving/loading and UI updates.

    #endregion

    // ===========================
    // Update Player Stats Method
    // ===========================
    /// <summary>
    /// Updates all player stats based on current upgrade levels.
    /// This method should be called whenever upgrades are applied.
    /// </summary>
    public void UpdatePlayerStats()
    {
        // Reset stats to base values
        maxHealth = 10;
        playerDamage = 1f;
        attackSpeed = 1.4f;
        maxMana = 5;
        fireballDamage = 1f;
        manaCostPerSpell = 2f;
        hidingDelay = 3f;
        damageMultiplier = 1.5f;

        // Apply Health Upgrades
        if (healthLevel > 0)
        {
            for (int i = 0; i < healthLevel && i < healthUpgradeLevels.Length; i++)
            {
                maxHealth += healthUpgradeLevels[i];
            }

            // Adjust current health to maintain percentage
            float healthPercentage = (float)currentHealth / maxHealth;
            currentHealth = Mathf.RoundToInt(maxHealth * healthPercentage);
            PlayerPrefs.SetFloat("PlayerMaxHealth", maxHealth);
            PlayerPrefs.SetFloat("PlayerCurrentHealth", currentHealth);
        }

        // Apply Damage Upgrades
        if (damageLevel > 0)
        {
            for (int i = 0; i < damageLevel && i < damageUpgradeLevels.Length; i++)
            {
                playerDamage += damageUpgradeLevels[i];
            }
        }

        // Apply Speed Upgrades
        if (speedLevel > 0)
        {
            for (int i = 0; i < speedLevel && i < speedUpgradeLevels.Length; i++)
            {
                attackSpeed -= speedUpgradeLevels[i];
            }
        }

        // Apply Mana Upgrades
        if (manaLevel > 0 && manaLevel <= maxManaLevels.Length)
        {
            maxMana = maxManaLevels[manaLevel - 1];
            // Adjust current mana to maintain percentage
            float manaPercentage = (float)currentMana / maxMana;
            currentMana = Mathf.RoundToInt(maxMana * manaPercentage);
            PlayerPrefs.SetFloat("PlayerMaxMana", maxMana);
            PlayerPrefs.SetFloat("PlayerCurrentMana", currentMana);
        }

        // Apply Fireball Damage Upgrades
        if (fireballDamageLevel > 0 && fireballDamageLevel <= fireballDamageLevels.Length)
        {
            fireballDamage = fireballDamageLevels[fireballDamageLevel - 1];
            manaCostPerSpell = manaCostLevels[fireballDamageLevel - 1];
        }

        // Apply Stealth Upgrades
        if (stealthLevel > 0 && stealthLevel <= hidingDelayLevels.Length)
        {
            hidingDelay = hidingDelayLevels[stealthLevel - 1];
            damageMultiplier = damageMultiplierLevels[stealthLevel - 1];
        }

        // Save all updated stats
        PlayerPrefs.Save();

        // Update PlayerAttack component with new attack speed
        PlayerAttack playerAttack = FindObjectOfType<PlayerAttack>();
        if (playerAttack != null)
        {
            playerAttack.SetAttackSpeed(attackSpeed);
        }

        // Delegate UI update to PlayerHealth
        PlayerHealth playerHealthComponent = FindObjectOfType<PlayerHealth>();
        if (playerHealthComponent != null)
        {
            playerHealthComponent.UpdateHealthFromManager();
            playerHealthComponent.UpdateManaFromManager();
        }

        Debug.Log("Player stats have been updated based on current upgrade levels.");
        // After applying all upgrades and skill unlocks, set an adjusted dodge cooldown
    float adjustedDodgeCooldown = 5f; // Default to 5 seconds if SpeedSkill1 is not unlocked
    if (IsSkillUnlocked("SpeedSkill1"))
    {
        adjustedDodgeCooldown = 4f; // Set to 4 seconds if SpeedSkill1 is unlocked
    }

    // Store this adjustedDodgeCooldown in a public field or property so Player can access it
    PlayerAdjustedDodgeCooldown = adjustedDodgeCooldown;

    PlayerPrefs.Save();
    Debug.Log("Player stats have been updated based on current upgrade levels.");
    }

    // ===========================
    // Save System Methods
    // ===========================
    #region SaveSystem

    /// <summary>
    /// Creates a SaveData object with the current game state.
    /// </summary>
    /// <returns>SaveData object.</returns>
    public SaveData CreateSaveData()
    {
        SaveData data = new SaveData();

        // This method is now redundant because we have CollectPlayerData()
        // You can remove this method if not used elsewhere.

        return data;
    }

    /// <summary>
    /// Saves the game to the current slot, including safe zone states.
    /// </summary>
    public void SaveGame()
    {
        // Save enemy states before creating save data
        GameManager.Instance.SaveEnemyStates();

        // Create a new SaveData object
        SaveData data = new SaveData();

        // **Collect Player Data**
        CollectPlayerData(data);

        // Serialize safeZoneStates
        data.safeZoneStates = new List<SafeZoneState>();
        foreach (var kvp in GameManager.Instance.safeZoneStates)
        {
            SafeZoneState state = new SafeZoneState
            {
                safeZoneID = kvp.Key,
                isActive = kvp.Value
            };
            data.safeZoneStates.Add(state);
        }

        // Serialize portcullisStates
        data.portcullisStates = new List<PortcullisState>();
        foreach (var kvp in GameManager.Instance.portcullisStates)
        {
            PortcullisState state = new PortcullisState
            {
                portcullisID = kvp.Key,
                isOpen = kvp.Value
            };
            data.portcullisStates.Add(state);
        }

        // Serialize collected pickups
        data.collectedPickups = new List<string>(GameManager.Instance.collectedPickups);

        // Serialize permaDeadEnemies
        data.permaDeadEnemies = new List<string>(GameManager.Instance.permaDeadEnemies);

        // Serialize enemies
        data.enemies = new List<EnemyData>();
        foreach (var kvp in GameManager.Instance.enemyHealth)
        {
            EnemyData enemyData = new EnemyData
            {
                enemyID = kvp.Key,
                currentHealth = kvp.Value,
                isDead = GameManager.Instance.IsEnemyDead(kvp.Key),
                respawnable = GameManager.Instance.IsEnemyRespawnable(kvp.Key)
            };
            data.enemies.Add(enemyData);
        }

        // Serialize other game data as needed...

        // Save the data using SaveSystem
        SaveSystem.SaveGame(data, currentSlotNumber);
        Debug.Log($"Game saved in slot {currentSlotNumber}");
    }

    /// <summary>
    /// Loads a game from the specified slot, including safe zone states.
    /// </summary>
    /// <param name="slotNumber">1-based slot number.</param>
    public void LoadGame(int slotNumber)
    {
        SaveData data = SaveSystem.LoadGame(slotNumber);
        if (data != null)
        {
            currentSlotNumber = slotNumber;

            // Apply Player Data
            ApplyPlayerData(data);

            // Load enemy data
            GameManager.Instance.enemyHealth.Clear();
            GameManager.Instance.deadEnemies.Clear();
            GameManager.Instance.permaDeadEnemies.Clear();

            if (data.enemies != null)
            {
                foreach (var enemyData in data.enemies)
                {
                    GameManager.Instance.enemyHealth[enemyData.enemyID] = enemyData.currentHealth;
                    if (enemyData.isDead)
                    {
                        GameManager.Instance.MarkEnemyAsDead(enemyData.enemyID, enemyData.respawnable);
                    }
                }
            }

            // Load permaDeadEnemies
            if (data.permaDeadEnemies != null)
            {
                GameManager.Instance.permaDeadEnemies = new HashSet<string>(data.permaDeadEnemies);
            }

            // Load safe zone data
            GameManager.Instance.safeZoneStates.Clear();
            if (data.safeZoneStates != null)
            {
                foreach (var state in data.safeZoneStates)
                {
                    GameManager.Instance.safeZoneStates.Add(state.safeZoneID, state.isActive);
                }
            }

            // Load portcullis data
            GameManager.Instance.portcullisStates.Clear();
            if (data.portcullisStates != null)
            {
                foreach (var state in data.portcullisStates)
                {
                    GameManager.Instance.portcullisStates.Add(state.portcullisID, state.isOpen);
                }
            }

            // Load collected pickups
            GameManager.Instance.collectedPickups.Clear();
            if (data.collectedPickups != null)
            {
                GameManager.Instance.collectedPickups = new HashSet<string>(data.collectedPickups);
            }

            // Initialize safe zones and portcullises based on loaded states
            GameManager.Instance.InitializeSafeZones();
            GameManager.Instance.InitializePortcullises();

            // Load the scene
            isLoadingGame = true;
            loadedPlayerPosition = data.playerPosition;
            SceneManager.LoadScene(data.sceneName);
        }
        else
        {
            Debug.LogWarning($"No save data found in slot {slotNumber}");
        }
    }

    /// <summary>
    /// Collects player data into the SaveData object.
    /// </summary>
    /// <param name="data">SaveData object to populate.</param>
    public void CollectPlayerData(SaveData data)
    {
        // Scene and position
        data.sceneName = SceneManager.GetActiveScene().name;
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            data.playerPosition = player.transform.position;
        }
        else
        {
            Debug.LogWarning("Player GameObject not found. Player position will not be saved.");
            data.playerPosition = Vector3.zero;
        }

        // Health and Mana
        data.currentHealth = currentHealth;
        data.maxHealth = maxHealth;
        data.currentMana = currentMana;
        data.maxMana = maxMana;

        // Honor Points
        data.honorPoints = honorPoints;
        data.lostHonorPoints = lostHonorPoints;
        data.hasHonorRecoveryObject = hasHonorRecoveryObject;
        data.playerDeathLocation = playerDeathLocation;
        data.deathSceneName = deathSceneName;

        // Upgrades
        data.healthLevel = healthLevel;
        data.damageLevel = damageLevel;
        data.speedLevel = speedLevel;
        data.manaLevel = manaLevel;
        data.fireballDamageLevel = fireballDamageLevel;
        data.stealthLevel = stealthLevel;

        // Flask Data
        data.flaskData = flaskData;

        // Skill Unlocks
        data.healthSkill1Unlocked = healthSkill1Unlocked;
        data.healthSkill2Unlocked = healthSkill2Unlocked;
        data.attackSkill1Unlocked = attackSkill1Unlocked;
        data.attackSkill2Unlocked = attackSkill2Unlocked;
        data.speedSkill1Unlocked = speedSkill1Unlocked;
        data.speedSkill2Unlocked = speedSkill2Unlocked;
        data.manaSkill1Unlocked = manaSkill1Unlocked;
        data.manaSkill2Unlocked = manaSkill2Unlocked;
        data.fireballSkill1Unlocked = fireballSkill1Unlocked;
        data.fireballSkill2Unlocked = fireballSkill2Unlocked;
        data.stealthSkill1Unlocked = stealthSkill1Unlocked;
        data.stealthSkill2Unlocked = stealthSkill2Unlocked;
        data.hasGrapplingHook = hasGrapplingHook;

        // Note: Collected pickups are managed by GameManager and SaveSystem, so they are not collected here.
    }

    // ===========================
    // Apply Player Data Method
    // ===========================
    /// <summary>
    /// Applies loaded player data from the SaveData object.
    /// </summary>
    /// <param name="data">SaveData object containing loaded data.</param>
    public void ApplyPlayerData(SaveData data)
    {
        // Update PlayerManager data
        currentHealth = data.currentHealth;
        maxHealth = data.maxHealth;
        currentMana = data.currentMana;
        maxMana = data.maxMana;
        honorPoints = data.honorPoints;
        lostHonorPoints = data.lostHonorPoints;
        hasHonorRecoveryObject = data.hasHonorRecoveryObject;
        playerDeathLocation = data.playerDeathLocation;
        deathSceneName = data.deathSceneName;
        healthLevel = data.healthLevel;
        damageLevel = data.damageLevel;
        speedLevel = data.speedLevel;
        manaLevel = data.manaLevel;
        fireballDamageLevel = data.fireballDamageLevel;
        stealthLevel = data.stealthLevel;

        // Flask Data
        flaskData = data.flaskData;

        // Skill Unlocks
        healthSkill1Unlocked = data.healthSkill1Unlocked;
        healthSkill2Unlocked = data.healthSkill2Unlocked;
        attackSkill1Unlocked = data.attackSkill1Unlocked;
        attackSkill2Unlocked = data.attackSkill2Unlocked;
        speedSkill1Unlocked = data.speedSkill1Unlocked;
        speedSkill2Unlocked = data.speedSkill2Unlocked;
        manaSkill1Unlocked = data.manaSkill1Unlocked;
        manaSkill2Unlocked = data.manaSkill2Unlocked;
        fireballSkill1Unlocked = data.fireballSkill1Unlocked;
        fireballSkill2Unlocked = data.fireballSkill2Unlocked;
        stealthSkill1Unlocked = data.stealthSkill1Unlocked;
        stealthSkill2Unlocked = data.stealthSkill2Unlocked;
        hasGrapplingHook = data.hasGrapplingHook;

        // Update player stats based on loaded upgrade levels
        UpdatePlayerStats();

        // Update UI elements
        UpdateHonorPointsUI();
        UpdateFlaskUI();

        // Update Player Health and Mana UI
        PlayerHealth playerHealthComponent = FindObjectOfType<PlayerHealth>();
        if (playerHealthComponent != null)
        {
            playerHealthComponent.UpdateHealthFromManager();
            playerHealthComponent.UpdateManaFromManager();
        }
        else
        {
            Debug.LogWarning("PlayerHealth component not found in the scene.");
        }

        // Set the flag and position for loading
        isLoadingGame = true;
        loadedPlayerPosition = data.playerPosition;

        Debug.Log("Player data applied successfully from loaded SaveData.");
        if (UserInput.instance != null)
{
    UserInput.instance.RefreshAvailableSkills();
}
    }

    #endregion

    // ===========================
    // Update Method
    // ===========================
    private void Update()
    {
        // Handle Flask Cooldown
        if (isFlaskOnCooldown)
        {
            flaskCooldownTimer -= Time.deltaTime;
            if (flaskCooldownTimer <= 0f)
            {
                isFlaskOnCooldown = false;
                flaskCooldownTimer = 0f;
                Debug.Log("Flask cooldown ended.");
                UpdateFlaskUI(); // Update UI to reflect that flasks can be used again
            }
        }

        // **Detect Shift Key Press to Cancel Flask Usage**
        // Listen for both Left Shift and Right Shift keys
        if (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift))
        {
            // Check if any flask usage coroutine is currently running
            if (hpFlaskCoroutine != null || manaFlaskCoroutine != null)
            {
                CancelFlaskUsage();
                Debug.Log("Flask usage canceled by pressing Shift key.");
            }
        }
    }

    /// <summary>
    /// Resets all game data for a new game.
    /// </summary>
    public void ResetAllData()
    {
        // Reset health and mana
        maxHealth = 10;
        currentHealth = maxHealth;
        maxMana = 5;
        currentMana = maxMana;

        // Reset honor points
        honorPoints = 0;
        lostHonorPoints = 0;
        hasHonorRecoveryObject = false;
        playerDeathLocation = Vector2.zero;
        deathSceneName = "";

        // Reset upgrades
        healthLevel = 0;
        damageLevel = 0;
        speedLevel = 0;
        manaLevel = 0;
        fireballDamageLevel = 0;
        stealthLevel = 0;

        // Reset flask data
        flaskData = new FlaskData();
        flaskData.allocatedHpFlasks = 3;
        flaskData.allocatedManaFlasks = 2;
        flaskData.currentHpFlasks = flaskData.allocatedHpFlasks;
        flaskData.currentManaFlasks = flaskData.allocatedManaFlasks;

        // Reset skill unlocks
        healthSkill1Unlocked = false;
        healthSkill2Unlocked = false;
        attackSkill1Unlocked = false;
        attackSkill2Unlocked = false;
        speedSkill1Unlocked = false;
        speedSkill2Unlocked = false;
        manaSkill1Unlocked = false;
        manaSkill2Unlocked = false;
        fireballSkill1Unlocked = false;
        fireballSkill2Unlocked = false;
        stealthSkill1Unlocked = false;
        stealthSkill2Unlocked = false;

        // Clear PlayerPrefs
        PlayerPrefs.DeleteAll();

        // Update stats and UI
        UpdatePlayerStats();
        UpdateFlaskUI();
        UpdateHonorPointsUI();

        Debug.Log("All game data has been reset for a new game.");
    }

    private IEnumerator SetPlayerPositionAfterLoad(Vector3 position)
    {
        yield return null; // Wait for the next frame to ensure the scene is fully loaded
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            player.transform.position = position;
            Debug.Log("Player position set to loaded position: " + position);
        }
        else
        {
            Debug.LogWarning("Player not found after loading scene.");
        }
    }

    public void UnlockGrapplingHook()
{
    hasGrapplingHook = true;
    SavePlayerStats(); // Save the updated state
    Debug.Log("Grappling Hook unlocked!");
}

    // ===========================
    // AttemptGrapple Method
    // ===========================
    /// <summary>
    /// Attempts to perform a grapple action.
    /// </summary>
    public void AttemptGrapple()
{
    Debug.Log("AttemptGrapple() called in PlayerManager.");

    // Find the Player component
    Player player = FindObjectOfType<Player>();
    if (player != null)
    {
        Debug.Log("Player component found. Initiating grapple.");
        player.TurnCheck(); // Ensure player is facing the correct direction
        player.AttemptGrapple(); // Delegate to Player.cs
    }
    else
    {
        Debug.LogError("Player component not found. Cannot perform grapple.");
    }
}

/// <summary>
/// Attempts to activate AttackSkill if AttackSkill2 is unlocked and off cooldown.
/// </summary>
public void TryActivateAttackSkill()
{
    // 1. Input Debounce
    if (Time.time < nextAttackSkillAllowedTime)
    {
       // Debug.Log("AttackSkill2 input locked. Wait for debounce.");
        return;
    }
    nextAttackSkillAllowedTime = Time.time + skillInputDebounce;

    // 2. Normal checks
    if (!IsSkillUnlocked("AttackSkill2"))
    {
        Debug.Log("AttackSkill2 not unlocked. Cannot activate skill.");
        return;
    }
    if (attackSkillOnCooldown)
    {
        Debug.Log("Attack Skill 2 is on cooldown. Please wait.");
        return;
    }
    if (attackSkillActive)
    {
        Debug.Log("Attack Skill 2 is already active. Cannot activate again.");
        return;
    }

    // 3. Activate
    StartCoroutine(ActivateAttackSkill());
}


private IEnumerator ActivateAttackSkill()
{
    // Activate
    attackSkillActive = true;
    attackSkillMultiplier = 2f; // Example effect
    Debug.Log("Attack Skill 2 activated! Damage doubled for 30 seconds.");

    yield return new WaitForSeconds(attackSkillDuration);

    // Deactivate
    attackSkillActive = false;
    attackSkillMultiplier = 1f;
    Debug.Log("Attack Skill 2 ended. Damage returned to normal.");

    // Start cooldown
    attackSkillOnCooldown = true;
    attackSkillCooldownTimer = attackSkillCooldown;
    Debug.Log($"Attack Skill 2 cooldown started for {attackSkillCooldown} seconds.");

    while (attackSkillCooldownTimer > 0f)
    {
        attackSkillCooldownTimer -= Time.deltaTime;
        yield return null;
    }

    // Cooldown ended
    attackSkillOnCooldown = false;
    attackSkillCooldownTimer = 0f;
    Debug.Log("Attack Skill 2 is ready to be used again.");
}


public void TryActivateHPSkill()
{
    // 1. Input Debounce
    if (Time.time < nextHPSkillAllowedTime)
    {
       // Debug.Log("HP Skill 2 input locked. Wait for debounce.");
        return;
    }
    nextHPSkillAllowedTime = Time.time + skillInputDebounce;

    // 2. Normal checks
    if (!IsSkillUnlocked("HealthSkill2"))
    {
        Debug.Log("HP Skill 2 not unlocked. Cannot activate skill.");
        return;
    }
    if (hpSkillOnCooldown)
    {
        Debug.Log("HP Skill 2 is on cooldown. Please wait.");
        return;
    }
    if (hpSkillActive)
    {
        Debug.Log("HP Skill 2 is already active. Cannot activate again.");
        return;
    }

    // 3. Activate
    StartCoroutine(ActivateHPSkill());
}


private IEnumerator ActivateHPSkill()
{
    hpSkillActive = true;
    hpSkillMultiplier = 0.5f; // Example effect: half damage taken
    Debug.Log("HP Skill 2 activated! Player takes half damage for 30 seconds.");

    yield return new WaitForSeconds(hpSkillDuration);

    hpSkillActive = false;
    hpSkillMultiplier = 1f;
    Debug.Log("HP Skill 2 ended. Damage taken returned to normal.");

    hpSkillOnCooldown = true;
    hpSkillCooldownTimer = hpSkillCooldown;
    Debug.Log($"HP Skill 2 cooldown started for {hpSkillCooldown} seconds.");

    while (hpSkillCooldownTimer > 0f)
    {
        hpSkillCooldownTimer -= Time.deltaTime;
        yield return null;
    }

    hpSkillOnCooldown = false;
    hpSkillCooldownTimer = 0f;
    Debug.Log("HP Skill 2 is ready to be used again.");
}


public void TryActivateMPSkill()
{
    // 1. Input Debounce
    if (Time.time < nextMPSkillAllowedTime)
    {
       // Debug.Log("MP Skill 2 input locked. Wait for debounce.");
        return;
    }
    nextMPSkillAllowedTime = Time.time + skillInputDebounce;

    // 2. Normal checks
    if (!IsSkillUnlocked("ManaSkill2"))
    {
        Debug.Log("MP Skill 2 not unlocked. Cannot activate skill.");
        return;
    }
    if (mpSkillOnCooldown)
    {
        Debug.Log("MP Skill 2 is on cooldown. Please wait.");
        return;
    }
    if (mpSkillActive)
    {
        Debug.Log("MP Skill 2 is already active. Cannot activate again.");
        return;
    }

    // 3. Activate
    StartCoroutine(ActivateMPSkill());
}


private IEnumerator ActivateMPSkill()
{
    mpSkillActive = true;
    mpSkillMultiplier = 0.5f; // Example effect: half mana cost
    Debug.Log("MP Skill 2 activated! Player's mana costs halved for 30 seconds.");

    yield return new WaitForSeconds(mpSkillDuration);

    mpSkillActive = false;
    mpSkillMultiplier = 1f;
    Debug.Log("MP Skill 2 ended. Mana costs returned to normal.");

    mpSkillOnCooldown = true;
    mpSkillCooldownTimer = mpSkillCooldown;
    Debug.Log($"MP Skill 2 cooldown started for {mpSkillCooldown} seconds.");

    while (mpSkillCooldownTimer > 0f)
    {
        mpSkillCooldownTimer -= Time.deltaTime;
        yield return null;
    }

    mpSkillOnCooldown = false;
    mpSkillCooldownTimer = 0f;
    Debug.Log("MP Skill 2 is ready to be used again.");
}


public void TryActivatePowerSkill()
{
    // 1. Input Debounce
    if (Time.time < nextPowerSkillAllowedTime)
    {
       // Debug.Log("FireballSkill2 input locked. Wait for debounce.");
        return;
    }
    nextPowerSkillAllowedTime = Time.time + skillInputDebounce;

    // 2. Normal checks
    if (!IsSkillUnlocked("FireballSkill2"))
    {
        Debug.Log("FireballSkill2 not unlocked. Cannot activate skill.");
        return;
    }
    if (powerSkillOnCooldown)
    {
        Debug.Log("FireballSkill2 is on cooldown. Please wait.");
        return;
    }
    if (powerSkillActive)
    {
        Debug.Log("FireballSkill2 is already active. Cannot activate again.");
        return;
    }

    // 3. Activate
    StartCoroutine(ActivatePowerSkill());
}


private IEnumerator ActivatePowerSkill()
{
    powerSkillActive = true;
    powerSkillMultiplier = 2f; // Double spell damage
    Debug.Log("Power Skill (FireballSkill2) activated! Spell damage doubled for 30 seconds.");

    // Wait 30 seconds
    yield return new WaitForSeconds(powerSkillDuration);

    // Revert changes
    powerSkillActive = false;
    powerSkillMultiplier = 1f;
    Debug.Log("Power Skill (FireballSkill2) ended. Spell damage returned to normal.");

    // Start cooldown
    powerSkillOnCooldown = true;
    powerSkillCooldownTimer = powerSkillCooldown;
    Debug.Log($"Power Skill (FireballSkill2) cooldown started for {powerSkillCooldown} seconds.");

    while (powerSkillCooldownTimer > 0f)
    {
        powerSkillCooldownTimer -= Time.deltaTime;
        yield return null;
    }

    powerSkillOnCooldown = false;
    powerSkillCooldownTimer = 0f;
    Debug.Log("Power Skill (FireballSkill2) is ready again.");
}

public bool CanSpeedSkill2Evade()
{
    return IsSkillUnlocked("SpeedSkill2") && speedSkill2EvadeOffCooldown;
}

public void TriggerSpeedSkill2EvadeCooldown()
{
    if (!IsSkillUnlocked("SpeedSkill2")) return;
    speedSkill2EvadeOffCooldown = false;
    StartCoroutine(SpeedSkill2EvadeCooldownRoutine());
}

private IEnumerator SpeedSkill2EvadeCooldownRoutine()
{
    yield return new WaitForSeconds(speedSkill2EvadeCooldownTime);
    speedSkill2EvadeOffCooldown = true;
    Debug.Log("SpeedSkill2 evasion ready again.");
}
/// <summary>
/// Attempts to activate StealthSkill if StealthSkill1 is unlocked and off cooldown.
/// </summary>
public void TryActivateStealthSkill()
{
    // 1. Input Debounce
    if (Time.time < nextStealthSkillAllowedTime)
    {
       // Debug.Log("StealthSkill1 input locked. Wait for debounce.");
        return;
    }
    nextStealthSkillAllowedTime = Time.time + skillInputDebounce;

    // 2. Normal checks
    if (!IsSkillUnlocked("StealthSkill1"))
    {
        Debug.Log("StealthSkill1 not unlocked. Cannot activate skill.");
        return;
    }
    if (stealthSkillOnCooldown)
    {
        Debug.Log("Stealth Skill 1 is on cooldown. Please wait.");
        return;
    }
    if (stealthSkillActive)
    {
        Debug.Log("Stealth Skill 1 is already active. Cannot activate again.");
        return;
    }

    // 3. Activate
    StartCoroutine(ActivateStealthSkill());
}


private IEnumerator ActivateStealthSkill()
{
    stealthSkillActive = true;
    Debug.Log("Stealth Skill 1 activated! Player is now partially invisible for 20 seconds.");

    Player player = FindObjectOfType<Player>();
    if (player != null)
    {
        player.SetTransparency(0.3f);
        player.SetLayerOverride("HiddenPlayer");
    }

    // Wait for the duration
    float elapsed = 0f;
    while (elapsed < stealthSkillDuration && stealthSkillActive)
    {
        elapsed += Time.deltaTime;
        yield return null;
    }

    // If still active after full duration, deactivate
    if (stealthSkillActive)
    {
        DeactivateStealthSkill();
    }

    // Start cooldown
    stealthSkillOnCooldown = true;
    stealthSkillCooldownTimer = stealthSkillCooldown;
    while (stealthSkillCooldownTimer > 0f)
    {
        stealthSkillCooldownTimer -= Time.deltaTime;
        yield return null;
    }

    stealthSkillOnCooldown = false;
    Debug.Log("Stealth Skill 1 is ready again.");
}

public void DeactivateStealthSkill()
{
    if (stealthSkillActive)
    {
        stealthSkillActive = false;

        Player player = FindObjectOfType<Player>();
        if (player != null)
        {
            player.SetTransparency(1f);
            player.SetLayerOverride("Player");
        }
        Debug.Log("Stealth Skill 1 ended. Player returned to normal state.");
    }
}



/// <summary>
/// Checks if StealthSkill2 can be triggered when the player would normally die.
/// </summary>
public bool CanTriggerStealthSkill2()
{
    return IsSkillUnlocked("StealthSkill2") && !stealthSkill2OnCooldown && !stealthSkill2Active;
}

/// <summary>
/// Triggers StealthSkill2 effect when the player would die, granting invulnerability and temporary invisibility.
/// </summary>
public void TriggerStealthSkill2()
{
    StartCoroutine(StealthSkill2Routine());
}

/// <summary>
/// Coroutine for StealthSkill2: 
/// 1) Makes the player invulnerable for 2s.
/// 2) Then invisible ("HiddenPlayer" layer, 70% transparency) for 5s.
/// After 5s, restore to normal and start cooldown.
/// </summary>
private IEnumerator StealthSkill2Routine()
{
    stealthSkill2Active = true;
    Debug.Log("StealthSkill2 triggered! Player becomes invulnerable and invisible.");

    // Get Player reference
    Player player = FindObjectOfType<Player>();
    if (player == null)
    {
        Debug.LogError("No Player found. Cannot apply StealthSkill2 effects.");
        stealthSkill2Active = false;
        yield break;
    }

    // Get PlayerHealth reference
    PlayerHealth playerHealth = player.GetComponent<PlayerHealth>();
    if (playerHealth == null)
    {
        Debug.LogError("PlayerHealth component not found on Player.");
        stealthSkill2Active = false;
        yield break;
    }

    // Activate both Invulnerability and Invisibility
    player.SetTransparency(0.3f); // 70% transparent
    playerHealth.SetStealthSkill2Invulnerable(true);
    player.SetLayerOverride("HiddenPlayer");
    Debug.Log("Player is invulnerable and invisible for " + stealthSkill2Duration + " seconds due to StealthSkill2.");

    // Restore player's health to a minimal positive value to prevent death
    PlayerManager.Instance.SetHealth(1f);
    playerHealth.UpdateHealthFromManager(); // Update the health bar UI

    // Wait for the duration of StealthSkill2
    yield return new WaitForSeconds(stealthSkill2Duration); // e.g., 5 seconds

    // Deactivate both Invulnerability and Invisibility
    playerHealth.SetStealthSkill2Invulnerable(false);
    player.SetTransparency(1f); // Fully opaque
    player.SetLayerOverride("Player");
    Debug.Log("StealthSkill2 ended, player returned to normal state.");

    stealthSkill2Active = false;

    // Start cooldown
    stealthSkill2OnCooldown = true;
    stealthSkill2CooldownTimer = stealthSkill2Cooldown;

    Debug.Log("StealthSkill2 cooldown started for " + stealthSkill2Cooldown + " seconds.");

    // Count down cooldown
    while (stealthSkill2CooldownTimer > 0f)
    {
        stealthSkill2CooldownTimer -= Time.deltaTime;
        yield return null;
    }

    stealthSkill2OnCooldown = false;
    Debug.Log("StealthSkill2 is ready again.");
}
public List<SelectableSkill> GetUnlockedSelectableSkills()
{
    List<SelectableSkill> unlockedSkills = new List<SelectableSkill>();

    // Check if Fireball (Spell) is always available or if you consider it unlocked by default
    // Assuming Fireball (basic spell) is always available if player has mana:
    unlockedSkills.Add(SelectableSkill.Fireball);

    if (IsSkillUnlocked("AttackSkill2"))
        unlockedSkills.Add(SelectableSkill.AttackSkill2);
    if (IsSkillUnlocked("HealthSkill2")) // It's called HPSkill 2 in code
        unlockedSkills.Add(SelectableSkill.HPSkill2);
    if (IsSkillUnlocked("ManaSkill2"))   // MPSkill2
        unlockedSkills.Add(SelectableSkill.MPSkill2);
    if (IsSkillUnlocked("StealthSkill1"))
        unlockedSkills.Add(SelectableSkill.StealthSkill1);
    if (IsSkillUnlocked("FireballSkill2"))
        unlockedSkills.Add(SelectableSkill.FireballSkill2);

    return unlockedSkills;
}




}
