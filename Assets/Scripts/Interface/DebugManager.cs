using UnityEngine;

public class DebugManager : MonoBehaviour
{
    /// <summary>
    /// Adds 1000 honor points to the player.
    /// </summary>
    public void Add10000Honor()
    {
        PlayerManager.Instance.AddHonorPoints(10000);
        Debug.Log("Added 10000 Honor Points");
    }

    /// <summary>
    /// Resets the player's honor points to 0.
    /// </summary>
    public void ResetHonor()
    {
        PlayerManager.Instance.SetHonorPoints(0);
        Debug.Log("Honor Points reset to 0");
    }

    /// <summary>
    /// Triggers a full reset of all player and game data.
    /// Calls the ResetAllData method in the PlayerManager class.
    /// </summary>
    public void ResetAll()
    {
        if (PlayerManager.Instance != null)
        {
            PlayerManager.Instance.ResetAllData(); // Trigger the reset method in PlayerManager
            Debug.Log("All game data has been reset: Player stats, upgrades, honor points, flasks, and skills.");
        }
        else
        {
            Debug.LogError("PlayerManager instance is null. Cannot reset data.");
        }
    }
}
