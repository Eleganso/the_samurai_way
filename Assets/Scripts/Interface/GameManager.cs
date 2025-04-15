using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Manages game-wide states, including enemy health, safe zones, portcullis states, collected pickups, and player respawning.
/// Implements singleton pattern to ensure only one instance exists.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    // ===========================
    // Door Transition Variables
    // ===========================
    private string doorPointID;
    private bool useDoorPoint = false;

    // ===========================
    // Player Health
    // ===========================
    public int playerHealth;

    // ===========================
    // Enemy Health and Dead Enemies
    // ===========================
    public Dictionary<string, float> enemyHealth;
    public HashSet<string> deadEnemies;

    // Permanent Dead Enemies (non-respawnable)
    public HashSet<string> permaDeadEnemies;

    // ===========================
    // Safe Zone Variables
    // ===========================
    private string currentSafeZoneID; // Store the current SafeZone ID
    public Dictionary<string, bool> safeZoneStates; // SafeZone states

    // ===========================
    // Portcullis States
    // ===========================
    public Dictionary<string, bool> portcullisStates = new Dictionary<string, bool>();

    // ===========================
    // Collected Pickups
    // ===========================
    public HashSet<string> collectedPickups;

    private void Awake()
    {
        // Implement Singleton Pattern
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            enemyHealth = new Dictionary<string, float>();
            deadEnemies = new HashSet<string>();
            permaDeadEnemies = new HashSet<string>();
            safeZoneStates = new Dictionary<string, bool>();
            portcullisStates = new Dictionary<string, bool>();
            collectedPickups = new HashSet<string>();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        InitializeEnemies();
        InitializeSafeZones();
        InitializePortcullises();
        PlayerManager.Instance.LoadHonorPoints(); // Load honor points at the start of the game

        // Load collected pickups
        LoadCollectedPickups();
    }

    /// <summary>
    /// Initializes enemy data dictionaries.
    /// </summary>
    public void InitializeEnemyData()
    {
        if (enemyHealth == null)
        {
            enemyHealth = new Dictionary<string, float>();
        }
        if (deadEnemies == null)
        {
            deadEnemies = new HashSet<string>();
        }
        if (permaDeadEnemies == null)
        {
            permaDeadEnemies = new HashSet<string>();
        }
    }

    // ===========================
    // Door Transition Methods
    // ===========================
    public void SetDoorPoint(string doorPointID)
    {
        this.doorPointID = doorPointID;
        useDoorPoint = true;
    }

    public string GetDoorPointID()
    {
        return doorPointID;
    }

    public bool ShouldUseDoorPoint()
    {
        return useDoorPoint;
    }

    public void ResetDoorPoint()
    {
        useDoorPoint = false;
    }

    // ===========================
    // Player Health Methods
    // ===========================
    public void UpdatePlayerHealth(int health)
    {
        playerHealth = health;
    }

    public int GetPlayerHealth()
    {
        return playerHealth;
    }

    // ===========================
    // Enemy Management Methods
    // ===========================
    public void UpdateEnemyHealth(string enemyID, float health)
    {
        if (enemyHealth.ContainsKey(enemyID))
        {
            enemyHealth[enemyID] = health;
        }
        else
        {
            enemyHealth.Add(enemyID, health);
        }
    }

    public float GetEnemyHealth(string enemyID)
    {
        if (enemyHealth.ContainsKey(enemyID))
        {
            return enemyHealth[enemyID];
        }
        return -1f; // Indicating the enemy is not found
    }

    // Checks if an enemy is dead, considering both deadEnemies and permaDeadEnemies
    public bool IsEnemyDead(string enemyID)
    {
        return deadEnemies.Contains(enemyID) || permaDeadEnemies.Contains(enemyID);
    }

    // Resets enemies, excluding permanent dead enemies
    public void ResetEnemies()
    {
        deadEnemies.Clear(); // Clear the dead enemies list
        // permaDeadEnemies remain intact

        enemyHealth.Clear();  // Clear the enemy health dictionary

        EnemyHealth[] enemies = FindObjectsOfType<EnemyHealth>();
        foreach (var enemy in enemies)
        {
            if (!permaDeadEnemies.Contains(enemy.enemyID))
            {
                enemy.EnableAndRefillHealth(); // Refill health and enable enemy
                UpdateEnemyHealth(enemy.enemyID, enemy.maxHealth); // Reset health in the dictionary
            }
            else
            {
                enemy.Disable(); // Keep the perma-dead enemy disabled
            }
        }
    }

    // Marks an enemy as dead, considering if it is respawnable
    public void MarkEnemyAsDead(string enemyID, bool respawnable)
    {
        if (respawnable)
        {
            if (!deadEnemies.Contains(enemyID))
            {
                deadEnemies.Add(enemyID);
            }
        }
        else
        {
            if (!permaDeadEnemies.Contains(enemyID))
            {
                permaDeadEnemies.Add(enemyID);
            }
        }
    }

    // Checks if an enemy is respawnable
    public bool IsEnemyRespawnable(string enemyID)
    {
        EnemyHealth enemy = FindEnemyByID(enemyID);
        if (enemy != null)
        {
            return enemy.Respawnable;
        }
        return true; // Default to true if enemy not found
    }

    // Helper method to find enemy by ID
    private EnemyHealth FindEnemyByID(string enemyID)
    {
        EnemyHealth[] enemies = FindObjectsOfType<EnemyHealth>();
        foreach (var enemy in enemies)
        {
            if (enemy.enemyID == enemyID)
            {
                return enemy;
            }
        }
        return null;
    }

    // ===========================
    // Safe Zone Methods
    // ===========================
    public void SetSafeZone(string safeZoneID, bool isActive)
    {
        if (safeZoneStates.ContainsKey(safeZoneID))
        {
            safeZoneStates[safeZoneID] = isActive;
        }
        else
        {
            safeZoneStates.Add(safeZoneID, isActive);
        }
    }

    public bool IsSafeZoneActive(string safeZoneID)
    {
        return safeZoneStates.ContainsKey(safeZoneID) && safeZoneStates[safeZoneID];
    }

    public void SetCurrentSafeZoneID(string safeZoneID)
    {
        currentSafeZoneID = safeZoneID;
        Debug.Log("Current SafeZone ID set to: " + safeZoneID);
    }

    public string GetCurrentSafeZoneID()
    {
        return currentSafeZoneID;
    }

    // ===========================
    // Safe Zone Interaction Methods
    // ===========================
    public void InteractWithActiveSafeZone()
    {
        SaveEnemyStates();
        PlayerManager.Instance.DeductHonorPointsForSafeZone(); // Deduct 10% of honor points when using the safe zone
        PlayerManager.Instance.SaveHonorRecoveryData(); // Save honor recovery data
        PlayerManager.Instance.SaveHonorPoints(); // Save honor points after deduction
        ResetSceneAndEnemies();
        StartCoroutine(MovePlayerToSafeZoneWithDelay(0.01f)); // Add a 0.01-second delay before moving the player
    }

    // ===========================
    // Player Respawn Methods
    // ===========================
    public void RespawnPlayer()
    {
        SaveEnemyStates();
        PlayerManager.Instance.SaveHonorRecoveryData(); // Save honor recovery data
        ResetSceneAndEnemies();
        StartCoroutine(MovePlayerToClosestSafeZoneFromDeath(0.01f)); // Add a 0.01-second delay before moving the player
    }

    // ===========================
    // Scene Reload Methods
    // ===========================
    public void ReloadScene()
    {
        PlayerManager.Instance.SaveHonorRecoveryData(); // Save honor recovery data before reloading the scene
        PlayerManager.Instance.SaveHonorPoints(); // Save honor points before reloading the scene
        Scene currentScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(currentScene.name);
    }

    private IEnumerator ResetEnemiesAfterSceneLoad()
    {
        yield return new WaitForEndOfFrame();
        ResetEnemies();
        yield return new WaitForSeconds(0.001f); // Wait briefly before moving the player
        MovePlayerToSafeZone();
    }

    // ===========================
    // Enemy Initialization and Reset Methods
    // ===========================
    /// <summary>
    /// Initializes enemy data dictionaries and sets up enemies in the scene.
    /// </summary>
    public void InitializeEnemies()
    {
        if (enemyHealth == null)
        {
            enemyHealth = new Dictionary<string, float>();
        }
        if (deadEnemies == null)
        {
            deadEnemies = new HashSet<string>();
        }
        if (permaDeadEnemies == null)
        {
            permaDeadEnemies = new HashSet<string>();
        }

        // Find all enemies in the scene
        EnemyHealth[] enemies = FindObjectsOfType<EnemyHealth>();

        foreach (var enemy in enemies)
        {
            if (IsEnemyDead(enemy.enemyID))
            {
                enemy.Disable();
            }
            else
            {
                // Check if enemy data exists in saved data
                if (enemyHealth.ContainsKey(enemy.enemyID))
                {
                    // Set enemy's current health from saved data
                    enemy.CurrentHealth = enemyHealth[enemy.enemyID];
                    enemy.InitializeHealth(false); // Pass false to avoid resetting health
                }
                else
                {
                    // First-time initialization or no saved data; set enemy to full health
                    enemy.InitializeHealth(true); // Pass true for first-time initialization
                    // Save the initial health
                    enemyHealth[enemy.enemyID] = enemy.maxHealth;
                }
            }
        }
    }

    // Ensures that permaDeadEnemies remain disabled
    public void LoadEnemyStates()
    {
        EnemyHealth[] enemies = FindObjectsOfType<EnemyHealth>();
        foreach (var enemy in enemies)
        {
            if (IsEnemyDead(enemy.enemyID))
            {
                enemy.Disable();
            }
            else
            {
                float savedHealth = GetEnemyHealth(enemy.enemyID);
                if (savedHealth > 0)
                {
                    enemy.CurrentHealth = savedHealth;
                }
                else
                {
                    enemy.CurrentHealth = enemy.maxHealth;
                }
                enemy.InitializeHealth(false); // Pass false to avoid resetting health
            }
        }
    }

    // ===========================
    // Safe Zone Initialization
    // ===========================
    public void InitializeSafeZones()
    {
        SafeZone[] safeZones = FindObjectsOfType<SafeZone>();
        foreach (var safeZone in safeZones)
        {
            if (safeZone != null && IsSafeZoneActive(safeZone.safeZoneID))
            {
                safeZone.ActivateSafeZone();
            }
        }
    }

    // ===========================
    // Portcullis State Methods
    // ===========================
    /// <summary>
    /// Sets the state of a Portcullis.
    /// </summary>
    /// <param name="portcullisID">Unique identifier for the Portcullis.</param>
    /// <param name="isOpen">True if the Portcullis is open.</param>
    public void SetPortcullisState(string portcullisID, bool isOpen)
    {
        if (portcullisStates.ContainsKey(portcullisID))
        {
            portcullisStates[portcullisID] = isOpen;
        }
        else
        {
            portcullisStates.Add(portcullisID, isOpen);
        }
    }

    /// <summary>
    /// Gets the state of a Portcullis.
    /// </summary>
    /// <param name="portcullisID">Unique identifier for the Portcullis.</param>
    /// <returns>True if the Portcullis is open; otherwise, false.</returns>
    public bool IsPortcullisOpen(string portcullisID)
    {
        if (portcullisStates.ContainsKey(portcullisID))
        {
            return portcullisStates[portcullisID];
        }
        else
        {
            // Default to closed if not found
            return false;
        }
    }

    // ===========================
    // Portcullis Initialization
    // ===========================
    public void InitializePortcullises()
    {
        Portcullis[] portcullises = FindObjectsOfType<Portcullis>();
        foreach (var portcullis in portcullises)
        {
            portcullis.InitializePortcullis();
        }
    }

    // ===========================
    // Saving and Loading Enemy States
    // ===========================
    public void SaveEnemyStates()
    {
        // This method can be implemented as needed for your save system
        // It might involve collecting enemy data and saving it using your SaveSystem
    }

    // ===========================
    // Scene and Enemy Reset Methods
    // ===========================
    public void ResetSceneAndEnemies()
    {
        ReloadScene();
        StartCoroutine(ResetEnemiesAfterSceneLoad());
    }

    // ===========================
    // Player Movement Methods
    // ===========================
    private IEnumerator MovePlayerToSafeZoneWithDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        MovePlayerToSafeZone();
    }

    private void MovePlayerToSafeZone()
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null && !string.IsNullOrEmpty(currentSafeZoneID))
        {
            SafeZone[] safeZones = FindObjectsOfType<SafeZone>();
            foreach (var safeZone in safeZones)
            {
                if (safeZone.safeZoneID == currentSafeZoneID)
                {
                    Vector2 safeZonePosition = safeZone.transform.position;
                    Debug.Log("Moving player to SafeZone ID: " + currentSafeZoneID + " at position: " + safeZonePosition);
                    player.transform.position = safeZonePosition;
                    break;
                }
            }
        }
    }

    private IEnumerator MovePlayerToClosestSafeZoneFromDeath(float delay)
    {
        yield return new WaitForSeconds(delay);
        Vector2 deathLocation = PlayerManager.Instance.GetPlayerDeathLocation();
        Vector2 closestSafeZonePosition = GetClosestActiveSafeZonePosition(deathLocation);
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            Debug.Log("Respawning player at closest SafeZone from death location: " + closestSafeZonePosition);
            player.transform.position = closestSafeZonePosition;
        }
    }

    public Vector2 GetClosestActiveSafeZonePosition(Vector2 position)
    {
        SafeZone[] safeZones = FindObjectsOfType<SafeZone>();
        float minDistance = float.MaxValue;
        Vector2 closestPosition = Vector2.zero;

        foreach (var safeZone in safeZones)
        {
            if (IsSafeZoneActive(safeZone.safeZoneID))
            {
                float distance = Vector2.Distance(position, safeZone.transform.position);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    closestPosition = safeZone.transform.position;
                }
            }
        }
        return closestPosition;
    }

    // ===========================
    // Scene Load Completion Handling
    // ===========================
    public void OnSceneLoadComplete()
    {
        Debug.Log("Scene load complete, checking if HonorRecovery object should spawn...");
        if (PlayerManager.Instance.hasHonorRecoveryObject && SceneManager.GetActiveScene().name == PlayerManager.Instance.deathSceneName)
        {
            StartCoroutine(PlayerManager.Instance.DelayedSpawnHonorRecoveryObject());
        }

        // Load enemy states after loading the scene
        LoadEnemyStates();

        // Initialize Portcullises after scene load
        InitializePortcullises();

        // Load collected pickups
        LoadCollectedPickups();
    }

    // ===========================
    // Safe Zone Reset Method
    // ===========================
    /// <summary>
    /// Resets all safe zones to inactive.
    /// </summary>
    public void ResetSafeZones()
    {
        safeZoneStates.Clear();
        SafeZone[] safeZones = FindObjectsOfType<SafeZone>();
        foreach (var safeZone in safeZones)
        {
            safeZone.isActive = false;
            if (safeZone.appearObject != null)
            {
                safeZone.appearObject.SetActive(false);
            }
        }
        Debug.Log("All safe zones have been reset to inactive.");
    }

    // ===========================
    // Collected Pickups Management
    // ===========================
    /// <summary>
    /// Checks if a pickup has already been collected.
    /// </summary>
    /// <param name="pickupID">Unique identifier for the pickup.</param>
    /// <returns>True if collected, false otherwise.</returns>
    public bool IsPickupCollected(string pickupID)
    {
        return collectedPickups.Contains(pickupID);
    }

    /// <summary>
    /// Marks a pickup as collected.
    /// </summary>
    /// <param name="pickupID">Unique identifier for the pickup.</param>
    public void MarkPickupAsCollected(string pickupID)
    {
        if (!collectedPickups.Contains(pickupID))
        {
            collectedPickups.Add(pickupID);
        }
    }

    // Collected pickups are now saved and loaded via the SaveSystem

    // ===========================
    // Save and Load Collected Pickups
    // ===========================
    /// <summary>
    /// Loads collected pickups from the saved data.
    /// This method is called during the game start and after loading a game.
    /// </summary>
    public void LoadCollectedPickups()
    {
        // Collected pickups are loaded via the SaveSystem and applied in ApplyLoadedData()
        // No need to implement anything here unless you have additional logic
    }

    /// <summary>
    /// Saves collected pickups to the saved data.
    /// This method can be called after a pickup is collected.
    /// </summary>
    public void SaveCollectedPickups()
    {
        // Collected pickups are saved via the SaveSystem
        // No need to implement anything here unless you have additional logic
    }
}
