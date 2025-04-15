using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MerchantUIManager : MonoBehaviour
{
    // ===========================
    // UI Elements
    // ===========================
    public TextMeshProUGUI honorPointsText;

    public Button[] healthUpgradeButtons;
    public Button[] damageUpgradeButtons;
    public Button[] speedUpgradeButtons;
    public Button[] manaUpgradeButtons; // Array of buttons for mana upgrades
    public Button[] fireballUpgradeButtons; // Array of buttons for fireball damage upgrades
    public Button[] stealthUpgradeButtons; // Array of buttons for stealth upgrades

    public Button healthSkill1Button;
    public Button healthSkill2Button;
    public Button attackSkill1Button;
    public Button attackSkill2Button;
    public Button speedSkill1Button;
    public Button speedSkill2Button;
    public Button manaSkill1Button; // Skill button for mana
    public Button manaSkill2Button; // Skill button for mana
    public Button fireballSkill1Button; // Skill button for fireball
    public Button fireballSkill2Button; // Skill button for fireball
    public Button stealthSkill1Button; // Skill button for stealth
    public Button stealthSkill2Button; // Skill button for stealth

    public Button closeButton; // Close button to hide the merchant panel

    // ===========================
    // Initialization
    // ===========================
    private void Start()
    {
        UpdateHonorPointsDisplay();
        InitializeButtons();
        UpdateUpgradeButtons(); // Ensure buttons reflect current upgrade levels
        UpdateSkillButtons();
        closeButton.onClick.AddListener(CloseMerchantPanel);
    }

    // ===========================
    // UI Display Methods
    // ===========================
    /// <summary>
    /// Updates the Honor Points display in the UI.
    /// </summary>
    private void UpdateHonorPointsDisplay()
    {
        honorPointsText.text = "Honor Points: " + PlayerManager.Instance.GetHonorPoints().ToString();
    }

    // ===========================
    // Button Initialization
    // ===========================
    /// <summary>
    /// Initializes all upgrade and skill buttons with appropriate listeners and text.
    /// </summary>
    private void InitializeButtons()
    {
        // Initialize Health Upgrade Buttons
        for (int i = 0; i < healthUpgradeButtons.Length; i++)
        {
            int level = i + 1;
            int cost = GetCostForLevel(level);
            healthUpgradeButtons[i].GetComponentInChildren<TextMeshProUGUI>().text = $"Upgrade Health to Level {level} ({cost} Honor Points)";
            int index = i;
            healthUpgradeButtons[i].onClick.AddListener(() => UpgradeHealth(index, cost));
        }

        // Initialize Damage Upgrade Buttons
        for (int i = 0; i < damageUpgradeButtons.Length; i++)
        {
            int level = i + 1;
            int cost = GetCostForLevel(level);
            damageUpgradeButtons[i].GetComponentInChildren<TextMeshProUGUI>().text = $"Upgrade Damage to Level {level} ({cost} Honor Points)";
            int index = i;
            damageUpgradeButtons[i].onClick.AddListener(() => UpgradeDamage(index, cost));
        }

        // Initialize Speed Upgrade Buttons
        for (int i = 0; i < speedUpgradeButtons.Length; i++)
        {
            int level = i + 1;
            int cost = GetCostForLevel(level);
            speedUpgradeButtons[i].GetComponentInChildren<TextMeshProUGUI>().text = $"Upgrade Speed to Level {level} ({cost} Honor Points)";
            int index = i;
            speedUpgradeButtons[i].onClick.AddListener(() => UpgradeSpeed(index, cost));
        }

        // Initialize Mana Upgrade Buttons
        for (int i = 0; i < manaUpgradeButtons.Length; i++)
        {
            int level = i + 1;
            int cost = GetCostForLevel(level);
            manaUpgradeButtons[i].GetComponentInChildren<TextMeshProUGUI>().text = $"Upgrade Mana to Level {level} ({cost} Honor Points)";
            int index = i;
            manaUpgradeButtons[i].onClick.AddListener(() => UpgradeMana(index, cost));
        }

        // Initialize Fireball Damage Upgrade Buttons
        for (int i = 0; i < fireballUpgradeButtons.Length; i++)
        {
            int level = i + 1;
            int cost = GetCostForLevel(level);
            fireballUpgradeButtons[i].GetComponentInChildren<TextMeshProUGUI>().text = $"Upgrade Fireball Damage to Level {level} ({cost} Honor Points)";
            int index = i;
            fireballUpgradeButtons[i].onClick.AddListener(() => UpgradeFireballDamage(index, cost));
        }

        // Initialize Stealth Upgrade Buttons
        for (int i = 0; i < stealthUpgradeButtons.Length; i++)
        {
            int level = i + 1;
            int cost = GetCostForLevel(level);
            stealthUpgradeButtons[i].GetComponentInChildren<TextMeshProUGUI>().text = $"Upgrade Stealth to Level {level} ({cost} Honor Points)";
            int index = i;
            stealthUpgradeButtons[i].onClick.AddListener(() => UpgradeStealth(index, cost));
        }

        // Initialize Skill Buttons
        healthSkill1Button.onClick.AddListener(() => BuySkill(3000, "HealthSkill1"));
        healthSkill2Button.onClick.AddListener(() => BuySkill(5000, "HealthSkill2"));
        attackSkill1Button.onClick.AddListener(() => BuySkill(3000, "AttackSkill1"));
        attackSkill2Button.onClick.AddListener(() => BuySkill(5000, "AttackSkill2"));
        speedSkill1Button.onClick.AddListener(() => BuySkill(3000, "SpeedSkill1"));
        speedSkill2Button.onClick.AddListener(() => BuySkill(5000, "SpeedSkill2"));
        manaSkill1Button.onClick.AddListener(() => BuySkill(3000, "ManaSkill1"));
        manaSkill2Button.onClick.AddListener(() => BuySkill(5000, "ManaSkill2"));
        fireballSkill1Button.onClick.AddListener(() => BuySkill(3000, "FireballSkill1"));
        fireballSkill2Button.onClick.AddListener(() => BuySkill(5000, "FireballSkill2"));
        stealthSkill1Button.onClick.AddListener(() => BuySkill(3000, "StealthSkill1"));
        stealthSkill2Button.onClick.AddListener(() => BuySkill(5000, "StealthSkill2"));
    }

    // ===========================
    // Upgrade Methods
    // ===========================
    /// <summary>
    /// Upgrades the player's health level.
    /// </summary>
    /// <param name="index">Button index.</param>
    /// <param name="cost">Cost in honor points.</param>
    private void UpgradeHealth(int index, int cost)
    {
        int level = index + 1;
        if (PlayerManager.Instance.GetHonorPoints() >= cost)
        {
            PlayerManager.Instance.DeductHonorPoints(cost);
            PlayerManager.Instance.UpgradeHealth(level);
            UpdateHonorPointsDisplay();
            UpdateUpgradeButtons(); // Refresh button states
            UpdateSkillButtons(); // Refresh skill buttons if necessary
        }
        else
        {
            Debug.Log("Not enough honor points to upgrade health.");
        }
    }

    /// <summary>
    /// Upgrades the player's damage level.
    /// </summary>
    /// <param name="index">Button index.</param>
    /// <param name="cost">Cost in honor points.</param>
    private void UpgradeDamage(int index, int cost)
    {
        int level = index + 1;
        if (PlayerManager.Instance.GetHonorPoints() >= cost)
        {
            PlayerManager.Instance.DeductHonorPoints(cost);
            PlayerManager.Instance.UpgradeDamage(level);
            UpdateHonorPointsDisplay();
            UpdateUpgradeButtons(); // Refresh button states
            UpdateSkillButtons(); // Refresh skill buttons if necessary
        }
        else
        {
            Debug.Log("Not enough honor points to upgrade damage.");
        }
    }

    /// <summary>
    /// Upgrades the player's speed level.
    /// </summary>
    /// <param name="index">Button index.</param>
    /// <param name="cost">Cost in honor points.</param>
    private void UpgradeSpeed(int index, int cost)
    {
        int level = index + 1;
        if (PlayerManager.Instance.GetHonorPoints() >= cost)
        {
            PlayerManager.Instance.DeductHonorPoints(cost);
            PlayerManager.Instance.UpgradeSpeed(level);
            UpdateHonorPointsDisplay();
            UpdateUpgradeButtons(); // Refresh button states
            UpdateSkillButtons(); // Refresh skill buttons if necessary
        }
        else
        {
            Debug.Log("Not enough honor points to upgrade speed.");
        }
    }

    /// <summary>
    /// Upgrades the player's mana level.
    /// </summary>
    /// <param name="index">Button index.</param>
    /// <param name="cost">Cost in honor points.</param>
    private void UpgradeMana(int index, int cost)
    {
        int level = index + 1;
        if (PlayerManager.Instance.GetHonorPoints() >= cost)
        {
            PlayerManager.Instance.DeductHonorPoints(cost);
            PlayerManager.Instance.UpgradeMana(level);
            UpdateHonorPointsDisplay();
            UpdateUpgradeButtons(); // Refresh button states
            UpdateSkillButtons(); // Refresh skill buttons if necessary
        }
        else
        {
            Debug.Log("Not enough honor points to upgrade mana.");
        }
    }

    /// <summary>
    /// Upgrades the player's fireball damage level.
    /// </summary>
    /// <param name="index">Button index.</param>
    /// <param name="cost">Cost in honor points.</param>
    private void UpgradeFireballDamage(int index, int cost)
    {
        int level = index + 1;
        if (PlayerManager.Instance.GetHonorPoints() >= cost)
        {
            PlayerManager.Instance.DeductHonorPoints(cost);
            PlayerManager.Instance.UpgradeFireballDamage(level);
            UpdateHonorPointsDisplay();
            UpdateUpgradeButtons(); // Refresh button states
            UpdateSkillButtons(); // Refresh skill buttons if necessary
        }
        else
        {
            Debug.Log("Not enough honor points to upgrade fireball damage.");
        }
    }

    /// <summary>
    /// Upgrades the player's stealth level.
    /// </summary>
    /// <param name="index">Button index.</param>
    /// <param name="cost">Cost in honor points.</param>
    private void UpgradeStealth(int index, int cost)
    {
        int level = index + 1;
        if (PlayerManager.Instance.GetHonorPoints() >= cost)
        {
            PlayerManager.Instance.DeductHonorPoints(cost);
            PlayerManager.Instance.UpgradeStealth(level);
            UpdateHonorPointsDisplay();
            UpdateUpgradeButtons(); // Refresh button states
            UpdateSkillButtons(); // Refresh skill buttons if necessary
        }
        else
        {
            Debug.Log("Not enough honor points to upgrade stealth.");
        }
    }

    // ===========================
    // Skill Purchase Method
    // ===========================
    /// <summary>
    /// Buys a skill if the player has enough honor points and hasn't already unlocked it.
    /// </summary>
    /// <param name="cost">Cost in honor points.</param>
    /// <param name="skillName">Name of the skill to buy.</param>
    private void BuySkill(int cost, string skillName)
    {
        if (PlayerManager.Instance.IsSkillUnlocked(skillName))
        {
            Debug.Log("Skill already unlocked.");
            return;
        }

        if (PlayerManager.Instance.GetHonorPoints() >= cost)
        {
            PlayerManager.Instance.DeductHonorPoints(cost);
            PlayerManager.Instance.UnlockSkill(skillName, 1);
            UpdateHonorPointsDisplay();
            UpdateSkillButtons(); // Refresh skill buttons to reflect the purchase
        }
        else
        {
            Debug.Log("Not enough honor points to buy this skill.");
        }
    }

    // ===========================
    // Upgrade Buttons State Update
    // ===========================
    /// <summary>
    /// Updates the interactable states of all upgrade buttons based on the player's current upgrade levels.
    /// </summary>
    private void UpdateUpgradeButtons()
    {
        // Update Health Upgrade Buttons
        for (int i = 0; i < healthUpgradeButtons.Length; i++)
        {
            // Button should be interactable if player's health level matches the button's level
            // and the player hasn't reached the maximum upgrade level
            if (PlayerManager.Instance.healthLevel == i && i < healthUpgradeButtons.Length)
            {
                healthUpgradeButtons[i].interactable = true;
            }
            else
            {
                healthUpgradeButtons[i].interactable = false;
            }
        }

        // Update Damage Upgrade Buttons
        for (int i = 0; i < damageUpgradeButtons.Length; i++)
        {
            if (PlayerManager.Instance.damageLevel == i && i < damageUpgradeButtons.Length)
            {
                damageUpgradeButtons[i].interactable = true;
            }
            else
            {
                damageUpgradeButtons[i].interactable = false;
            }
        }

        // Update Speed Upgrade Buttons
        for (int i = 0; i < speedUpgradeButtons.Length; i++)
        {
            if (PlayerManager.Instance.speedLevel == i && i < speedUpgradeButtons.Length)
            {
                speedUpgradeButtons[i].interactable = true;
            }
            else
            {
                speedUpgradeButtons[i].interactable = false;
            }
        }

        // Update Mana Upgrade Buttons
        for (int i = 0; i < manaUpgradeButtons.Length; i++)
        {
            if (PlayerManager.Instance.manaLevel == i && i < manaUpgradeButtons.Length)
            {
                manaUpgradeButtons[i].interactable = true;
            }
            else
            {
                manaUpgradeButtons[i].interactable = false;
            }
        }

        // Update Fireball Damage Upgrade Buttons
        for (int i = 0; i < fireballUpgradeButtons.Length; i++)
        {
            if (PlayerManager.Instance.fireballDamageLevel == i && i < fireballUpgradeButtons.Length)
            {
                fireballUpgradeButtons[i].interactable = true;
            }
            else
            {
                fireballUpgradeButtons[i].interactable = false;
            }
        }

        // Update Stealth Upgrade Buttons
        for (int i = 0; i < stealthUpgradeButtons.Length; i++)
        {
            if (PlayerManager.Instance.stealthLevel == i && i < stealthUpgradeButtons.Length)
            {
                stealthUpgradeButtons[i].interactable = true;
            }
            else
            {
                stealthUpgradeButtons[i].interactable = false;
            }
        }
    }

    // ===========================
    // Skill Buttons State Update
    // ===========================
    /// <summary>
    /// Updates the interactable states of all skill buttons based on the player's current upgrade levels and skill unlocks.
    /// </summary>
    private void UpdateSkillButtons()
    {
        healthSkill1Button.interactable = (PlayerManager.Instance.healthLevel >= 5) && !PlayerManager.Instance.IsSkillUnlocked("HealthSkill1");
        healthSkill2Button.interactable = (PlayerManager.Instance.healthLevel >= 10) && !PlayerManager.Instance.IsSkillUnlocked("HealthSkill2");
        attackSkill1Button.interactable = (PlayerManager.Instance.damageLevel >= 5) && !PlayerManager.Instance.IsSkillUnlocked("AttackSkill1");
        attackSkill2Button.interactable = (PlayerManager.Instance.damageLevel >= 10) && !PlayerManager.Instance.IsSkillUnlocked("AttackSkill2");
        speedSkill1Button.interactable = (PlayerManager.Instance.speedLevel >= 5) && !PlayerManager.Instance.IsSkillUnlocked("SpeedSkill1");
        speedSkill2Button.interactable = (PlayerManager.Instance.speedLevel >= 10) && !PlayerManager.Instance.IsSkillUnlocked("SpeedSkill2");
        manaSkill1Button.interactable = (PlayerManager.Instance.manaLevel >= 5) && !PlayerManager.Instance.IsSkillUnlocked("ManaSkill1");
        manaSkill2Button.interactable = (PlayerManager.Instance.manaLevel >= 10) && !PlayerManager.Instance.IsSkillUnlocked("ManaSkill2");
        fireballSkill1Button.interactable = (PlayerManager.Instance.fireballDamageLevel >= 5) && !PlayerManager.Instance.IsSkillUnlocked("FireballSkill1");
        fireballSkill2Button.interactable = (PlayerManager.Instance.fireballDamageLevel >= 10) && !PlayerManager.Instance.IsSkillUnlocked("FireballSkill2");
        stealthSkill1Button.interactable = (PlayerManager.Instance.stealthLevel >= 5) && !PlayerManager.Instance.IsSkillUnlocked("StealthSkill1");
        stealthSkill2Button.interactable = (PlayerManager.Instance.stealthLevel >= 10) && !PlayerManager.Instance.IsSkillUnlocked("StealthSkill2");
    }

    // ===========================
    // Utility Methods
    // ===========================
    /// <summary>
    /// Retrieves the cost for a given upgrade level.
    /// </summary>
    /// <param name="level">Upgrade level (1-based).</param>
    /// <returns>Cost in honor points.</returns>
    private int GetCostForLevel(int level)
    {
        int[] costs = { 300, 400, 550, 750, 1000, 1300, 1650, 2050, 2500, 3000 };
        if (level <= costs.Length && level > 0)
        {
            return costs[level - 1];
        }
        else
        {
            Debug.LogWarning($"Invalid level {level} for cost calculation. Returning max cost.");
            return costs[costs.Length - 1];
        }
    }

    // ===========================
    // Close Panel Method
    // ===========================
    /// <summary>
    /// Closes the merchant panel UI.
    /// </summary>
    public void CloseMerchantPanel()
    {
        Debug.Log("Close button pressed");
        gameObject.SetActive(false);
    }
}
