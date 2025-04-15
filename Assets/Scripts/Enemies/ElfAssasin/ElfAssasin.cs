using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ElfAssassin : MonoBehaviour, IEnemyActions, IChaseZoneUser, IEnemyAggro
{
    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private DetectionZone detectionZone;
    [SerializeField] private SwordAttack swordAttackZone;
    [SerializeField] private Transform[] patrolPoints;
    [SerializeField] private AudioSource swingSoundSource;
    [SerializeField] private AudioSource hitSoundSource;
    [SerializeField] private AudioSource shurikenThrowSoundSource; // Audio source for shuriken throw sound
    [SerializeField] private ChaseZone chaseZone; // Reference to the ChaseZone script
    private Transform playerTransform;
    private PlayerHealth playerHealth;
    private EnemyHealth enemyHealth;

    [Header("Settings")]
    [SerializeField] private float moveSpeed = 3f;         // Normal speed when aggroed
    [SerializeField] private float patrolSpeed = 1.5f;     // Slower speed when patrolling (for stealth)
    [SerializeField] private float walkStopRate = 0.6f;
    [SerializeField] private float damage = 2;
    [SerializeField] private float staggerTimer = 1f;
    [SerializeField] private float disableDuration = 2.5f;
    [SerializeField] private float disableMoveSpeed = 1f;  // Move speed during the disable behavior period

    [Header("Stealth Visuals")]
    [SerializeField] private float stealthAlpha = 0.7f; // 70% opacity when stealthy (30% invisible)
    [SerializeField] private float normalAlpha = 1.0f;  // 100% opacity when aggroed
    [SerializeField] private float alphaTransitionSpeed = 5.0f; // Speed of the transparency transition
    
    private SpriteRenderer[] spriteRenderers; // All sprite renderers on this enemy and children
    private float currentAlpha; // Current alpha value for smooth transition

    private int patrolDestination = 0;
    private bool isChasing = false;
    private bool canMove = true; // Indicates if the Elf Assassin can perform actions
    private bool isBehaviorDisabled = false;
    private bool playerOnHead = false; // Indicates if the player is on the Elf Assassin's head
    private bool isStaggering = false; // Flag to track stagger state
    public bool aggro = false; // Aggro state, made public for ChaseZone

    private bool hasPerformedHalfHealthAction = false;
    private float actionCooldownTimer = 0f;
    private bool isPerformingHalfHealthAction = false;
    private bool isPerformingSomeAction = false; // Added for OnSomeDamageTaken

    [SerializeField] private GameObject shurikenPrefab;
    [SerializeField] private Transform shurikenPoint;

    [Header("Move Away Settings")]
    [SerializeField] private float moveAwayDuration = 1f; // Serialized duration for moving away
    [SerializeField] private float moveAwaySpeed = 5f;    // Serialized move speed during move away

    private float moveAwayDirectionFacing = 0f; // Direction to move during move away action

    // Proximity detection radius
    [Header("Proximity Detection")]
    [SerializeField] private float proximityDetectionRadius = 1.5f; // Enemy detects player within this radius regardless of stealth
    [SerializeField] private float proximityDetectionOffsetX = 0f;   // Offset in the X direction
    [SerializeField] private float proximityDetectionOffsetY = 0f;   // Offset in the Y direction
    private bool isPlayerInProximity = false;

    // Chase distance for aggro state
    [Header("Aggro Chase Distance")]
    [SerializeField] private float chaseDistanceAggro = 10f;    // Extended chase distance when aggroed

    private bool isPlayerInChaseZone = false; // Flag to indicate if the player is in the chase zone

    // New variable to track previous aggro state
    private bool previousAggroState = false;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
        enemyHealth = GetComponent<EnemyHealth>();

        // Get all sprite renderers on this gameobject and its children
        spriteRenderers = GetComponentsInChildren<SpriteRenderer>();
        
        // Set initial alpha to stealth value
        currentAlpha = stealthAlpha;
        UpdateRendererAlpha(currentAlpha);

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
            playerHealth = player.GetComponent<PlayerHealth>();
        }

        // Get reference to ChaseZone
        chaseZone = GetComponentInChildren<ChaseZone>();
        if (chaseZone != null)
        {
            chaseZone.SetEnemy(this); // Pass this instance to the ChaseZone
        }
        else
        {
            Debug.LogError("ChaseZone not found. Please ensure the ChaseZone script is attached to a child object.");
        }

        aggro = false; // Set aggro to false at the start

        // **Set Layer to Enemy by Default**
        gameObject.layer = LayerMask.NameToLayer("Enemy");
    }

    void Update()
    {
        if (isBehaviorDisabled || !canMove || isStaggering || isPerformingHalfHealthAction || isPerformingSomeAction)
            return; // Skip behavior if disabled, staggering, or performing action

        HasTarget = detectionZone.detectedColliders.Count > 0;

        // Calculate the center of the proximity detection with offsets
        Vector2 proximityCenter = new Vector2(transform.position.x + proximityDetectionOffsetX, transform.position.y + proximityDetectionOffsetY);

        // Update isPlayerInProximity
        isPlayerInProximity = Vector2.Distance(proximityCenter, playerTransform.position) <= proximityDetectionRadius;

        // Get reference to the Player script
        Player player = playerTransform.GetComponent<Player>();

        // Determine if the player is detectable
        bool isPlayerDetectable = true;
        if (player != null)
        {
            if (player.isCrouchingInBushes && !isPlayerInProximity)
            {
                isPlayerDetectable = false; // Player is hidden
            }
        }

        // **Update Aggro State**
        if (!aggro)
        {
            if (isPlayerInProximity)
            {
                SetAggro(true); // Always aggro if player is in proximity
            }
            else if (isPlayerDetectable && isPlayerInChaseZone)
            {
                SetAggro(true);
            }
            else
            {
                SetAggro(false); // Remain not aggroed
            }
        }

        // Determine if should chase based on aggro
        if (aggro)
        {
            float distanceToPlayer = Vector2.Distance(transform.position, playerTransform.position);

            // Check if player is within chase distance
            if (distanceToPlayer <= chaseDistanceAggro)
            {
                isChasing = true;
            }
            else
            {
                // Player is too far, lose aggro
                SetAggro(false);
                isChasing = false;
            }
        }
        else
        {
            isChasing = false;
        }

        // **Update Layer Based on Aggro**
        UpdateLayerBasedOnAggro();

        // Smoothly transition alpha based on aggro state
        float targetAlpha = aggro ? normalAlpha : stealthAlpha;
        if (currentAlpha != targetAlpha)
        {
            currentAlpha = Mathf.MoveTowards(currentAlpha, targetAlpha, alphaTransitionSpeed * Time.deltaTime);
            UpdateRendererAlpha(currentAlpha);
        }

        // Handle half-health action
        HandleHalfHealthAction();

        if (HasTarget && canMove)
        {
            animator.SetTrigger("attack");
        }
    }

    // Helper method to update all sprite renderers' alpha values
    private void UpdateRendererAlpha(float alpha)
    {
        foreach (SpriteRenderer renderer in spriteRenderers)
        {
            Color color = renderer.color;
            color.a = alpha;
            renderer.color = color;
        }
    }

    private void FixedUpdate()
    {
        if (isBehaviorDisabled)
        {
            PatrolWithSpeed(disableMoveSpeed); // Move at the disableMoveSpeed during the disable period
            return;
        }

        if (isPerformingHalfHealthAction || isPerformingSomeAction) // Prevent movement if performing any special action
        {
            rb.linearVelocity = new Vector2(moveAwayDirectionFacing * moveAwaySpeed, rb.linearVelocity.y);
            return;
        }

        if (playerOnHead)
        {
            PatrolWithSpeed(patrolSpeed); // Force patrol behavior if the player is on the head
            return;
        }

        if (isChasing)
        {
            ChasePlayer(moveSpeed);
        }
        else
        {
            PatrolWithSpeed(patrolSpeed); // Use patrol speed when not chasing
        }
    }

    void ChasePlayer(float currentMoveSpeed)
    {
        if (isBehaviorDisabled || isStaggering || isPerformingHalfHealthAction || isPerformingSomeAction)
            return; // Prevent movement if staggering or performing action

        float direction = transform.position.x > playerTransform.position.x ? -1 : 1;

        if (canMove)
        {
            rb.linearVelocity = new Vector2(direction * currentMoveSpeed, rb.linearVelocity.y);
        }
        else
        {
            rb.linearVelocity = new Vector2(Mathf.Lerp(rb.linearVelocity.x, 0, walkStopRate), rb.linearVelocity.y);
        }

        // Face the player while chasing
        transform.localScale = new Vector3(Mathf.Sign(direction) * Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
    }

    void Patrol()
    {
        if (isPerformingHalfHealthAction || isPerformingSomeAction)
            return; // Prevent patrol during special actions
        PatrolWithSpeed(patrolSpeed); // Use patrol speed
    }

    void PatrolWithSpeed(float currentMoveSpeed)
    {
        if (isPerformingHalfHealthAction || isPerformingSomeAction)
            return; // Prevent patrol during special actions

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
        if (HasTarget && playerHealth != null)
        {
            bool damageApplied = playerHealth.TakeDamage(damage); // Capture return value

            if (damageApplied)
            {
                // Play ElfAssassin's hit sound
                if (hitSoundSource != null)
                {
                    hitSoundSource.Play();
                }
            }
            // No action needed if attack was evaded
        }
    }

    // OnEveryDamageTaken method to handle actions that occur every time the enemy takes damage
    public void OnEveryDamageTaken()
    {
        float distanceToPlayer = Vector2.Distance(transform.position, playerTransform.position);
        if (distanceToPlayer <= chaseDistanceAggro)
        {
            SetAggro(true);  // Set aggro to true when taking damage within aggro range
        }
        StartCoroutine(HandleStagger());
    }

    private IEnumerator HandleStagger()
    {
        canMove = false; // Disable movement and actions
        isStaggering = true; // Set stagger flag
        animator.SetBool("canMove", false);
        rb.linearVelocity = Vector2.zero;
        animator.SetTrigger("takeDamage");

        yield return new WaitForSeconds(staggerTimer);

        animator.SetBool("canMove", true);
        canMove = true; // Re-enable movement and actions
        isStaggering = false; // Clear stagger flag
    }

    // OnSomeDamageTaken method to handle actions that occur based on a percentage chance
    public void OnSomeDamageTaken()
    {
        if (!isPerformingSomeAction)
        {
            StartCoroutine(PerformOnSomeDamageTakenAction());
        }
    }

    private IEnumerator PerformOnSomeDamageTakenAction()
    {
        // Wait until the stagger state is cleared
        while (isStaggering)
        {
            yield return null; // Wait for the next frame and recheck
        }

        isPerformingSomeAction = true;
        canMove = false;
        isChasing = false;
        animator.SetBool("canMove", false);

        // Calculate move away direction (opposite of player position)
        moveAwayDirectionFacing = transform.position.x > playerTransform.position.x ? 1 : -1;

        // Face the movement direction
        transform.localScale = new Vector3(moveAwayDirectionFacing * Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);

        // Wait for the move away duration
        yield return new WaitForSeconds(moveAwayDuration);

        // After moving away, stop movement
        rb.linearVelocity = Vector2.zero;

        // Throw shuriken after moving away
        animator.SetTrigger("throwShuriken");
        yield return new WaitForSeconds(0.5f); // Adjust based on animation timing

        // Resume normal behavior
        canMove = true;
        animator.SetBool("canMove", true);
        isChasing = true;
        isPerformingSomeAction = false; // Reset the action flag
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

    // Method called when the head collision is detected
    public void HandleHeadCollision()
    {
        Debug.Log("Player collided on Elf Assassin's head.");
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
        SetAggro(false);  // Reset aggro when behavior is disabled using SetAggro

        yield return new WaitForSeconds(duration);

        isBehaviorDisabled = false;
        playerOnHead = false; // Reset player on head flag
    }

    void OnDrawGizmos()
    {
        // Draw proximity detection radius with offset
        Gizmos.color = Color.green;
        Vector2 proximityCenter = new Vector2(transform.position.x + proximityDetectionOffsetX, transform.position.y + proximityDetectionOffsetY);
        Gizmos.DrawWireSphere(proximityCenter, proximityDetectionRadius);

        // Draw aggro chase distance
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

        // Calculate move away direction (opposite of player position)
        moveAwayDirectionFacing = transform.position.x > playerTransform.position.x ? 1 : -1;

        // Face the movement direction
        transform.localScale = new Vector3(moveAwayDirectionFacing * Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);

        // Wait for the move away duration
        yield return new WaitForSeconds(moveAwayDuration);

        // After moving away, stop movement
        rb.linearVelocity = Vector2.zero;

        // Throw shuriken after moving away
        animator.SetTrigger("throwShuriken");
        yield return new WaitForSeconds(0.5f); // Adjust based on animation timing

        // Resume normal behavior
        canMove = true;
        animator.SetBool("canMove", true);
        isChasing = true;
        isPerformingHalfHealthAction = false;
    }

    public void ThrowShuriken()
    {
        if (shurikenPrefab != null && playerTransform != null)
        {
            GameObject shuriken = Instantiate(shurikenPrefab, shurikenPoint.position, Quaternion.identity);

            Vector2 direction = (playerTransform.position - shurikenPoint.position).normalized;

            Shuriken shurikenScript = shuriken.GetComponent<Shuriken>();
            if (shurikenScript != null)
            {
                shurikenScript.SetDirection(direction);
            }

            // Rotate the shuriken to face the player
            shuriken.transform.right = direction;

            // Play the shuriken throw sound
            if (shurikenThrowSoundSource != null)
            {
                shurikenThrowSoundSource.Play();
            }
        }
    }

    // Implementation of IChaseZoneUser
    public void SetPlayerInChaseZone(bool isInZone)
    {
        isPlayerInChaseZone = isInZone;
    }

    // Implementation of IEnemyAggro
    public bool IsAggroed
    {
        get { return aggro; }
    }

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

    /// <summary>
    /// Updates the layer of the Elf Assassin based on the current aggro state.
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