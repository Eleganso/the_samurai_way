using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class FlaskAllocationUI : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private Slider allocationSlider; // Assign in Inspector
    [SerializeField] private TextMeshProUGUI hpFlasksText; // Assign in Inspector
    [SerializeField] private TextMeshProUGUI mpFlasksText; // Assign in Inspector
    [SerializeField] private Button confirmButton; // Assign in Inspector
    [SerializeField] private Button cancelButton; // **New Reference** - Assign in Inspector

    [Header("Dependencies")]
    [SerializeField] private FlaskFlinger flaskFlinger; // **New Reference** - Assign in Inspector

    private int maxFlasks;

    private void Start()
    {
        // Validate assignments
        if (allocationSlider == null || hpFlasksText == null || mpFlasksText == null ||
            confirmButton == null || cancelButton == null || flaskFlinger == null)
        {
            Debug.LogError("FlaskAllocationUI: One or more UI elements or dependencies are not assigned in the Inspector.");
            return;
        }

        // Get max flasks from PlayerManager
        maxFlasks = PlayerManager.Instance.flaskData.maxFlasks;

        // Initialize slider
        allocationSlider.minValue = 0;
        allocationSlider.maxValue = maxFlasks;
        allocationSlider.wholeNumbers = true;
        allocationSlider.value = PlayerManager.Instance.flaskData.allocatedHpFlasks; // Set initial value to allocated HP flasks

        // Update text displays
        UpdateFlaskTexts((int)allocationSlider.value, maxFlasks - (int)allocationSlider.value);

        // Add listener for slider value changes
        allocationSlider.onValueChanged.AddListener(OnSliderValueChanged);

        // Add listener for confirm and cancel buttons
        confirmButton.onClick.AddListener(OnConfirmButtonClicked);
        cancelButton.onClick.AddListener(OnCancelButtonClicked);
    }

    /// <summary>
    /// Updates the flask texts based on the slider value.
    /// </summary>
    /// <param name="hp">Number of HP flasks.</param>
    /// <param name="mp">Number of MP flasks.</param>
    private void UpdateFlaskTexts(int hp, int mp)
    {
        hpFlasksText.text = $"HP Flasks: {hp}";
        mpFlasksText.text = $"MP Flasks: {mp}";
    }

    /// <summary>
    /// Called when the slider value changes to update flask counts.
    /// </summary>
    /// <param name="value">Slider value.</param>
    private void OnSliderValueChanged(float value)
    {
        int hpFlasks = Mathf.RoundToInt(value);
        int mpFlasks = maxFlasks - hpFlasks;
        UpdateFlaskTexts(hpFlasks, mpFlasks);
    }

    /// <summary>
    /// Called when the Confirm button is clicked.
    /// Saves the flask distribution and closes the UI.
    /// </summary>
    private void OnConfirmButtonClicked()
    {
        int hpFlasks = Mathf.RoundToInt(allocationSlider.value);
        int mpFlasks = maxFlasks - hpFlasks;

        bool success = PlayerManager.Instance.SetFlaskDistribution(hpFlasks, mpFlasks);
        if (success)
        {
            Debug.Log($"Flask distribution updated: {hpFlasks} HP, {mpFlasks} MP.");
            CloseUI(); // **Close the UI after confirming**
        }
        else
        {
            Debug.LogError("Failed to update flask distribution.");
        }
    }

    /// <summary>
    /// Called when the Cancel button is clicked.
    /// Closes the UI without saving changes.
    /// </summary>
    private void OnCancelButtonClicked()
    {
        CloseUI(); // **Close the UI when Canceling**
    }

    /// <summary>
    /// Closes the Flask Allocation UI by interacting with FlaskFlinger.
    /// </summary>
    private void CloseUI()
    {
        if (flaskFlinger != null)
        {
            flaskFlinger.CloseFlaskAllocationUI();
        }
        else
        {
            Debug.LogError("FlaskFlinger reference is missing.");
        }
    }
}
