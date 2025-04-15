using UnityEngine;

public class SafeZone : MonoBehaviour
{
    public string safeZoneID;
    public bool isActive = false;
    public GameObject appearObject; // Object to appear when activated
    public float activationDistance = 5f; // Distance within which the SafeZone will activate

    private GameObject player;
    private bool isPlayerNear = false; // Track if the player is near the SafeZone

    private void Start()
    {
        if (GameManager.Instance.IsSafeZoneActive(safeZoneID))
        {
            ActivateSafeZone();
        }
        else
        {
            if (appearObject != null)
            {
                appearObject.SetActive(false);
            }
        }

        player = GameObject.FindWithTag("Player");
    }

    private void Update()
    {
        if (player != null && !isActive)
        {
            float distance = Vector2.Distance(player.transform.position, transform.position);
            if (distance <= activationDistance)
            {
                ActivateSafeZone();
                PlayerManager.Instance.SaveGame();
                
            }
        }

        // Check for interaction input when the player is near the SafeZone
        if (isPlayerNear && UserInput.instance.IsUseActionTriggered())
        {
            UseSafeZone();
        }
    }

    public void ActivateSafeZone()
    {
        isActive = true;
        if (appearObject != null)
        {
            appearObject.SetActive(true);
        }
        GameManager.Instance.SetSafeZone(safeZoneID, true);
        Debug.Log($"SafeZone {safeZoneID} activated.");
    }

    public void DeactivateSafeZone()
    {
        isActive = false;
        if (appearObject != null)
        {
            appearObject.SetActive(false);
        }
    }

    public void UseSafeZone()
    {
        if (!isActive)
        {
            ActivateSafeZone();
        }

        // Deduct honor points and update UI
        int currentHonorPoints = PlayerManager.Instance.GetHonorPoints();
        int pointsToDeduct = Mathf.FloorToInt(currentHonorPoints * 0.10f); // 10% deduction
        PlayerManager.Instance.DeductHonorPoints(pointsToDeduct);
        PlayerManager.Instance.SaveHonorPoints();
        PlayerManager.Instance.UpdateHonorPointsUI();
        Debug.Log($"Used SafeZone {safeZoneID}. Honor Points deducted and saved.");

        // Trigger the player's UseSafeZone method
        PlayerHealth playerHealth = player.GetComponent<PlayerHealth>();
        if (playerHealth != null)
        {
            playerHealth.UseSafeZone();
            PlayerManager.Instance.ReplenishFlasks(); // Replenish flasks upon using safe zone
            PlayerManager.Instance.SaveGame();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerNear = true;
            player = other.gameObject; // Update the player reference
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerNear = false;
        }
    }
}
