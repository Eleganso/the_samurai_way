using UnityEngine;

public class FireGeneral : MonoBehaviour, IEnemyAggro
{
    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private Rigidbody2D rb;
    private Transform playerTransform;
    private PlayerHealth playerHealth;

    [Header("Movement Bounds")]
    [SerializeField] private float minDistance = 2f;
    [SerializeField] private float maxDistance = 5f;
    [SerializeField] private float facingRange = 10f;
    [SerializeField] private float moveSpeed = 2f;

    [Header("Jump Settings")]
    [SerializeField] private float jumpHeight = 20f;
    [SerializeField] private float jumpCooldown = 5f;
    private float lastJumpTime;

    [Header("Attack Detection Zones")]
    [SerializeField] private GameObject Detection;
    [SerializeField] private GameObject Attack1Zone;
    [SerializeField] private GameObject Attack2Zone;
    [SerializeField] private GameObject Attack3Zone;
    [SerializeField] private GameObject AttackSpecialZone;

    [Header("Sounds")]
    [SerializeField] private AudioSource swingSound;
    [SerializeField] private AudioSource hitSound;
    [SerializeField] private AudioSource evadeSound; // For evasion sound

    private bool isPlayerInDetectionZone = false;
    private bool isPlayerInAttack1Zone = false;
    private bool isPlayerInAttack2Zone = false;
    private bool isPlayerInAttack3Zone = false;
    private bool isPlayerInAttackSpecialZone = false;

    [Header("Attack Timing")]
    [SerializeField] private float minTime = 1f;
    [SerializeField] private float maxTime = 3f;
    private float nextActionTime = 0f;

    private float originalMoveSpeed;
    private bool canTurn = true;

    // Aggro related fields
    private bool aggro = false;
    private bool previousAggroState = false;

    [Header("Proximity Detection")]
    [SerializeField] private float proximityDetectionRadius = 2f;
    private bool isPlayerInProximity = false;

    [Header("Aggro Chase Distance")]
    [SerializeField] private float chaseDistanceAggro = 10f;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
        originalMoveSpeed = moveSpeed;

        // Set initial layer to "Enemy"
        gameObject.layer = LayerMask.NameToLayer("Enemy");

        // Get reference to player
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
            playerHealth = player.GetComponent<PlayerHealth>();
            if (playerHealth == null)
            {
                Debug.LogError("PlayerHealth component not found on the Player.");
            }
        }
        else
        {
            Debug.LogError("Player GameObject with tag 'Player' not found in the scene.");
        }
    }

    public void DisableMovement()
    {
        // Reduce moveSpeed drastically
        moveSpeed = originalMoveSpeed * 0.0001f;
        canTurn = false;
    }

    public void EnableMovement()
    {
        canTurn = true;
        moveSpeed = originalMoveSpeed;
    }

    private void Update()
    {
        // Automatically attempt to jump based on cooldown
        if (Time.time - lastJumpTime >= jumpCooldown)
        {
            lastJumpTime = Time.time;
        }

        if (playerTransform == null)
        {
            Debug.LogWarning("playerTransform not assigned.");
            return;
        }

        float distanceToPlayer = Vector2.Distance(transform.position, playerTransform.position);

        // If not attacking, restore move speed if altered
        if (!animator.GetBool("attackPlaying") && moveSpeed < originalMoveSpeed && moveSpeed > 0)
        {
            moveSpeed = originalMoveSpeed;
        }

        // If not attacking, handle movement and attacks
        if (!animator.GetBool("attackPlaying"))
        {
            MoveAndAttack();
        }

        UpdateAnimation();

        if (distanceToPlayer <= facingRange)
        {
            FacePlayer();
        }

        // Update aggro state based on detection zone or proximity
        UpdateAggroState();
    }

    private void UpdateAggroState()
    {
        Vector2 proximityCenter = transform.position;
        isPlayerInProximity = Vector2.Distance(proximityCenter, playerTransform.position) <= proximityDetectionRadius;

        if (isPlayerInDetectionZone || isPlayerInProximity)
        {
            SetAggro(true);
        }
        else
        {
            SetAggro(false);
        }
    }

    public bool IsAggroed => aggro;

    public void SetAggro(bool isAggro)
    {
        aggro = isAggro;
        UpdateLayerBasedOnAggro();
        ChangeChildLayersBasedOnAggro();
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

    private void ChangeChildLayersBasedOnAggro()
    {
        int newLayer = aggro ? LayerMask.NameToLayer("EnemyHitboxAggro") : LayerMask.NameToLayer("EnemyHitbox");

        if (Detection != null) Detection.layer = newLayer;
        if (Attack1Zone != null) Attack1Zone.layer = newLayer;
        if (Attack2Zone != null) Attack2Zone.layer = newLayer;
        if (Attack3Zone != null) Attack3Zone.layer = newLayer;
        if (AttackSpecialZone != null) AttackSpecialZone.layer = newLayer;
    }

    private void UpdateAnimation()
    {
        animator.SetFloat("Speed", Mathf.Abs(rb.linearVelocity.x));
    }

    private void MoveAndAttack()
    {
        if (playerTransform == null) return;

        float distanceToPlayer = Vector2.Distance(transform.position, playerTransform.position);

        // Attempt attacks if player is in detection zone and cooldown elapsed
        if (isPlayerInDetectionZone && Time.time >= nextActionTime)
        {
            if (isPlayerInAttack1Zone || isPlayerInAttack2Zone || isPlayerInAttack3Zone || isPlayerInAttackSpecialZone)
            {
                PerformRandomAttack();
                nextActionTime = Time.time + Random.Range(minTime, maxTime);
            }
        }

        // Move towards player if within facingRange
        if (distanceToPlayer <= facingRange)
        {
            if (distanceToPlayer > minDistance && distanceToPlayer < maxDistance)
            {
                MoveTowardsPlayer();
            }
            FacePlayer();
        }
    }

    private void MoveTowardsPlayer()
    {
        Vector2 direction = (playerTransform.position - transform.position).normalized;
        rb.linearVelocity = new Vector2(direction.x * moveSpeed, rb.linearVelocity.y);
    }

    private void FacePlayer()
    {
        if (!canTurn) return;

        transform.localScale = new Vector3(playerTransform.position.x > transform.position.x ? 1 : -1, 1, 1);
    }

    private void PerformRandomAttack()
    {
        int randomAttack = Random.Range(0, 4);
        switch (randomAttack)
        {
            case 0: Attack1(); break;
            case 1: Attack2(); break;
            case 2: Attack3(); break;
            case 3: AttackSpecial(); break;
        }
    }

    public void Attack1()
    {
        if (!animator.GetBool("attackPlaying"))
        {
            animator.SetBool("attackPlaying", true);
            animator.SetTrigger("FireGeneralAttack1");
            nextActionTime = Time.time + Random.Range(minTime, maxTime);
        }
    }

    public void Attack2()
    {
        if (!animator.GetBool("attackPlaying"))
        {
            animator.SetBool("attackPlaying", true);
            animator.SetTrigger("FireGeneralAttack2");
            nextActionTime = Time.time + Random.Range(minTime, maxTime);
        }
    }

    public void Attack3()
    {
        if (!animator.GetBool("attackPlaying"))
        {
            animator.SetBool("attackPlaying", true);
            animator.SetTrigger("FireGeneralAttack3");
            nextActionTime = Time.time + Random.Range(minTime, maxTime);
        }
    }

    public void AttackSpecial()
    {
        if (!animator.GetBool("attackPlaying"))
        {
            animator.SetBool("attackPlaying", true);
            animator.SetTrigger("FireGeneralAttackSpecial");
            nextActionTime = Time.time + Random.Range(minTime, maxTime);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            if (Detection != null && Detection.GetComponent<Collider2D>().IsTouching(other))
                isPlayerInDetectionZone = true;
            SetAggro(true);

            if (Attack1Zone != null && Attack1Zone.GetComponent<Collider2D>().IsTouching(other))
                isPlayerInAttack1Zone = true;
            if (Attack2Zone != null && Attack2Zone.GetComponent<Collider2D>().IsTouching(other))
                isPlayerInAttack2Zone = true;
            if (Attack3Zone != null && Attack3Zone.GetComponent<Collider2D>().IsTouching(other))
                isPlayerInAttack3Zone = true;
            if (AttackSpecialZone != null && AttackSpecialZone.GetComponent<Collider2D>().IsTouching(other))
                isPlayerInAttackSpecialZone = true;
        }
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInDetectionZone = (Detection != null && Detection.GetComponent<Collider2D>().IsTouching(other));

            isPlayerInAttack1Zone = (Attack1Zone != null && Attack1Zone.GetComponent<Collider2D>().IsTouching(other));
            isPlayerInAttack2Zone = (Attack2Zone != null && Attack2Zone.GetComponent<Collider2D>().IsTouching(other));
            isPlayerInAttack3Zone = (Attack3Zone != null && Attack3Zone.GetComponent<Collider2D>().IsTouching(other));
            isPlayerInAttackSpecialZone = (AttackSpecialZone != null && AttackSpecialZone.GetComponent<Collider2D>().IsTouching(other));
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInDetectionZone = false;
            isPlayerInAttack1Zone = false;
            isPlayerInAttack2Zone = false;
            isPlayerInAttack3Zone = false;
            isPlayerInAttackSpecialZone = false;

            SetAggro(false);
        }
    }

    /// <summary>
    /// Called by Animation Events to apply damage if the player is in the corresponding attack zone.
    /// </summary>
    public void ApplyDamage1(float damage)
    {
        if (isPlayerInAttack1Zone)
        {
            ApplyDamage(damage);
        }
    }

    public void ApplyDamage2(float damage)
    {
        if (isPlayerInAttack2Zone)
        {
            ApplyDamage(damage);
        }
    }

    public void ApplyDamage3(float damage)
    {
        if (isPlayerInAttack3Zone)
        {
            ApplyDamage(damage);
        }
    }

    public void ApplyDamageSpecial(float damage)
    {
        if (isPlayerInAttackSpecialZone)
        {
            ApplyDamage(damage);
        }
    }

    public void PlaySwingSound()
    {
        if (swingSound != null)
        {
            swingSound.Play();
        }
    }

    private void ApplyDamage(float damage)
    {
        if (playerTransform == null)
        {
            Debug.LogWarning("No playerTransform found. Cannot apply damage.");
            return;
        }

        if (playerHealth == null)
        {
            playerHealth = playerTransform.GetComponent<PlayerHealth>();
            if (playerHealth == null)
            {
                Debug.LogWarning("PlayerHealth component not found on the player to apply damage to.");
                return;
            }
        }

        // Attempt to apply damage to the player
        bool damageApplied = playerHealth.TakeDamage(damage);

        if (damageApplied)
        {
            // Damage was applied, play hit sound
            if (hitSound != null) hitSound.Play();
        }
        else
        {
            // Damage was evaded, play evade sound if assigned
            if (evadeSound != null) evadeSound.Play();
        }
    }

    public void ResetAttackPlayingFlag()
    {
        animator.SetBool("attackPlaying", false);
    }

    // Handle damage-based aggro
    public void OnEveryDamageTaken()
    {
        float distanceToPlayer = Vector2.Distance(transform.position, playerTransform.position);

        // Become aggro if within chase distance after taking damage
        if (distanceToPlayer <= chaseDistanceAggro)
        {
            SetAggro(true);
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, proximityDetectionRadius);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, chaseDistanceAggro);
    }
}
