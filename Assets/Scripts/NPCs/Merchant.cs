using UnityEngine;

public class Merchant : MonoBehaviour
{
    public GameObject merchantPanel; // Reference to the Merchant UI panel
    private bool isPlayerNear = false; // Track if the player is near the merchant

    private void Start()
    {
        // Ensure the merchantPanel is inactive at the start
        if (merchantPanel != null)
        {
            merchantPanel.SetActive(false);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Check if the object entering the trigger is tagged as "Player"
        if (other.CompareTag("Player"))
        {
            isPlayerNear = true;
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        // Check if the object exiting the trigger is tagged as "Player"
        if (other.CompareTag("Player"))
        {
            isPlayerNear = false;

            // Hide the merchant panel when the player leaves
            if (merchantPanel != null)
            {
                merchantPanel.SetActive(false);
            }
        }
    }

    private void Update()
    {
        // Check for interaction input when the player is near the merchant
        if (isPlayerNear && UserInput.instance.IsUseActionTriggered())
        {
            if (merchantPanel != null)
            {
                merchantPanel.SetActive(true); // Show the merchant panel when 'Use' action is triggered
                Debug.Log("Merchant panel activated");
            }
        }
    }
}
