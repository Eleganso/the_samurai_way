using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages the health and related behaviors of an enemy.
/// </summary>
public class EnemyHealth : MonoBehaviour, IDamageable
{
    [SerializeField] public float maxHealth = 4f;
    private float currentHealth;
    public bool HasTakenDamage { get; set; }

    [SerializeField] private GameObject enableObject;
    [SerializeField] private GameObject disableObject;

    [SerializeField] private AudioSource damageTakenClip;
    [SerializeField] private AudioSource dyingClip;

    [SerializeField] private float pushForceX = 2f;
    [SerializeField] private float pushForceY = 0f;

    private Rigidbody2D rb;
    private Animator animator;
    private GameObject player;
    private Collider2D enemyCollider;

    [SerializeField] public string enemyID;
    [SerializeField] private int honorPoints = 10;

    // **Add the Respawnable checkbox**
    [Header("Respawn Settings")]
    [SerializeField] private bool respawnable = true;
    public bool Respawnable { get { return respawnable; } }

    // Health Bar Fields
    [SerializeField] private GameObject healthBarPrefab;
    private Slider healthSlider;
    private GameObject healthBarInstance;

    [SerializeField] private float damageResetTime = 0.5f;

    private bool enemyAlive = true;
    private IEnemyAggro enemyAggro;

    public bool IsEnemyAlive => enemyAlive;

    // Properties
    public float CurrentHealth
    {
        get => currentHealth;
        set
        {
            currentHealth = value;
            if (currentHealth <= 0)
            {
                Die();
            }
            UpdateHealthBar();
        }
    }

    public float MaxHealth => maxHealth;

    // Half Health Action Settings
    [Header("Half Health Action Settings")]
    [SerializeField] private bool enableHalfHealthAction = false;
    [SerializeField] private bool actionOnceOnly = false;
    [SerializeField] private float actionCooldown = 30f;

    public bool EnableHalfHealthAction => enableHalfHealthAction;
    public bool ActionOnceOnly => actionOnceOnly;
    public float ActionCooldown => actionCooldown;

    // On Damage Taken Action Settings
    [Header("On Damage Taken Action Settings")]
    [SerializeField] private bool enableOnDamageAction = false;
    [SerializeField] [Range(0f, 100f)] private float actionChancePercentage = 10f;

    private IEnemyActions enemyActions;

    // Reference to TimeController
    private TimeController timeController;

    // **Damage Particle Effect**
    [Header("Damage Particle Effect")]
    [SerializeField] private GameObject damageParticleEffectPrefab; // Particle effect prefab for damage
    [SerializeField] private GameObject dyingParticleEffectPrefab; // Particle effect prefab for damage

    private void Start()
    {
        InitializeHealth(true);
        CreateHealthBar();

        enemyActions = GetComponent<IEnemyActions>();
        enemyAggro = GetComponent<IEnemyAggro>();

        // Dynamically find TimeController via GameManager.Instance
        if (GameManager.Instance != null)
        {
            timeController = GameManager.Instance.GetComponent<TimeController>();
            if (timeController == null)
            {
                Debug.LogWarning("TimeController component not found on GameManager.");
            }
        }
        else
        {
            Debug.LogWarning("GameManager.Instance is null. Cannot find TimeController.");
        }
    }

    /// <summary>
    /// Initializes the enemy's health and status.
    /// </summary>
    /// <param name="firstTime">Indicates if this is the first initialization.</param>
    public void InitializeHealth(bool firstTime)
    {
        if (GameManager.Instance.IsEnemyDead(enemyID))
        {
            gameObject.SetActive(false);
            return;
        }

        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
        player = GameObject.FindGameObjectWithTag("Player");
        enemyCollider = GetComponent<Collider2D>();
        enemyAlive = true;
        animator.SetBool("enemyAlive", true);

        if (enableObject != null)
            enableObject.SetActive(false);
        if (disableObject != null)
            disableObject.SetActive(true);

        if (firstTime)
        {
            float savedHealth = GameManager.Instance.GetEnemyHealth(enemyID);
            CurrentHealth = savedHealth > 0 ? savedHealth : maxHealth;
        }
        else
        {
            UpdateHealthBar();
        }
    }

    private void CreateHealthBar()
    {
        if (healthBarPrefab != null)
        {
            Vector3 healthBarPosition = transform.position + new Vector3(0f, 1.5f, 0);
            healthBarInstance = Instantiate(healthBarPrefab, healthBarPosition, Quaternion.identity);
            healthSlider = healthBarInstance.GetComponentInChildren<Slider>();
            healthSlider.maxValue = maxHealth;
            healthSlider.value = currentHealth;
            healthBarInstance.transform.SetParent(transform);
        }
    }

    private void UpdateHealthBar()
    {
        if (healthSlider != null)
        {
            healthSlider.value = currentHealth;
        }
    }

    public void Damage(float damageAmount, GameObject damageSource)
{
    Shielder shielder = GetComponent<Shielder>();
    if (shielder != null && shielder.isBlocking)
    {
        shielder.TriggerBlock();
        Debug.Log("Shielder blocked the attack! No damage applied.");
        return;
    }

    if (CurrentHealth <= 0) return;

    // Check if enemy is not aggroed
    // If not aggroed and being hit by the player (player layer) 
    // or by a fireball layer (assuming "Fireball" is the layer name) when MagicSkill1 is unlocked, apply the damage multiplier.
    bool isNonAggroed = (enemyAggro != null && !enemyAggro.IsAggroed);

    // Check if the object hitting is from the player layer
    bool isPlayerAttack = damageSource.layer == LayerMask.NameToLayer("Player");

    // Check if MagicSkill1 is unlocked and the source is from the Fireball layer
    bool isPlayerSpell = PlayerManager.Instance.IsSkillUnlocked("FireballSkill1") 
                     && damageSource.layer == LayerMask.NameToLayer("Fireball");

    if (isNonAggroed && (isPlayerAttack || isPlayerSpell))
    {
        damageAmount *= PlayerManager.Instance.damageMultiplier;

        // If using a TimeController slow-mo effect for critical hits:
        if (timeController != null)
        {
            timeController.ScreenShake();
            timeController.ActivateSlowTime();
        }
    }

    damageTakenClip?.Play();

    // Instantiate Damage Particle Effect if exists
    if (damageParticleEffectPrefab != null)
    {
        GameObject effect = Instantiate(damageParticleEffectPrefab, transform.position, Quaternion.identity);
        Destroy(effect, 1f);
    }

    HasTakenDamage = true;
    CurrentHealth -= damageAmount;
    GameManager.Instance.UpdateEnemyHealth(enemyID, CurrentHealth);

    Vector2 pushDirection = transform.position.x > player.transform.position.x
        ? new Vector2(pushForceX, pushForceY)
        : new Vector2(-pushForceX, pushForceY);
    rb.AddForce(pushDirection, ForceMode2D.Impulse);

    if (enemyAggro != null)
    {
        enemyAggro.SetAggro(true);
    }

    StartCoroutine(ResetDamageState());

    if (enemyActions != null)
    {
        enemyActions.OnEveryDamageTaken();
    }

    // If enableOnDamageAction is true, there's a chance to trigger some action
    if (enableOnDamageAction && enemyAlive)
    {
        float randomValue = Random.Range(0f, 100f);
        if (randomValue <= actionChancePercentage)
        {
            enemyActions?.OnSomeDamageTaken();
        }
    }
}



    private IEnumerator ResetDamageState()
    {
        yield return new WaitForSeconds(damageResetTime);
        HasTakenDamage = false;
    }

    private void Die()
    {
        Transform enemyHeadTransform = transform.Find("EnemyHead");
        if (enemyHeadTransform != null)
        {
            enemyHeadTransform.gameObject.SetActive(false);
        }

        if (enemyAlive)
        {
            rb.linearVelocity = Vector2.zero;
            enemyAlive = false;
            animator.SetBool("enemyAlive", false);
            animator.SetTrigger("dead");
            rb.isKinematic = true;
            enemyCollider.enabled = false;
            dyingClip?.Play();
            // **Instantiate Dying Particle Effect**
        if (dyingParticleEffectPrefab != null)
        {
            GameObject effect = Instantiate(dyingParticleEffectPrefab, transform.position, Quaternion.identity);
            Destroy(effect, 1f); // Destroy the effect after 1 second
        }

            if (enableObject != null)
                enableObject.SetActive(true);
            if (disableObject != null)
                disableObject.SetActive(false);

            GameManager.Instance.MarkEnemyAsDead(enemyID, Respawnable);
            PlayerManager.Instance.AddHonorPoints(honorPoints);
        }
    }

    public void DestroyEnemy()
    {
        Destroy(gameObject);
    }

    public void Disable()
    {
        gameObject.SetActive(false);
    }

    public void EnableAndRefillHealth()
    {
        gameObject.SetActive(true);
        CurrentHealth = maxHealth;
        GameManager.Instance.UpdateEnemyHealth(enemyID, CurrentHealth);
        enemyAlive = true;
        animator.SetBool("enemyAlive", true);
        enemyCollider.enabled = true;
        rb.isKinematic = false;

        if (disableObject != null)
            disableObject.SetActive(true);
        if (enableObject != null)
            enableObject.SetActive(false);

        ResetComponents();
    }

    private void ResetComponents()
    {
        // Reset any other components disabled during death
    }
}
