public class FlaskData
{
    // Total Flask Capacity Upgrade Level (0 to 5)
    public int capacityLevel = 0;
    public int maxFlasks = 5; // Starts at 5, increases to 10

    // HP Flask Healing Amount Upgrade Level (0 to 5)
    public int hpHealingLevel = 0;
    public int hpHealingAmount = 5; // Starts at 5, increases to 30

    // Mana Flask Refill Amount Upgrade Level (0 to 5)
    public int manaRefillLevel = 0;
    public int manaRefillAmount = 3; // Starts at 3, increases to 8

    // Current Flask Counts
    public int totalFlasks = 5;
    public int hpFlasks = 3;
    public int manaFlasks = 2;

    // Methods to update values based on levels
    public void UpdateMaxFlasks()
    {
        maxFlasks = 5 + capacityLevel; // Increases by 1 per level
    }

    public void UpdateHpHealingAmount()
    {
        hpHealingAmount = 5 + (hpHealingLevel * 5); // Increases by 5 per level
    }

    public void UpdateManaRefillAmount()
    {
        manaRefillAmount = 3 + manaRefillLevel; // Increases by 1 per level
    }

    // Save and Load Methods can be added here if needed
}
