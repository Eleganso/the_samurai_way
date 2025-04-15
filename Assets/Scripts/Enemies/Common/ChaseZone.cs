using UnityEngine; 

public class ChaseZone : MonoBehaviour
{
    private IChaseZoneUser enemy;
    private IEnemyAggro enemyAggro;
    private bool isPlayerInZone = false;
    private Player player;

    public void SetEnemy(IChaseZoneUser enemy)
    {
        this.enemy = enemy;
    }

    private void Awake()
    {
        // Get the enemy references from the parent object
        if (enemy == null)
        {
            enemy = GetComponentInParent<IChaseZoneUser>();
            if (enemy == null)
            {
                Debug.LogError("ChaseZone requires a parent with IChaseZoneUser implementation.");
            }
        }

        enemyAggro = GetComponentInParent<IEnemyAggro>();
        if (enemyAggro == null)
        {
            Debug.LogError("ChaseZone requires a parent with IEnemyAggro implementation.");
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            player = collision.GetComponent<Player>();
            isPlayerInZone = true;

            // If the player is crouching in bushes and the enemy is NOT aggroed, ignore them
            if (player != null && (!player.isCrouchingInBushes || enemyAggro.IsAggroed))
            {
                enemy.SetPlayerInChaseZone(true);
            }
            else
            {
                enemy.SetPlayerInChaseZone(false);
            }
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            isPlayerInZone = false;
            enemy.SetPlayerInChaseZone(false);
            player = null;
        }
    }

    private void Update()
    {
        if (isPlayerInZone && player != null)
        {
            // Check if the player is hidden in bushes and if the enemy is aggroed
            if (player.isCrouchingInBushes && !enemyAggro.IsAggroed)
            {
                // Player is hidden, and enemy is not aggroed, so stop chasing
                enemy.SetPlayerInChaseZone(false);
            }
            else
            {
                // Player is detectable, and the chase zone should remain active
                enemy.SetPlayerInChaseZone(true);
            }
        }
    }
}
