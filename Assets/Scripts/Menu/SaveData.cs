using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Serializable class to store all game save data.
/// </summary>
[Serializable]
public class SaveData
{
    // ===========================
    // Scene and Position
    // ===========================
    public string sceneName;
    public Vector3 playerPosition;

    // ===========================
    // Health and Mana
    // ===========================
    public float currentHealth;
    public float maxHealth;
    public float currentMana;
    public float maxMana;

    // ===========================
    // Honor Points and Recovery
    // ===========================
    public int honorPoints;
    public int lostHonorPoints;
    public bool hasHonorRecoveryObject;
    public Vector2 playerDeathLocation;
    public string deathSceneName;

    // ===========================
    // Upgrades
    // ===========================
    public int healthLevel;
    public int damageLevel;
    public int speedLevel;
    public int manaLevel;
    public int fireballDamageLevel;
    public int stealthLevel;
    public bool hasGrapplingHook;

    // ===========================
    // Flask Data
    // ===========================
    public PlayerManager.FlaskData flaskData;

    // ===========================
    // Skill Unlocks
    // ===========================
    public bool healthSkill1Unlocked;
    public bool healthSkill2Unlocked;
    public bool attackSkill1Unlocked;
    public bool attackSkill2Unlocked;
    public bool speedSkill1Unlocked;
    public bool speedSkill2Unlocked;
    public bool manaSkill1Unlocked;
    public bool manaSkill2Unlocked;
    public bool fireballSkill1Unlocked; // If applicable
    public bool fireballSkill2Unlocked; // If applicable
    public bool stealthSkill1Unlocked;
    public bool stealthSkill2Unlocked;

    // ===========================
    // Skill Cooldowns and Active States
    // ===========================

    // **Attack Skill 2**
    public bool attackSkill2OnCooldown;
    public float attackSkill2CooldownTimer;

    // **Health Skill 2**
    public bool healthSkill2Active;
    public bool healthSkill2OnCooldown;
    public float healthSkill2CooldownTimer;
    public float healthSkill2Timer;

    // **Speed Skill 2**
    public bool speedSkill2OnCooldown;
    public float speedSkill2CooldownTimer;
    public bool canDodgeProjectiles;
    public float speedSkill2Timer;

    // **Mana Skill 2**
    public bool manaSkill2Active;
    public bool manaSkill2OnCooldown;
    public float manaSkill2CooldownTimer;
    public float manaSkill2Timer;

    // **Stealth Skill 1**
    public bool stealthSkill1Active;
    public bool stealthSkill1OnCooldown;
    public float stealthSkill1CooldownTimer;
    public float stealthSkill1Timer;

    // **Stealth Skill 2**
    public bool stealthSkill2Active;
    public bool stealthSkill2OnCooldown;
    public float stealthSkill2CooldownTimer;
    public float stealthSkill2Timer;

    // **Power Skill (FireballSkill2)**
    public bool powerSkillActive;
    public bool powerSkillOnCooldown;
    public float powerSkillCooldownTimer;
    public float powerSkillTimer;

    // ===========================
    // Enemy Data
    // ===========================
    public List<EnemyData> enemies;

    // **Perma Dead Enemies**
    public List<string> permaDeadEnemies;

    // ===========================
    // Safe Zone States
    // ===========================
    public List<SafeZoneState> safeZoneStates;

    // ===========================
    // Portcullis States
    // ===========================
    public List<PortcullisState> portcullisStates;

    // ===========================
    // Collected Pickups
    // ===========================
    public List<string> collectedPickups;
}

/// <summary>
/// Serializable class to store individual enemy data.
/// </summary>
[Serializable]
public class EnemyData
{
    public string enemyID;
    public float currentHealth;
    public bool isDead;
    public bool respawnable;
}

/// <summary>
/// Serializable class to store individual safe zone states.
/// </summary>
[Serializable]
public class SafeZoneState
{
    public string safeZoneID;
    public bool isActive;
}

/// <summary>
/// Serializable class to store individual portcullis states.
/// </summary>
[Serializable]
public class PortcullisState
{
    public string portcullisID;
    public bool isOpen;
}
