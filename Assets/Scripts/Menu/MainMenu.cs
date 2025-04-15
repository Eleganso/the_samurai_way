using System; // Added for StringComparison
using System.IO; // Added for Path
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Manages the main menu interactions including starting a new game, loading a game, and quitting.
/// </summary>
public class MainMenu : MonoBehaviour
{
    [Header("UI Panels")]
    [Tooltip("Panel containing slot selection buttons.")]
    public GameObject slotSelectionPanel;

    [Header("Slot Buttons")]
    [Tooltip("Array of buttons representing save slots.")]
    public Button[] slotButtons; // Assign the three slot buttons in the Inspector

    [Header("Slot Texts")]
    [Tooltip("Array of TextMeshProUGUI elements displaying slot status.")]
    public TextMeshProUGUI[] slotTexts; // Assign the Text components of the slot buttons

    [Header("Debugging")]
    [Tooltip("Enable or disable debug logs.")]
    [SerializeField] private bool enableDebugLogs = true; // Toggle debug logs on/off

    private enum MenuState { None, NewGame, LoadGame }
    private MenuState currentState = MenuState.None;

    private void Start()
    {
        // Validate assignments
        if (slotSelectionPanel == null)
        {
            Debug.LogError("Slot Selection Panel is not assigned in the Inspector.");
            return;
        }

        if (slotButtons.Length != slotTexts.Length)
        {
            Debug.LogError("The number of slot buttons and slot texts do not match.");
            return;
        }

        // Initialize the UI
        slotSelectionPanel.SetActive(false);
        UpdateSlotDisplay();
    }

    /// <summary>
    /// Handler for the "New Game" button click.
    /// </summary>
    public void OnNewGameButton()
    {
        if (enableDebugLogs) Debug.Log("New Game Button Clicked");
        currentState = MenuState.NewGame;
        ShowSlotSelection();
    }

    /// <summary>
    /// Handler for the "Load Game" button click.
    /// </summary>
    public void OnLoadGameButton()
    {
        if (enableDebugLogs) Debug.Log("Load Game Button Clicked");
        currentState = MenuState.LoadGame;
        ShowSlotSelection();
    }

    /// <summary>
    /// Handler for the "Quit" button click.
    /// </summary>
    public void OnQuitButton()
    {
        if (enableDebugLogs) Debug.Log("Quit Button Clicked");
        Application.Quit();
    }

    /// <summary>
    /// Displays the slot selection panel and updates slot statuses.
    /// </summary>
    private void ShowSlotSelection()
    {
        slotSelectionPanel.SetActive(true);
        UpdateSlotDisplay();
    }

    /// <summary>
    /// Updates the interactable state and text of each save slot button based on current state.
    /// </summary>
    private void UpdateSlotDisplay()
    {
        for (int i = 0; i < slotButtons.Length; i++)
        {
            int slotNumber = i + 1; // 1-based indexing
            bool slotExists = SaveSystem.SaveFileExists(slotNumber);

            if (enableDebugLogs)
                Debug.Log($"[MainMenu] Slot {slotNumber} Exists: {slotExists}");

            // Determine if the button should be interactable
            bool interactable = currentState == MenuState.NewGame ? !slotExists : slotExists;
            slotButtons[i].interactable = interactable;

            // Update the slot text
            slotTexts[i].text = $"Slot {slotNumber}\n" + (slotExists ? "Saved" : "Empty");
        }
    }

    /// <summary>
    /// Handler when a slot button is selected.
    /// </summary>
    /// <param name="slotNumber">1-based slot number.</param>
    public void OnSlotSelected(int slotNumber)
    {
        if (enableDebugLogs) Debug.Log($"Slot {slotNumber} Selected");

        if (currentState == MenuState.NewGame)
        {
            StartNewGame(slotNumber);
        }
        else if (currentState == MenuState.LoadGame)
        {
            LoadGame(slotNumber);
        }
    }

    /// <summary>
    /// Convenience method for Slot 1 selection.
    /// </summary>
    public void OnSlot1Selected()
    {
        OnSlotSelected(1);
    }

    /// <summary>
    /// Convenience method for Slot 2 selection.
    /// </summary>
    public void OnSlot2Selected()
    {
        OnSlotSelected(2);
    }

    /// <summary>
    /// Convenience method for Slot 3 selection.
    /// </summary>
    public void OnSlot3Selected()
    {
        OnSlotSelected(3);
    }

    /// <summary>
    /// Initiates a new game in the specified slot.
    /// </summary>
    /// <param name="slotNumber">1-based slot number.</param>
    private void StartNewGame(int slotNumber)
    {
        if (enableDebugLogs) Debug.Log($"Starting New Game in Slot {slotNumber}");

        // Ensure PlayerManager exists
        if (PlayerManager.Instance != null)
        {
            // Reset all player data
            PlayerManager.Instance.ResetAllData();

            // Set the current slot number
            PlayerManager.Instance.currentSlotNumber = slotNumber;

            // Create and save a new game in the selected slot
            PlayerManager.Instance.SaveGame();

            // Optionally, provide user feedback (e.g., a loading screen)

            // Load the starting scene
            string startingSceneName = "scene1_1"; // Replace with your actual starting scene name
            if (SceneExists(startingSceneName))
            {
                SceneManager.LoadScene(startingSceneName);
            }
            else
            {
                Debug.LogError($"Starting scene '{startingSceneName}' does not exist. Please check the scene name.");
            }
        }
        else
        {
            Debug.LogError("PlayerManager instance is null. Ensure PlayerManager exists in the scene.");
        }
    }

    /// <summary>
    /// Initiates loading a game from the specified slot.
    /// </summary>
    /// <param name="slotNumber">1-based slot number.</param>
    private void LoadGame(int slotNumber)
    {
        if (enableDebugLogs) Debug.Log($"Loading Game from Slot {slotNumber}");

        // Ensure PlayerManager exists
        if (PlayerManager.Instance != null)
        {
            // Load the game from the selected slot
            PlayerManager.Instance.LoadGame(slotNumber);
        }
        else
        {
            Debug.LogError("PlayerManager instance is null. Ensure PlayerManager exists in the scene.");
        }
    }

    /// <summary>
    /// Checks if a scene with the given name exists in the build settings.
    /// </summary>
    /// <param name="sceneName">Name of the scene to check.</param>
    /// <returns>True if the scene exists; otherwise, false.</returns>
    private bool SceneExists(string sceneName)
    {
        int sceneCount = SceneManager.sceneCountInBuildSettings;
        string[] allScenePaths = new string[sceneCount];
        for (int i = 0; i < sceneCount; i++)
        {
            allScenePaths[i] = Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(i));
            if (allScenePaths[i].Equals(sceneName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }
}
