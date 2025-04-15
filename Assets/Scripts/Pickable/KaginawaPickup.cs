using UnityEngine;

public class KaginawaPickup : MonoBehaviour
{
    public string pickupID; // Unique ID for this pickup

    private void Start()
    {
        // Check if this pickup has already been collected
        if (GameManager.Instance.IsPickupCollected(pickupID))
        {
            Destroy(gameObject); // Destroy the pickup if already collected
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            PlayerManager.Instance.UnlockGrapplingHook();

            // Mark this pickup as collected
            GameManager.Instance.MarkPickupAsCollected(pickupID);
            // Save collected pickups (if necessary)
            // GameManager.Instance.SaveCollectedPickups();

            Destroy(gameObject); // Remove the pickup from the scene
        }
    }
}
