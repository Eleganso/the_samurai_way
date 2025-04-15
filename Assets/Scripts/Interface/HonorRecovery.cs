using UnityEngine;

public class HonorRecovery : MonoBehaviour
{
    private int honorPointsToRecover;

    public void SetHonorPoints(int points)
    {
        honorPointsToRecover = Mathf.FloorToInt(points * 0.70f); // Recover 70% of the lost honor points
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerManager.Instance.RecoverLostHonorPoints();
            Destroy(gameObject); // Destroy the recovery object after picking it up
        }
    }
}
