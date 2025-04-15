using System;
using System.IO;
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Static class responsible for handling game saving and loading operations using JSON serialization.
/// </summary>
public static class SaveSystem
{
    /// <summary>
    /// Retrieves the file path for a given save slot number.
    /// </summary>
    /// <param name="slotNumber">1-based slot number.</param>
    /// <returns>File path as a string.</returns>
    private static string GetSavePath(int slotNumber)
    {
        return Path.Combine(Application.persistentDataPath, $"save_slot_{slotNumber}.json");
    }

    /// <summary>
    /// Checks if a save file exists for the specified slot.
    /// </summary>
    /// <param name="slotNumber">1-based slot number.</param>
    /// <returns>True if the save file exists; otherwise, false.</returns>
    public static bool SaveFileExists(int slotNumber)
    {
        string path = GetSavePath(slotNumber);
        bool exists = File.Exists(path);
        Debug.Log($"[SaveSystem] SaveFileExists - Slot {slotNumber}: {exists}");
        return exists;
    }

    /// <summary>
    /// Saves the game data to the specified slot using JSON serialization.
    /// </summary>
    /// <param name="data">SaveData object containing all game state.</param>
    /// <param name="slotNumber">1-based slot number.</param>
    public static void SaveGame(SaveData data, int slotNumber)
    {
        string path = GetSavePath(slotNumber);

        try
        {
            // **Collect data from GameManager before serialization**
            CollectGameData(data);

            string json = JsonUtility.ToJson(data, true); // Pretty print for readability
            File.WriteAllText(path, json);
            Debug.Log($"[SaveSystem] Game successfully saved to Slot {slotNumber} at path: {path}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SaveSystem] Failed to save game to Slot {slotNumber}: {ex.Message}");
        }
    }

    /// <summary>
    /// Loads the game data from the specified slot using JSON deserialization.
    /// </summary>
    /// <param name="slotNumber">1-based slot number.</param>
    /// <returns>Loaded SaveData object if successful; otherwise, null.</returns>
    public static SaveData LoadGame(int slotNumber)
    {
        string path = GetSavePath(slotNumber);
        if (File.Exists(path))
        {
            try
            {
                string json = File.ReadAllText(path);
                SaveData data = JsonUtility.FromJson<SaveData>(json);

                // **Apply loaded data to GameManager and PlayerManager**
                ApplyLoadedData(data);

                Debug.Log($"[SaveSystem] Game successfully loaded from Slot {slotNumber} at path: {path}");
                return data;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveSystem] Failed to load game from Slot {slotNumber}: {ex.Message}");
                return null;
            }
        }
        else
        {
            Debug.LogWarning($"[SaveSystem] No save file found in Slot {slotNumber} at path: {path}");
            return null;
        }
    }

    /// <summary>
    /// Deletes the save file from the specified slot.
    /// </summary>
    /// <param name="slotNumber">1-based slot number.</param>
    public static void DeleteSaveFile(int slotNumber)
    {
        string path = GetSavePath(slotNumber);

        if (File.Exists(path))
        {
            try
            {
                File.Delete(path);
                Debug.Log($"[SaveSystem] Save file successfully deleted from Slot {slotNumber} at path: {path}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveSystem] Failed to delete save file from Slot {slotNumber}: {ex.Message}");
            }
        }
        else
        {
            Debug.LogWarning($"[SaveSystem] No save file to delete in Slot {slotNumber} at path: {path}");
        }
    }

    /// <summary>
    /// Collects data from the game to populate the SaveData object.
    /// </summary>
    /// <param name="data">The SaveData object to populate.</param>
    private static void CollectGameData(SaveData data)
    {
        // ===========================
        // Collect Enemy Data
        // ===========================
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

        // **Collect permaDeadEnemies**
        data.permaDeadEnemies = new List<string>(GameManager.Instance.permaDeadEnemies);

        // ===========================
        // Collect Safe Zone States
        // ===========================
        data.safeZoneStates = new List<SafeZoneState>();
        foreach (var kvp in GameManager.Instance.safeZoneStates)
        {
            SafeZoneState safeZoneState = new SafeZoneState
            {
                safeZoneID = kvp.Key,
                isActive = kvp.Value
            };
            data.safeZoneStates.Add(safeZoneState);
        }

        // ===========================
        // Collect Portcullis States
        // ===========================
        data.portcullisStates = new List<PortcullisState>();
        foreach (var kvp in GameManager.Instance.portcullisStates)
        {
            PortcullisState portcullisState = new PortcullisState
            {
                portcullisID = kvp.Key,
                isOpen = kvp.Value
            };
            data.portcullisStates.Add(portcullisState);
        }

        // ===========================
        // Collect Collected Pickups
        // ===========================
        data.collectedPickups = new List<string>(GameManager.Instance.collectedPickups);

        // ===========================
        // Collect Other Game Data
        // ===========================
        // Collect player stats, upgrades, flask data, skills, cooldowns, etc., from PlayerManager
        PlayerManager.Instance.CollectPlayerData(data);

        // If you have other game data to collect, add it here
    }

    /// <summary>
    /// Applies loaded data to the game.
    /// </summary>
    /// <param name="data">The SaveData object containing loaded data.</param>
    private static void ApplyLoadedData(SaveData data)
    {
        // ===========================
        // Apply Enemy Data
        // ===========================
        GameManager.Instance.enemyHealth.Clear();
        GameManager.Instance.deadEnemies.Clear();
        GameManager.Instance.permaDeadEnemies.Clear();

        if (data.enemies != null)
        {
            foreach (EnemyData enemyData in data.enemies)
            {
                GameManager.Instance.enemyHealth[enemyData.enemyID] = enemyData.currentHealth;
                if (enemyData.isDead)
                {
                    GameManager.Instance.MarkEnemyAsDead(enemyData.enemyID, enemyData.respawnable);
                }
            }
        }

        // **Apply permaDeadEnemies**
        if (data.permaDeadEnemies != null)
        {
            GameManager.Instance.permaDeadEnemies = new HashSet<string>(data.permaDeadEnemies);
        }

        // ===========================
        // Apply Safe Zone States
        // ===========================
        GameManager.Instance.safeZoneStates.Clear();
        if (data.safeZoneStates != null)
        {
            foreach (SafeZoneState safeZoneState in data.safeZoneStates)
            {
                GameManager.Instance.safeZoneStates[safeZoneState.safeZoneID] = safeZoneState.isActive;
            }
        }

        // ===========================
        // Apply Portcullis States
        // ===========================
        GameManager.Instance.portcullisStates.Clear();
        if (data.portcullisStates != null)
        {
            foreach (PortcullisState portcullisState in data.portcullisStates)
            {
                GameManager.Instance.portcullisStates[portcullisState.portcullisID] = portcullisState.isOpen;
            }
        }

        // ===========================
        // Apply Collected Pickups
        // ===========================
        GameManager.Instance.collectedPickups.Clear();
        if (data.collectedPickups != null)
        {
            GameManager.Instance.collectedPickups = new HashSet<string>(data.collectedPickups);
        }

        // ===========================
        // Apply Other Game Data
        // ===========================
        // Apply player stats, upgrades, flask data, skills, cooldowns, etc., to PlayerManager
        PlayerManager.Instance.ApplyPlayerData(data);

        // If you have other game data to apply, add it here
    }
}
