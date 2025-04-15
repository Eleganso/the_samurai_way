using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ElfMage : MonoBehaviour, IEnemyActions, IEnemyAggro
{
    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private DetectionZone detectionZone;
    [SerializeField] private Transform[] patrolPoints;
    [SerializeField] private Transform playerTransform;
    [SerializeField] private AudioSource attackSoundSource;
    [SerializeField] private AudioSource spell1SoundSource;
    [SerializeField] private AudioSource spell2SoundSource;

    [Header("Attack and Spells")]
    [SerializeField] private GameObject autoAttackPrefab;
    [SerializeField] private Transform autoAttackPoint;
    [SerializeField] private float autoAttackCooldown = 3f;

    [SerializeField] private GameObject spell1Prefab;
    [SerializeField] private Transform spell1Point;
    [SerializeField] private float spell1Cooldown = 10f;

    [SerializeField] private GameObject spell2Prefab;
    [SerializeField] private Transform spell2Point;
    [SerializeField] private float spell2Duration = 5f;
    [SerializeField] private float spell2Cooldown = 20f;
    [SerializeField] private float spell2Damage = 1f;  // Changed to float

    [Header("Settings")]
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float moveAwayDistance = 5f; 
    [SerializeField] private float chaseDistance = 10f;  
    [SerializeField] private float chaseDistanceAggro = 15f;  // Aggro chase distance
    [SerializeField] private float turnDistance = 5f;
    [SerializeField] private float damage = 2f; // Changed to float
    [SerializeField] private float staggerTimer = 1f;
    [SerializeField] private float disableDuration = 2.5f;
    [SerializeField] private float disableMoveSpeed = 1f;

    private int patrolDestination = 0;
    private bool isChasing = false;
    private bool canMove = true;
    private bool isBehaviorDisabled = false;
    private bool isStaggering = false;
    private bool isChannelingSpell2 = false;
    private bool isChanneling = false;
    private bool aggro = false;  // Aggro flag

    private bool hasPerformedHalfHealthAction = false;
    private float actionCooldownTimer = 0f;
    private bool isPerformingHalfHealthAction = false;
    private bool isPerformingSomeAction = false;

    private float autoAttackTimer = 0f;
    private float spell1Timer = 0f;
    private float spell2Timer = 0f;

    private EnemyHealth enemyHealth;

    private Coroutine spell2Coroutine;

    // Field to track if the player is detected
    private bool hasTarget = false;

    // Flag to prevent multiple takeDamage triggers
    private bool isDamageHandled = false;

    // New variable to track previous aggro state
    private bool previousAggroState = false;

    public bool IsAggroed
    {
        get { return aggro; }
    }

    public void SetAggro(bool isAggro)
    {
        aggro = isAggro;
        UpdateLayerBasedOnAggro(); // Ensure layer is updated immediately
    }

    private void Awake()
    {
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
        enemyHealth = GetComponent<EnemyHealth>();

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
        }

        // Initialize hasTarget to false
        hasTarget = false;

        // Initialize layer to Enemy by default
        previousAggroState = false;
        gameObject.layer = LayerMask.NameToLayer("Enemy");
    }

    void Update()
    {
        if (isBehaviorDisabled || isChanneling || !canMove || isStaggering || isPerformingHalfHealthAction || isPerformingSomeAction) return;

        // Update hasTarget based on DetectionZone
        bool playerInZone = detectionZone.detectedColliders.Any(c => c.CompareTag("Player"));
        if (playerInZone != hasTarget)
        {
            hasTarget = playerInZone;
            animator.SetBool("hasTarget", hasTarget);
            Debug.Log($"ElfMage hasTarget set to {hasTarget}");
        }

        float distanceToPlayer = Vector2.Distance(transform.position, playerTransform.position);

        // If the player enters the chase distance or the Mage is damaged, switch to aggro mode
        if (distanceToPlayer <= chaseDistance || aggro)
        {
            SetAggro(true);  // Set aggro to true once conditions are met
        }

        // Use aggro chase distance if aggro is true
        if (aggro)
        {
            if (distanceToPlayer <= chaseDistanceAggro)
            {
                isChasing = true;
            }
            else
            {
                isChasing = false;
            }
        }
        else
        {
            if (distanceToPlayer <= chaseDistance)
            {
                isChasing = true;
            }
            else
            {
                isChasing = false;
            }
        }

        // **Update Layer Based on Aggro**
        UpdateLayerBasedOnAggro();

        // Handle attacks and spells if the player is within detection range
        if (hasTarget)
        {
            HandleAutoAttack();
            HandleSpell1();
            HandleSpell2();

            if (canMove)
            {
                animator.SetTrigger("attack");
            }
        }

        HandleHalfHealthAction();
    }

    private void FixedUpdate()
    {
        if (isBehaviorDisabled || isChanneling)
        {
            PatrolWithSpeed(disableMoveSpeed);
            return;
        }

        if (isPerformingHalfHealthAction || isPerformingSomeAction)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        if (isChasing)
        {
            MoveRelativeToPlayer(moveSpeed);
        }
        else
        {
            Patrol();
        }
    }

    void MoveRelativeToPlayer(float currentMoveSpeed)
    {
        if (isBehaviorDisabled || isStaggering || isChanneling || isPerformingHalfHealthAction || isPerformingSomeAction) return;

        float distanceToPlayer = Vector2.Distance(transform.position, playerTransform.position);

        // Flip to face the player if within turnDistance
        if (distanceToPlayer <= turnDistance)
        {
            float faceDirection = (transform.position.x > playerTransform.position.x) ? -1f : 1f;
            transform.localScale = new Vector3(Mathf.Abs(transform.localScale.x) * faceDirection, transform.localScale.y, transform.localScale.z);
        }

        // Move away from the player if within moveAwayDistance
        if (distanceToPlayer <= moveAwayDistance)
        {
            MoveAwayFromPlayer(currentMoveSpeed);
        }
        else if (aggro && distanceToPlayer <= chaseDistanceAggro)
        {
            MoveTowardPlayer(currentMoveSpeed);
        }
        else if (distanceToPlayer <= chaseDistance)
        {
            MoveTowardPlayer(currentMoveSpeed);
        }
        else
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        }
    }

    void MoveTowardPlayer(float currentMoveSpeed)
    {
        float direction = (transform.position.x < playerTransform.position.x) ? 1f : -1f;
        if (canMove)
        {
            rb.linearVelocity = new Vector2(direction * currentMoveSpeed, rb.linearVelocity.y);
        }
        else
        {
            rb.linearVelocity = new Vector2(Mathf.Lerp(rb.linearVelocity.x, 0f, 0.6f), rb.linearVelocity.y);
        }
    }

    void MoveAwayFromPlayer(float currentMoveSpeed)
    {
        float direction = (transform.position.x > playerTransform.position.x) ? 1f : -1f;
        if (canMove)
        {
            rb.linearVelocity = new Vector2(direction * currentMoveSpeed, rb.linearVelocity.y);
        }
        else
        {
            rb.linearVelocity = new Vector2(Mathf.Lerp(rb.linearVelocity.x, 0f, 0.6f), rb.linearVelocity.y);
        }
    }

    void Patrol()
    {
        PatrolWithSpeed(moveSpeed);
    }

    void PatrolWithSpeed(float currentMoveSpeed)
{
    // Check if there are any patrol points assigned
    if (patrolPoints == null || patrolPoints.Length == 0)
    {
        // No patrol points, just stay in place or implement alternative behavior
        rb.linearVelocity = Vector2.zero;
        return;
    }

    // Existing patrol logic
    Vector2 targetPos = patrolPoints[patrolDestination].position;
    targetPos.y = rb.position.y;
    Vector2 newPos = Vector2.MoveTowards(rb.position, targetPos, currentMoveSpeed * Time.fixedDeltaTime);
    rb.MovePosition(newPos);
    transform.localScale = new Vector3(patrolDestination == 0 ? -1f : 1f, 1f, 1f);

    if (Vector2.Distance(rb.position, targetPos) < 0.2f)
    {
        patrolDestination = (patrolDestination + 1) % patrolPoints.Length;
    }
}

    private void HandleAutoAttack()
    {
        autoAttackTimer += Time.deltaTime;
        if (autoAttackTimer >= autoAttackCooldown)
        {
            autoAttackTimer = 0f;
            PerformAutoAttack();
        }
    }

    private void PerformAutoAttack()
    {
        animator.SetTrigger("autoAttack");
    }

    public void ShootAutoAttack()
    {
        if (autoAttackPrefab != null && autoAttackPoint != null)
        {
            AutoAttack autoAttack = Instantiate(autoAttackPrefab, autoAttackPoint.position, Quaternion.identity).GetComponent<AutoAttack>();

            if (autoAttack != null)
            {
                Vector2 direction = (playerTransform.position - autoAttackPoint.position).normalized;

                if (direction.x > 0f)
                {
                    autoAttack.transform.localScale = new Vector3(1f, autoAttack.transform.localScale.y, autoAttack.transform.localScale.z);
                }
                else if (direction.x < 0f)
                {
                    autoAttack.transform.localScale = new Vector3(-1f, autoAttack.transform.localScale.y, autoAttack.transform.localScale.z);
                }

                autoAttack.Initialize(direction, damage); // damage is float
            }

            attackSoundSource?.Play();
        }
    }

    private void HandleSpell1()
    {
        spell1Timer += Time.deltaTime;
        if (spell1Timer >= spell1Cooldown)
        {
            spell1Timer = 0f;
            PerformSpell1();
        }
    }

    private void PerformSpell1()
    {
        animator.SetTrigger("spell1");
    }

    public void CastSpell1()
    {
        if (spell1Prefab != null && spell1Point != null)
        {
            Spell1 spell1 = Instantiate(spell1Prefab, spell1Point.position, Quaternion.identity).GetComponent<Spell1>();

            if (spell1 != null)
            {
                Vector2 direction = (playerTransform.position - spell1Point.position).normalized;

                if (direction.x > 0f)
                {
                    spell1.transform.localScale = new Vector3(1f, spell1.transform.localScale.y, spell1.transform.localScale.z);
                }
                else if (direction.x < 0f)
                {
                    spell1.transform.localScale = new Vector3(-1f, spell1.transform.localScale.y, spell1.transform.localScale.z);
                }

                spell1.Initialize(direction, damage * 2f); // damage * 2f is float
            }

            spell1SoundSource?.Play();
        }
    }

    private void HandleSpell2()
    {
        spell2Timer += Time.deltaTime;
        if (spell2Timer >= spell2Cooldown)
        {
            spell2Timer = 0f;
            PerformSpell2();
        }
    }

    private void PerformSpell2()
    {
        animator.SetTrigger("spell2");
    }

    public void ActivateSpell2()
    {
        channelOn();
        if (spell2Prefab != null)
        {
            Spell2 spell2 = Instantiate(spell2Prefab, transform.position, Quaternion.identity).GetComponent<Spell2>();
            if (spell2 != null)
            {
                isChannelingSpell2 = true;
                spell2.Initialize(this, spell2Duration); // Ensure Spell2.Initialize accepts float
                StartCoroutine(DealDamageDuringSpell(spell2Duration));
            }

            spell2SoundSource?.Play();
        }
    }

    private IEnumerator DealDamageDuringSpell(float duration)
    {
        float interval = 1.0f;
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            yield return new WaitForSeconds(interval);
            ApplySpell2Damage();
            elapsedTime += interval;
        }

        channelOff();
        isChannelingSpell2 = false;
    }

    public void ApplySpell2Damage()
    {
        SwordAttack swordAttack = spell2Point.GetComponent<SwordAttack>();

        if (swordAttack != null)
        {
            foreach (Collider2D detected in swordAttack.detectedColliders)
            {
                if (detected.CompareTag("Player"))
                {
                    PlayerHealth playerHealth = detected.GetComponent<PlayerHealth>();
                    if (playerHealth != null)
                    {
                        playerHealth.TakeDamage(spell2Damage); // Apply damage to the player
                        Debug.Log("Player hit by Spell2");
                    }
                }
            }
        }
    }

    public void channelOn()
    {
        gameObject.layer = LayerMask.NameToLayer("Default");
        isChanneling = true;
    }

    public void channelOff()
    {
        gameObject.layer = LayerMask.NameToLayer("Attackable");
        isChanneling = false;
    }

    public void OnEveryDamageTaken()
    {
        if (!isDamageHandled && !isChannelingSpell2)
        {
            isDamageHandled = true;
            SetAggro(true);  // Set aggro to true when taking damage
            StartCoroutine(HandleStagger());
        }
    }

    public void OnSomeDamageTaken()
    {
        if (!isPerformingSomeAction)
        {
            StartCoroutine(PerformOnSomeDamageTakenAction());
        }
    }

    private IEnumerator HandleStagger()
    {
        canMove = false;
        isStaggering = true;
        animator.SetBool("canMove", false);
        animator.SetTrigger("takeDamage");

        rb.linearVelocity = Vector2.zero;

        yield return new WaitForSeconds(staggerTimer);

        animator.SetBool("canMove", true);
        canMove = true;
        isStaggering = false;

        isDamageHandled = false;
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

        float direction = transform.position.x > playerTransform.position.x ? 1f : -1f;
        rb.linearVelocity = new Vector2(direction * moveSpeed * 2f, rb.linearVelocity.y);

        yield return new WaitForSeconds(0.5f);

        rb.linearVelocity = Vector2.zero;

        canMove = true;
        animator.SetBool("canMove", true);
        isChasing = true;
        isPerformingSomeAction = false;
    }

    private void HandleHalfHealthAction()
    {
        if (enemyHealth.EnableHalfHealthAction)
        {
            if (enemyHealth.CurrentHealth <= enemyHealth.MaxHealth / 2f)
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

        CastSpell1();

        yield return new WaitForSeconds(1f);

        canMove = true;
        animator.SetBool("canMove", true);
        isChasing = true;
        isPerformingHalfHealthAction = false;
    }

    public void HandleHeadCollision()
    {
        Debug.Log("Player collided on Elf Mage's head.");
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
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, moveAwayDistance);
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, chaseDistance);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, chaseDistanceAggro);
    }

    /// <summary>
    /// Updates the layer of the Elf Mage based on the current aggro state.
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
    /// <summary>
/// Public method to play the Spell2 sound. Can be called via animation event.
/// </summary>
public void PlaySpell2Sound()
{
    if (spell2SoundSource != null)
    {
        spell2SoundSource.Play();
    }
    else
    {
        Debug.LogWarning("spell2SoundSource is not assigned!");
    }
}

}
