// Archer.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Archer : MonoBehaviour, IEnemyAggro
{
    [SerializeField] private float shootingSpeed = 3f;
    [SerializeField] private int damage = 3;
    [SerializeField] private Transform shootingPoint;
    [SerializeField] private GameObject arrowPrefab;
    [SerializeField] private Animator animator;
    [SerializeField] private float sightRange = 10f; // Public value for sight range

    private AudioSource audioSource;
    private float timeSinceLastShot = 0f;
    private Transform playerTransform;

    private bool playerInDetectionZone = false;

    // Aggro variables
    private bool aggro = false;

    public bool IsAggroed
    {
        get { return aggro; }
    }

    public void SetAggro(bool isAggro)
    {
        aggro = isAggro;
    }

    private void Awake()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
        }
        audioSource = GetComponent<AudioSource>();
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            playerInDetectionZone = true;
            animator.SetBool("inRange", true);
            SetAggro(true); // Set aggro when player enters detection zone
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            playerInDetectionZone = false;
            animator.SetBool("inRange", false);
        }
    }

    private void Update()
    {
        if (playerTransform != null)
        {
            float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);

            // Update facing direction based on sight range only
            if (distanceToPlayer <= sightRange)
            {
                Vector3 direction = playerTransform.position - transform.position;
                if (direction.x > 0)
                {
                    // Face right
                    transform.localScale = new Vector3(1, 1, 1);
                }
                else if (direction.x < 0)
                {
                    // Face left
                    transform.localScale = new Vector3(-1, 1, 1);
                }
            }

            // Handle shooting based on detection zone only
            if (playerInDetectionZone)
            {
                HandleShooting();
            }
        }
    }

    private void HandleShooting()
    {
        timeSinceLastShot += Time.deltaTime;
        if (timeSinceLastShot >= shootingSpeed)
        {
            animator.SetTrigger("Shoot"); // Trigger shooting animation
            timeSinceLastShot = 0f;
            // Note: Actual arrow shooting and sound playing will now be handled by an animation event.
        }
    }

    // This method is intended to be called by an animation event
    private void ShootArrow()
    {
        // Offset values to adjust the spawn position
        float offsetX = -1.1f; // Adjust as needed for leftward positioning
        float offsetY = -1.3f; // Adjust as needed for downward positioning

        // Calculate new spawn position with offset
        Vector3 spawnPosition = new Vector3(shootingPoint.position.x + offsetX, shootingPoint.position.y + offsetY, shootingPoint.position.z);

        // Instantiate the arrow at the adjusted shooting point
        GameObject arrow = Instantiate(arrowPrefab, spawnPosition, Quaternion.identity);
        Arrow arrowComponent = arrow.GetComponent<Arrow>();
        if (arrowComponent != null)
        {
            Vector2 direction = new Vector2(Mathf.Sign(transform.localScale.x), 0);
            arrowComponent.SetDirection(direction);
            arrowComponent.damage = this.damage;

            // Adjust arrow's scale to reverse the sprite direction based on the archer's facing direction
            float arrowScaleDirection = transform.localScale.x > 0 ? 1f : -1f;
            arrow.transform.localScale = new Vector3(arrowScaleDirection * Mathf.Abs(arrow.transform.localScale.x), arrow.transform.localScale.y, arrow.transform.localScale.z);
        }
    }
}
