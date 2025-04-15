using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Shielder : MonoBehaviour, IEnemyActions, IChaseZoneUser, IEnemyAggro
{
    [Header("Attack Settings")]
    [SerializeField] private float minTimeBeforeAttack = 2f;
    [SerializeField] private float maxTimeBeforeAttack = 6f;
    private float attackTimer;
    private float timeToNextAttack;
    [SerializeField] private GameObject shieldBlock; // Reference to the ShieldBlock GameObject
    [SerializeField] private Animator animator;
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private DetectionZone attackZone;
    [SerializeField] private SwordAttack swordAttackZone; // Reference to the SwordAttack script
    [SerializeField] private ChaseZone chaseZone; // Reference to the ChaseZone

    [Header("Patrol Settings")]
    [SerializeField] private Transform[] patrolPoints;
    [SerializeField] private float patrolSpeed = 1.5f; // Speed for patrolling
    [SerializeField] private float moveSpeed = 4f;     // Speed for chasing
    [SerializeField] private float walkStopRate = 0.6f;
    private int patrolDestination = 0;

    [Header("Chase Settings")]
    [SerializeField] private Transform playerTransform;
    [SerializeField] private float chaseDistanceAggro = 10f; // Aggro chase distance
    [SerializeField] public float damage = 2;
    private bool isChasing = false;
    private bool isPlayerInChaseZone = false; // Is player in chase zone
    public bool aggro = false; // Aggro state, set to true when Shielder is aggroed

    [Header("Proximity Detection")]
    [SerializeField] private float proximityDetectionRadius = 1.5f; // Proximity detection radius
    [SerializeField] private float proximityDetectionOffsetX = 0f;
    [SerializeField] private float proximityDetectionOffsetY = 0f;
    private bool isPlayerInProximity = false;

    [Header("Block Settings")]
    [SerializeField] private float shieldOffDistance = 5f; // Distance at which shield is turned off and enemy runs
    [SerializeField] private float blockMoveSpeedMultiplier = 0.5f; // Multiplier for move speed when blocking
    public bool isBlocking = false;

    [Header("Sound Settings")]
    [SerializeField] private AudioSource swingSoundSource; // AudioSource for swing sound
    [SerializeField] private AudioSource hitSoundSource; // AudioSource for hit sound
    [SerializeField] private AudioSource blockSoundSource; // AudioSource for block sound
    [SerializeField] private float blockSoundCooldown = 0.5f; // Cooldown time for block sound
    private float blockSoundTimer = 0f; // Timer to track block sound cooldown

    [Header("Disable Behavior Settings")]
    [SerializeField] private float disableDuration = 2.5f; // Duration to disable behaviors after top collision
    [SerializeField] private float disableMoveSpeed = 1f; // Move speed during the disable behavior period
    private bool isBehaviorDisabled = false; // Indicates whether behaviors are temporarily disabled
    private bool playerOnHead = false; // Indicates if the player is on the Shielder's head

    private PlayerHealth playerHealth;

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

        // Assign chaseZone if it wasn't manually set
        if (chaseZone == null)
        {
            chaseZone = GetComponentInChildren<ChaseZone>();
            if (chaseZone != null)
            {
                chaseZone.SetEnemy(this); // Set this Shielder as the chase zone user
            }
            else
            {
                Debug.LogError("ChaseZone not found. Please ensure the ChaseZone script is attached to a child object.");
            }
        }
    }

    private void Start()
    {
        animator.SetBool("isRunning", true); // Set the isRunning animator parameter
    }

    void Update()
    {
        if (isBehaviorDisabled) return; // Skip behavior if disabled

        HasTarget = attackZone.detectedColliders.Count > 0;

        // Proximity detection for non-stealthy players
        Vector2 proximityCenter = new Vector2(transform.position.x + proximityDetectionOffsetX, transform.position.y + proximityDetectionOffsetY);
        isPlayerInProximity = Vector2.Distance(proximityCenter, playerTransform.position) <= proximityDetectionRadius;

        // Update aggro state and chase logic
        if (!aggro)
        {
            if (isPlayerInChaseZone || isPlayerInProximity)
            {
                SetAggro(true); // Set aggro when player is in chase zone or proximity
            }
        }

        if (aggro)
        {
            float distanceToPlayer = Vector2.Distance(transform.position, playerTransform.position);
            if (distanceToPlayer <= chaseDistanceAggro)
            {
                isChasing = true;
            }
            else
            {
                SetAggro(false);
                isChasing = false;
            }
        }

        if (isBlocking)
        {
            attackTimer += Time.deltaTime;
            if (attackTimer >= timeToNextAttack)
            {
                TriggerAttack();
                attackTimer = 0f;
                timeToNextAttack = Random.Range(minTimeBeforeAttack, maxTimeBeforeAttack);
            }
        }

        if (isBlocking && Vector2.Distance(transform.position, playerTransform.position) > shieldOffDistance)
        {
            BlockNo(); // Stop blocking
            animator.SetBool("isRunning", true);
        }
        else if (!isBlocking && !isChasing)
        {
            animator.SetBool("isRunning", false);
        }

        if (blockSoundTimer > 0)
        {
            blockSoundTimer -= Time.deltaTime;
        }

        // Update Layer Based on Aggro
        UpdateLayerBasedOnAggro();
    }

    private void FixedUpdate()
    {
        if (isBehaviorDisabled)
        {
            PatrolWithSpeed(disableMoveSpeed);
            return;
        }

        if (isChasing)
        {
            ChasePlayer(blockMoveSpeedMultiplier);
        }
        else
        {
            Patrol();
        }
    }

    void ChasePlayer(float speedMultiplier)
    {
        float direction = transform.position.x > playerTransform.position.x ? -1 : 1;
        float currentMoveSpeed = isBlocking ? moveSpeed * speedMultiplier : moveSpeed;

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
        PatrolWithSpeed(patrolSpeed); // Use patrol speed when patrolling
    }

    void PatrolWithSpeed(float currentMoveSpeed)
    {
        Vector2 targetPos = patrolPoints[patrolDestination].position;
        targetPos.y = rb.position.y;
        Vector2 newPos = Vector2.MoveTowards(rb.position, targetPos, currentMoveSpeed * Time.fixedDeltaTime);
        rb.MovePosition(newPos);
        transform.localScale = new Vector3(patrolDestination == 0 ? -1 : 1, 1, 1);

        if (Vector2.Distance(rb.position, targetPos) < 0.2f)
        {
            patrolDestination = (patrolDestination + 1) % patrolPoints.Length;
        }
    }

    public void AttemptDealDamage()
    {
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
            hitSoundSource?.Play(); // Play Shielder's hit sound
            SetAggro(true); // Aggro when player takes damage within chase distance aggro
            isChasing = true; // Start chasing
        }
        // No action needed if attack was evaded
    }
}


    public void PlayAttackSound()
    {
        if (swingSoundSource != null)
        {
            swingSoundSource.Play();
        }
    }

    public void BlockYes()
    {
        shieldBlock.SetActive(true);
        animator.SetBool("blocking", true);
        isBlocking = true;
        timeToNextAttack = Random.Range(minTimeBeforeAttack, maxTimeBeforeAttack);
    }

    public void BlockNo()
    {
        shieldBlock.SetActive(false);
        animator.SetBool("blocking", false);
        isBlocking = false;
    }

    private void TriggerAttack()
    {
        animator.SetTrigger("attack");
    }

    public void TriggerBlock()
    {
        animator.SetTrigger("block");

        if (blockSoundTimer <= 0f && blockSoundSource != null)
        {
            blockSoundSource.Play();
            blockSoundTimer = blockSoundCooldown;
        }
    }

    public void HandleHeadCollision()
    {
        Debug.Log("Player collided on Shielder's head.");
        playerOnHead = true;
        StartCoroutine(DisableBehaviorForDuration(disableDuration));
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.collider.CompareTag("Player") && collision.otherCollider.CompareTag("EnemyHead"))
        {
            HandleHeadCollision();
        }
    }

    private IEnumerator DisableBehaviorForDuration(float duration)
    {
        isBehaviorDisabled = true;
        isChasing = false;
        HasTarget = false;
        SetAggro(false); // Reset aggro when behavior is disabled

        yield return new WaitForSeconds(duration);

        isBehaviorDisabled = false;
        playerOnHead = false;
    }

    public bool _hasTarget = false;
    public bool HasTarget
    {
        get { return _hasTarget; }
        private set
        {
            float distanceToPlayer = Vector2.Distance(transform.position, playerTransform.position);

            if (value && distanceToPlayer <= chaseDistanceAggro)
            {
                _hasTarget = true;
            }
            else
            {
                _hasTarget = false;
            }

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

    public void OnEveryDamageTaken()
    {
        Debug.Log("Shielder has taken damage.");
        float distanceToPlayer = Vector2.Distance(transform.position, playerTransform.position);
        if (distanceToPlayer <= chaseDistanceAggro)
        {
            SetAggro(true);
            isChasing = true;
        }
    }

    public void OnSomeDamageTaken()
    {
        Debug.Log("Shielder took some damage, but only triggers specific action based on conditions.");
    }

    public void SetPlayerInChaseZone(bool isInZone)
    {
        isPlayerInChaseZone = isInZone;
        if (isInZone)
        {
            SetAggro(true);
        }
    }

    // Implementation of IEnemyAggro interface
    public bool IsAggroed
    {
        get { return aggro; }
    }

    public void SetAggro(bool isAggro)
    {
        aggro = isAggro;
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
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, chaseDistanceAggro);

        // For proximity detection gizmo
        Gizmos.color = Color.green;
        Vector2 proximityCenter = new Vector2(transform.position.x + proximityDetectionOffsetX, transform.position.y + proximityDetectionOffsetY);
        Gizmos.DrawWireSphere(proximityCenter, proximityDetectionRadius);
    }

    /// <summary>
    /// Updates the layer of the Shielder based on the current aggro state.
    /// </summary>
    private void UpdateLayerBasedOnAggro()
    {
        if (aggro != previousAggroState)
        {
            if (aggro)
            {
                gameObject.layer = LayerMask.NameToLayer("EnemyAggro");
                Debug.Log($"{gameObject.name} is now in EnemyAggro layer.");
            }
            else
            {
                gameObject.layer = LayerMask.NameToLayer("Enemy");
                Debug.Log($"{gameObject.name} reverted to Enemy layer.");
            }
            previousAggroState = aggro;
        }
    }
}
