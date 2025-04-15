using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class DeathBringer : MonoBehaviour, IChaseZoneUser, IEnemyAggro
{
    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private SwordAttack swordAttackZone; // Reference to the SwordAttack script
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private DetectionZone attackZone; // DetectionZone is back here
    [SerializeField] private Transform[] patrolPoints;
    [SerializeField] private Transform playerTransform;
    [SerializeField] private AudioSource swingSoundSource; // AudioSource for swing sound
    [SerializeField] private AudioSource hitSoundSource; // AudioSource for hit sound
    [SerializeField] private ChaseZone chaseZone; // Reference to the ChaseZone script
    private PlayerHealth playerHealth;

    [Header("Movement")]
    [SerializeField] private float patrolSpeed = 2f; // Patrol speed
    [SerializeField] private float chaseSpeed = 4f;  // Chase speed
    [SerializeField] private float walkStopRate = 0.6f;
    private int patrolDestination = 0;

    [Header("Chasing")]
    [SerializeField] private bool isChasing;
    [SerializeField] private float chaseDistanceAggro; // Aggro chase distance
    [SerializeField] public float damage = 2;
    private bool isAggro = false; // Aggro state for chasing the player
    private bool isPlayerInChaseZone = false; // Tracks whether the player is in the ChaseZone

    [Header("Proximity Detection")]
    [SerializeField] private float proximityDetectionRadius = 1.5f; // Enemy detects player within this radius
    [SerializeField] private float proximityDetectionOffsetX = 3.5f;   // X offset for proximity detection (starts at 3.5)
    [SerializeField] private float proximityDetectionOffsetY = 0f;   // Y offset for proximity detection
    private bool isPlayerInProximity = false; // Flag to check if the player is in proximity
    private int facingDirection = 1; // Tracks the direction the enemy is facing (1 for right, -1 for left)

    // New variable to track previous aggro state
    private bool previousAggroState = false;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
        GameObject player = GameObject.FindGameObjectWithTag("Player");

        if (player != null)
        {
            playerTransform = player.transform;
            playerHealth = player.GetComponent<PlayerHealth>();
        }

        // Initialize layer to Enemy by default
        previousAggroState = false;
        gameObject.layer = LayerMask.NameToLayer("Enemy");
    }

    void Update()
    {
        // Disable chasing if playerTransform is destroyed
        if (playerTransform == null)
        {
            isChasing = false;
        }
        else
        {
            // Using the detection zone to check if the player is in range
            HasTarget = attackZone.detectedColliders.Count > 0;
        }

        // Calculate the center of the proximity detection area
        Vector2 proximityCenter = new Vector2(transform.position.x + proximityDetectionOffsetX, transform.position.y + proximityDetectionOffsetY);
        isPlayerInProximity = Vector2.Distance(proximityCenter, playerTransform.position) <= proximityDetectionRadius;

        // Update aggro state and chase logic
        if (!isAggro)
        {
            if (isPlayerInChaseZone || isPlayerInProximity) // Trigger aggro based on proximity or chase zone
            {
                SetAggro(true); // Set aggro when player enters the chase zone or proximity
                isChasing = true; // Start chasing the player
            }
        }

        if (isAggro)
        {
            float distanceToPlayer = Vector2.Distance(transform.position, playerTransform.position);
            if (distanceToPlayer > chaseDistanceAggro)
            {
                SetAggro(false); // Stop chasing if the player is out of aggro range
                isChasing = false;
            }
        }

        // Update Layer Based on Aggro
        UpdateLayerBasedOnAggro();
    }

    private void FixedUpdate()
    {
        if (playerTransform != null) // Ensure playerTransform is not null
        {
            if (isChasing)
            {
                ChasePlayer();
            }
            else
            {
                Patrol();
            }
        }
        else
        {
            rb.linearVelocity = Vector2.zero;
        }
    }

    public void SetAggro(bool aggro)
    {
        isAggro = aggro;
        UpdateLayerBasedOnAggro(); // Ensure layer is updated immediately

        // Change the ChaseZone's layer based on the aggro state
        if (chaseZone != null)
        {
            chaseZone.gameObject.layer = aggro ? LayerMask.NameToLayer("EnemyHitboxAggro") : LayerMask.NameToLayer("EnemyHitbox");
        }

        // Change the DetectionZone's layer based on the aggro state
        if (attackZone != null)
        {
            attackZone.gameObject.layer = aggro ? LayerMask.NameToLayer("EnemyHitboxAggro") : LayerMask.NameToLayer("EnemyHitbox");
        }

        // Change the SwordAttack's layer based on the aggro state
        if (swordAttackZone != null)
        {
            swordAttackZone.gameObject.layer = aggro ? LayerMask.NameToLayer("EnemyHitboxAggro") : LayerMask.NameToLayer("EnemyHitbox");
        }

        if (aggro)
        {
            isChasing = true; // Start chasing when aggro is triggered
        }
        else
        {
            isChasing = false; // Stop chasing when aggro is removed
        }
    }

    void ChasePlayer()
    {
        // Check to ensure playerTransform is not null
        if (playerTransform == null) return;

        float direction = transform.position.x > playerTransform.position.x ? -1 : 1;
        if (direction != facingDirection)
        {
            // Flip the sprite and reverse the proximity offset X between 3.5 and -3.5
            FlipProximity(direction);
        }

        float currentMoveSpeed = chaseSpeed; // Use chase speed

        if (CanMove)
        {
            rb.linearVelocity = new Vector2(direction * currentMoveSpeed, rb.linearVelocity.y);
        }
        else
        {
            rb.linearVelocity = new Vector2(Mathf.Lerp(rb.linearVelocity.x, 0, walkStopRate), rb.linearVelocity.y);
        }

        transform.localScale = new Vector3(Mathf.Sign(direction) * Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
    }

    void Patrol()
    {
        Vector2 targetPos = patrolPoints[patrolDestination].position;
        targetPos.y = rb.position.y;
        Vector2 newPos = Vector2.MoveTowards(rb.position, targetPos, patrolSpeed * Time.fixedDeltaTime); // Use patrol speed
        rb.MovePosition(newPos);

        float direction = targetPos.x > rb.position.x ? 1 : -1;

        if (direction != facingDirection)
        {
            // Flip the sprite and reverse the proximity offset X between 3.5 and -3.5
            FlipProximity(direction);
        }

        transform.localScale = new Vector3(direction * Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);

        if (Vector2.Distance(rb.position, targetPos) < 0.2f)
        {
            patrolDestination = (patrolDestination + 1) % patrolPoints.Length;
        }
    }

    void FlipProximity(float direction)
    {
        facingDirection = (int)direction;
        proximityDetectionOffsetX = direction == 1 ? -3.5f : 3.5f; // Set proximity offset to 3.5 or -3.5 based on direction
    }

    public void AttemptDealDamage()
    {
        // Now using the detection zone to check for detected colliders (players or targets)
        if (swordAttackZone.detectedColliders.Any(collider => collider.CompareTag("Player")))
        {
            DealDamageToPlayer();
        }
    }

    private void DealDamageToPlayer()
{
    if (HasTarget && playerHealth != null && Vector2.Distance(transform.position, playerHealth.transform.position) <= chaseDistanceAggro)
    {
        bool damageApplied = playerHealth.TakeDamage(damage); // Capture return value

        if (damageApplied)
        {
            hitSoundSource?.Play(); // Play DeathBringer's hit sound
            SetAggro(true); // Aggro when player takes damage within chase distance aggro
            isChasing = true; // Start chasing
        }
        // No action needed if attack was evaded
    }
}


    // Method to be called by Animation Event
    public void PlayAttackSound()
    {
        if (swingSoundSource != null)
        {
            swingSoundSource.Play();
        }
    }

    // Properties for animation control
    public bool _hasTarget = false;
    public bool HasTarget
    {
        get { return _hasTarget; }
        private set
        {
            _hasTarget = value && playerTransform != null && Vector2.Distance(transform.position, playerTransform.position) <= chaseDistanceAggro;
            animator.SetBool("hasTarget", _hasTarget);
        }
    }

    public bool CanMove
    {
        get
        {
            return animator.GetBool("canMove");
        }
    }

    // Implementation of IChaseZoneUser methods
    public void SetPlayerInChaseZone(bool isInZone)
    {
        isPlayerInChaseZone = isInZone;
        if (isInZone)
        {
            SetAggro(true); // Set aggro when player enters the chase zone
        }
    }

    public bool IsAggroed
    {
        get { return isAggro; }
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.red; // For chaseDistanceAggro visualization
        Gizmos.DrawWireSphere(transform.position, chaseDistanceAggro);

        Gizmos.color = Color.green; // For proximity detection radius visualization
        Vector2 proximityCenter = new Vector2(transform.position.x + proximityDetectionOffsetX, transform.position.y + proximityDetectionOffsetY);
        Gizmos.DrawWireSphere(proximityCenter, proximityDetectionRadius);
    }

    /// <summary>
    /// Updates the layer of the DeathBringer based on the current aggro state.
    /// </summary>
    private void UpdateLayerBasedOnAggro()
    {
        if (isAggro != previousAggroState)
        {
            if (isAggro)
            {
                gameObject.layer = LayerMask.NameToLayer("EnemyAggro");
                Debug.Log($"{gameObject.name} is now in EnemyAggro layer.");
            }
            else
            {
                gameObject.layer = LayerMask.NameToLayer("Enemy");
                Debug.Log($"{gameObject.name} reverted to Enemy layer.");
            }
            previousAggroState = isAggro;
        }
    }
}
