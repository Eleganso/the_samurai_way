using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ElfSwordsman : MonoBehaviour, IEnemyActions, IChaseZoneUser, IEnemyAggro
{
    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private DetectionZone detectionZone;
    [SerializeField] private SwordAttack swordAttackZone;
    [SerializeField] private Transform[] patrolPoints;
    [SerializeField] private AudioSource swingSoundSource;
    [SerializeField] private AudioSource hitSoundSource;
    [SerializeField] private ChaseZone chaseZone;
    private Transform playerTransform;
    private Player player;
    private PlayerHealth playerHealth;
    private EnemyHealth enemyHealth;

    [Header("Settings")]
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float patrolSpeed = 1.5f;
    [SerializeField] private float walkStopRate = 0.6f;
    [SerializeField] private float damage = 2;
    [SerializeField] private float staggerTimer = 1f;
    [SerializeField] private float disableDuration = 2.5f;
    [SerializeField] private float disableMoveSpeed = 1f;

    private int patrolDestination = 0;
    private bool isChasing = false;
    private bool canMove = true;
    private bool isBehaviorDisabled = false;
    private bool playerOnHead = false;
    private bool isStaggering = false;
    private bool isCharging = false;
    public bool aggro = false;

    private bool hasPerformedHalfHealthAction = false;
    private float actionCooldownTimer = 0f;
    private bool isPerformingHalfHealthAction = false;
    private bool isPerformingSomeAction = false;

    [Header("Move Away Settings")]
    [SerializeField] private float chargeDuration = 1f;
    [SerializeField] private float chargeSpeed = 5f;

    private float chargeDirectionFacing = 0f;

    [Header("Proximity Detection")]
    [SerializeField] private float proximityDetectionRadius = 1.5f;
    [SerializeField] private float proximityDetectionOffsetX = 0f;
    [SerializeField] private float proximityDetectionOffsetY = 0f;
    private bool isPlayerInProximity = false;

    [Header("Aggro Chase Distance")]
    [SerializeField] private float chaseDistanceAggro = 10f;
    private bool isPlayerInChaseZone = false;

    private bool previousAggroState = false;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
        enemyHealth = GetComponent<EnemyHealth>();

        // Find the player and its components
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
        {
            playerTransform = playerObject.transform;
            player = playerObject.GetComponent<Player>();
            playerHealth = playerObject.GetComponent<PlayerHealth>();
        }
        else
        {
            Debug.LogError("Player not found in the scene.");
        }

        // Get reference to ChaseZone
        chaseZone = GetComponentInChildren<ChaseZone>();
        if (chaseZone != null)
        {
            chaseZone.SetEnemy(this);
        }
        else
        {
            Debug.LogError("ChaseZone not found. Please ensure the ChaseZone script is attached to a child object.");
        }

        aggro = false;
        previousAggroState = false;
        gameObject.layer = LayerMask.NameToLayer("Enemy");
    }

    void Update()
    {
        if (isBehaviorDisabled || !canMove || isStaggering || isPerformingHalfHealthAction || isPerformingSomeAction || isCharging)
            return;

        HasTarget = detectionZone.detectedColliders.Count > 0;

        // Calculate the center of the proximity detection with offsets
        Vector2 proximityCenter = new Vector2(transform.position.x + proximityDetectionOffsetX, transform.position.y + proximityDetectionOffsetY);

        isPlayerInProximity = Vector2.Distance(proximityCenter, playerTransform.position) <= proximityDetectionRadius;

        bool isPlayerDetectable = true;
        if (player != null)
        {
            if (player.isCrouchingInBushes && !isPlayerInProximity)
            {
                isPlayerDetectable = false;
            }
        }

        if (!aggro)
        {
            if (isPlayerInProximity)
            {
                SetAggro(true);
            }
            else if (isPlayerDetectable && isPlayerInChaseZone)
            {
                SetAggro(true);
            }
            else
            {
                SetAggro(false);
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
        else
        {
            isChasing = false;
        }

        UpdateLayerBasedOnAggro();

        HandleHalfHealthAction();

        if (HasTarget && canMove && !isCharging)
        {
            animator.SetTrigger("attack");
        }
    }

    private void FixedUpdate()
    {
        if (isBehaviorDisabled)
        {
            PatrolWithSpeed(disableMoveSpeed);
            return;
        }

        if (isPerformingHalfHealthAction || isPerformingSomeAction)
        {
            rb.linearVelocity = new Vector2(chargeDirectionFacing * chargeSpeed, rb.linearVelocity.y);
            return;
        }

        if (playerOnHead)
        {
            PatrolWithSpeed(patrolSpeed);
            return;
        }

        if (isChasing)
        {
            ChasePlayer(moveSpeed);
        }
        else
        {
            PatrolWithSpeed(patrolSpeed);
        }
    }

    void ChasePlayer(float currentMoveSpeed)
    {
        if (isBehaviorDisabled || isStaggering || isCharging || isPerformingHalfHealthAction || isPerformingSomeAction)
            return;

        float direction = transform.position.x > playerTransform.position.x ? -1 : 1;

        if (canMove)
        {
            rb.linearVelocity = new Vector2(direction * currentMoveSpeed, rb.linearVelocity.y);
        }
        else
        {
            rb.linearVelocity = new Vector2(Mathf.Lerp(rb.linearVelocity.x, 0, walkStopRate), rb.linearVelocity.y);
        }

        transform.localScale = new Vector3(Mathf.Sign(direction) * Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
    }

    void PatrolWithSpeed(float currentMoveSpeed)
    {
        if (isPerformingHalfHealthAction || isPerformingSomeAction)
            return;

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
        if (canMove && swordAttackZone.detectedColliders.Any(collider => collider.CompareTag("Player")))
        {
            DealDamageToPlayer();
        }
    }

    public void PlayAttackSound()
    {
        if (swingSoundSource != null)
        {
            swingSoundSource.Play();
        }
    }

    private void DealDamageToPlayer()
{
    if (HasTarget && playerHealth != null && Vector2.Distance(transform.position, playerHealth.transform.position) <= chaseDistanceAggro)
    {
        bool damageApplied = playerHealth.TakeDamage(damage); // Capture return value

        if (damageApplied)
        {
            hitSoundSource?.Play(); // Play ElfSwordsman's hit sound
            SetAggro(true); // Aggro when player takes damage within chase distance aggro
            isChasing = true; // Start chasing
        }
        // No action needed if attack was evaded
    }
}


    public void OnEveryDamageTaken()
    {
        float distanceToPlayer = Vector2.Distance(transform.position, playerTransform.position);
        if (distanceToPlayer <= chaseDistanceAggro)
        {
            SetAggro(true);
        }
        StartCoroutine(HandleStagger());
    }

    private IEnumerator HandleStagger()
    {
        canMove = false;
        isStaggering = true;
        animator.SetBool("canMove", false);
        rb.linearVelocity = Vector2.zero;
        animator.SetTrigger("takeDamage");

        yield return new WaitForSeconds(staggerTimer);

        animator.SetBool("canMove", true);
        canMove = true;
        isStaggering = false;
    }

    public void OnSomeDamageTaken()
    {
        if (!isPerformingSomeAction)
        {
            StartCoroutine(PerformOnSomeDamageTakenAction());
        }
    }

    private IEnumerator PerformOnSomeDamageTakenAction()
    {
        while (isStaggering)
        {
            yield return null;
        }

        isPerformingSomeAction = true;
        canMove = false;
        isChasing = false;
        animator.SetBool("canMove", false);

        isCharging = true;
        animator.SetBool("Charging", true);

        chargeDirectionFacing = transform.position.x > playerTransform.position.x ? -1 : 1;

        transform.localScale = new Vector3(chargeDirectionFacing * Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);

        rb.linearVelocity = new Vector2(chargeDirectionFacing * chargeSpeed, rb.linearVelocity.y);

        yield return new WaitForSeconds(chargeDuration);

        rb.linearVelocity = Vector2.zero;

        isCharging = false;
        animator.SetBool("Charging", false);

        animator.SetTrigger("attack");

        canMove = true;
        animator.SetBool("canMove", true);
        isChasing = true;
        isPerformingSomeAction = false;
    }

    public bool _hasTarget = false;
    public bool HasTarget
    {
        get { return _hasTarget; }
        private set
        {
            _hasTarget = value && playerTransform != null;
            animator.SetBool("hasTarget", _hasTarget);
        }
    }

    public void HandleHeadCollision()
    {
        Debug.Log("Player collided on Elf Swordsman's head.");
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
        SetAggro(false);

        yield return new WaitForSeconds(duration);

        isBehaviorDisabled = false;
        playerOnHead = false;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Vector2 proximityCenter = new Vector2(transform.position.x + proximityDetectionOffsetX, transform.position.y + proximityDetectionOffsetY);
        Gizmos.DrawWireSphere(proximityCenter, proximityDetectionRadius);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, chaseDistanceAggro);
    }

    private void HandleHalfHealthAction()
    {
        if (enemyHealth.EnableHalfHealthAction)
        {
            if (enemyHealth.CurrentHealth <= enemyHealth.MaxHealth / 2)
            {
                if ((!hasPerformedHalfHealthAction || !enemyHealth.ActionOnceOnly) && actionCooldownTimer <= 0f)
                {
                    StartCoroutine(PerformHalfHealthAction());

                    hasPerformedHalfHealthAction = true;
                    actionCooldownTimer = enemyHealth.ActionCooldown;
                }
            }
        }

        if (actionCooldownTimer > 0f)
        {
            actionCooldownTimer -= Time.deltaTime;
        }
    }

    private IEnumerator PerformHalfHealthAction()
    {
        isPerformingHalfHealthAction = true;
        canMove = false;
        isChasing = false;
        animator.SetBool("canMove", false);

        isCharging = true;
        animator.SetBool("Charging", true);

        chargeDirectionFacing = transform.position.x > playerTransform.position.x ? -1 : 1;

        transform.localScale = new Vector3(chargeDirectionFacing * Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);

        rb.linearVelocity = new Vector2(chargeDirectionFacing * chargeSpeed, rb.linearVelocity.y);

        yield return new WaitForSeconds(chargeDuration);

        rb.linearVelocity = Vector2.zero;

        isCharging = false;
        animator.SetBool("Charging", false);

        animator.SetTrigger("attack");

        canMove = true;
        animator.SetBool("canMove", true);
        isChasing = true;
        isPerformingHalfHealthAction = false;
    }

    // Implementation of IChaseZoneUser
    public void SetPlayerInChaseZone(bool isInZone)
    {
        isPlayerInChaseZone = isInZone;
    }

    // Implementation of IEnemyAggro
    // Implementation of IEnemyAggro
// Implementation of IEnemyAggro
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
    if (detectionZone != null)
    {
        detectionZone.gameObject.layer = aggro ? LayerMask.NameToLayer("EnemyHitboxAggro") : LayerMask.NameToLayer("EnemyHitbox");
    }

    // Change the SwordAttack's layer based on the aggro state
    if (swordAttackZone != null)
    {
        swordAttackZone.gameObject.layer = aggro ? LayerMask.NameToLayer("EnemyHitboxAggro") : LayerMask.NameToLayer("EnemyHitbox");
    }
}



    public bool IsAggroed
    {
        get { return aggro; }
    }

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
