using System.Collections.Generic; // For List<T>
using UnityEngine;                // For MonoBehaviour, Collider2D, etc.

public class DetectionZone : MonoBehaviour
{
    public List<Collider2D> detectedColliders = new List<Collider2D>();
    private IEnemyAggro enemyAggro;

    private void Awake()
    {
        enemyAggro = GetComponentInParent<IEnemyAggro>();
        if (enemyAggro == null)
        {
            Debug.LogError("DetectionZone requires a parent with IEnemyAggro implementation.");
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            Player player = collision.GetComponent<Player>();

            if (player != null && enemyAggro != null)
            {
                // If the player is crouching in bushes AND the enemy is not aggroed, prevent detection
                if (player.isCrouchingInBushes && !enemyAggro.IsAggroed)
                {
                    return; // Do not add to detectedColliders if the player is hidden and the enemy is not aggroed
                }
            }
        }
        detectedColliders.Add(collision);
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        detectedColliders.Remove(collision);
    }
}
