using UnityEngine;

public class FlaskUpgradePickup : MonoBehaviour
{
    public enum UpgradeType
    {
        Capacity,
        HpHealing,
        ManaRefill
    }

    public UpgradeType upgradeType;

    public string pickupID; // Unique ID for this pickup

    private void Start()
    {
        // Check if this pickup has already been collected
        if (GameManager.Instance.IsPickupCollected(pickupID))
        {
            // Destroy or disable the pickup
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            PlayerManager playerManager = PlayerManager.Instance;

            switch (upgradeType)
            {
                case UpgradeType.Capacity:
                    playerManager.UpgradeFlaskCapacity();
                    break;
                case UpgradeType.HpHealing:
                    playerManager.UpgradeHpFlask();
                    break;
                case UpgradeType.ManaRefill:
                    playerManager.UpgradeManaFlask();
                    break;
            }

            playerManager.SaveFlaskData();

            // Mark this pickup as collected
            GameManager.Instance.MarkPickupAsCollected(pickupID);
            GameManager.Instance.SaveCollectedPickups(); // Save after collecting

            Destroy(gameObject);
        }
    }
}
