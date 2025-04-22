using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Enemies.Navigation;

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
    private EnemyNavigationController navigationController;

    [Header("Settings")]
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float patrolSpeed = 1.5f;
    [SerializeField] private float walkStopRate = 0.6f;
    [SerializeField] private float damage = 2;
    [SerializeField] private float staggerTimer = 1f;
    [SerializeField] private float disableDuration = 2.5f;
    [SerializeField] private float disableMoveSpeed = 1f;

    [Header("Navigation Settings")]
    [SerializeField] private float jumpForce = 8f;
    [SerializeField] private float climbSpeed = 2f;
    [SerializeField] private float maxJumpDistance = 3f;
    [SerializeField] private bool useNavigationSystem = true;

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
        
        // Get or add reference to navigation controller
        navigationController = GetComponent<EnemyNavigationController>();
        if (navigationController == null && useNavigationSystem)
        {
            // Add the navigation controller
            navigationController = gameObject.AddComponent<EnemyNavigationController>();
            Debug.Log("EnemyNavigationController added to " + gameObject.name);
            
            // Create check points if they don't exist
            Transform groundCheck = transform.Find("GroundCheck");
            if (groundCheck == null)
            {
                groundCheck = new GameObject("GroundCheck").transform;
                groundCheck.SetParent(transform);
                groundCheck.localPosition = new Vector3(0, -0.5f, 0); // Position at feet
            }
            
            Transform wallCheck = transform.Find("WallCheck");
            if (wallCheck == null)
            {
                wallCheck = new GameObject("WallCheck").transform;
                wallCheck.SetParent(transform);
                wallCheck.localPosition = new Vector3(0.5f, 0, 0); // Position at mid-body
            }
            
            Transform ladderCheck = transform.Find("LadderCheck");
            if (ladderCheck == null)
            {
                ladderCheck = new GameObject("LadderCheck").transform;
                ladderCheck.SetParent(transform);
                ladderCheck.localPosition = new Vector3(0, 0.5f, 0); // Position at hands
            }
            
            // Setup navigation component with reference to player transform
            navigationController.SetupReferences(
                animator,
                groundCheck,
                wallCheck,
                ladderCheck,
                LayerMask.GetMask("Ground"),
                LayerMask.GetMask("Obstacle"),
                LayerMask.GetMask("Ladder")
            );
            
            // Set movement parameters
            navigationController.SetMovementParameters(moveSpeed, climbSpeed, jumpForce, maxJumpDistance);
            
            // Set player as target
            if (playerTransform != null)
            {
                navigationController.SetTarget(playerTransform);
            }
        }

        aggro = false;
        previousAggroState = false;
        gameObject.layer = LayerMask.NameToLayer("Enemy");
    }
    
    void Update()
    {
        if (isBehaviorDisabled || !canMove || isStaggering || isPerformingHalfHealthAction || isPerformingSomeAction || isCharging)
        {
            if (navigationController != null && useNavigationSystem)
            {
                navigationController.EnableNavigation(false);
            }
            return;
        }

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
                
                // Enable navigation system when chasing and aggro'd
                if (navigationController != null && useNavigationSystem)
                {
                    navigationController.EnableNavigation(true);
                }
            }
            else
            {
                SetAggro(false);
                isChasing = false;
                
                // Disable navigation when not chasing
                if (navigationController != null && useNavigationSystem)
                {
                    navigationController.EnableNavigation(false);
                }
            }
        }
        else
        {
            isChasing = false;
            
            // Disable navigation when not aggro'd
            if (navigationController != null && useNavigationSystem)
            {
                navigationController.EnableNavigation(false);
            }
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
        // Skip FixedUpdate movement logic if navigation system is handling it
        if (navigationController != null && useNavigationSystem && aggro && !isBehaviorDisabled && 
            !isPerformingHalfHealthAction && !isPerformingSomeAction && !playerOnHead)
        {
            return;
        }
        
        // Original movement logic for when not using navigation system
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
        
        // Pause navigation during stagger
        if (navigationController != null && useNavigationSystem)
        {
            navigationController.PauseNavigation(staggerTimer);
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
        
        // Disable navigation during special action
        if (navigationController != null && useNavigationSystem)
        {
            navigationController.EnableNavigation(false);
        }

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
        
        // Re-enable navigation after special action if applicable
        if (navigationController != null && useNavigationSystem && aggro)
        {
            navigationController.EnableNavigation(true);
        }
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
        
        // Disable navigation when behavior is disabled
        if (navigationController != null && useNavigationSystem)
        {
            navigationController.EnableNavigation(false);
        }

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
        
        // Disable navigation during half health action
        if (navigationController != null && useNavigationSystem)
        {
            navigationController.EnableNavigation(false);
        }

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
        
        // Re-enable navigation after half health action if applicable
        if (navigationController != null && useNavigationSystem && aggro)
        {
            navigationController.EnableNavigation(true);
        }
    }

    // Implementation of IChaseZoneUser
    public void SetPlayerInChaseZone(bool isInZone)
    {
        isPlayerInChaseZone = isInZone;
    }

    // Implementation of IEnemyAggro
    public void SetAggro(bool isAggro)
    {
        aggro = isAggro;
        UpdateLayerBasedOnAggro(); // Ensure layer is updated immediately
        
        // Update navigation system based on aggro state
        if (navigationController != null && useNavigationSystem)
        {
            navigationController.EnableNavigation(isAggro);
        }

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