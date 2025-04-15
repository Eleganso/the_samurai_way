using UnityEngine;

public class FlaskFlinger : MonoBehaviour
{
    [Header("UI References")]
    public GameObject flaskAllocationUIPanel; // Reference to the FlaskAllocationUI panel

    private bool isPlayerNear = false; // Track if the player is near the Flask Flinger
    private bool isUIOpen = false;     // Track if the FlaskAllocationUI is currently open

    private void Awake()
    {
        if (flaskAllocationUIPanel != null)
        {
            flaskAllocationUIPanel.SetActive(false); // Disable UI on Awake
        }
        else
        {
            Debug.LogError("FlaskAllocationUIPanel is not assigned in the Inspector.");
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerNear = true;
            Debug.Log("Player is near Flask Flinger.");
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerNear = false;
            Debug.Log("Player left Flask Flinger.");
            if (flaskAllocationUIPanel != null && flaskAllocationUIPanel.activeSelf)
            {
                flaskAllocationUIPanel.SetActive(false); // Hide UI if player walks away
                isUIOpen = false;
            }
        }
    }

    private void Update()
    {
        if (isPlayerNear && UserInput.instance.IsUseActionTriggered())
        {
            if (!isUIOpen)
            {
                if (flaskAllocationUIPanel != null)
                {
                    flaskAllocationUIPanel.SetActive(true);
                    isUIOpen = true;
                    Debug.Log("Flask Allocation UI activated");
                }
            }
            // No else case to prevent toggling off when button is released
        }
    }

    /// <summary>
    /// Called by the FlaskAllocationUI when Confirm or Cancel is pressed.
    /// </summary>
    public void CloseFlaskAllocationUI()
    {
        if (flaskAllocationUIPanel != null && flaskAllocationUIPanel.activeSelf)
        {
            flaskAllocationUIPanel.SetActive(false);
            isUIOpen = false;
            Debug.Log("Flask Allocation UI closed");
        }
    }
}
