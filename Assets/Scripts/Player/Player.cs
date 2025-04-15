// Assets/Scripts/Player/Player.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class Player : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 7.5f;
    [SerializeField] private float crouchingModifier = 0.5f;
    private float originalMoveSpeed;

    [Header("Jump")]
    [SerializeField] private float jumpForce = 5f;
    [SerializeField] private float jumpTime = 0.5f;
    private float jumpTimeCounter;
    private bool isJumping;
    private bool isFalling;

    [Header("Turn Check")]
    [SerializeField] private GameObject lLeg;
    [SerializeField] private GameObject rLeg;

    [Header("Ground Check")]
    [SerializeField] private float extraHeight = 0.25f;
    [SerializeField] private LayerMask whatIsGround;

    [Header("Dodge")]
    [SerializeField] private float dodgeSpeed = 15f;
    [SerializeField] private float dodgeDuration = 0.5f;
    [SerializeField] private float dodgeCooldown = 2f;
    private float dodgeCooldownTimer = 0f;

    [Header("Grappling Hook Settings")]
    [SerializeField] private float grappleRange = 15f; // Maximum grapple distance
    [SerializeField] private float pullStrength = 20f; // Force applied when pulling
    [SerializeField] private float ropeSpeed = 10f; // Speed of rope animation
    [SerializeField] private float pullDelay = 0.5f; // Delay before pull starts
    [SerializeField] private float grappleCooldown = 2f; // Time between grapples
    [SerializeField] private LayerMask hookableLayer;   // LayerMask for hookable objects

    [Header("Grappling Hook Visual Settings")]
    [SerializeField] private Color ropeColor = Color.white; // Color of the grappling rope
    [SerializeField] private float ropeThickness = 0.05f;   // Thickness of the grappling rope
    [SerializeField] private Material grappleRopeMaterial; // Optional: Assign a custom material in Inspector

    private float grappleCooldownTimerLocal = 0f; // Local cooldown timer for grapple
    private bool isGrappling = false;
    public bool IsGrappling
    {
        get { return isGrappling; }
    }
    private Vector2 grapplePoint;
    private LineRenderer grappleLine; // For visualizing the grappling hook
    private Coroutine grappleCoroutine;

    private bool isSwinging = false;
    private Vector2 swingAnchor;

    private Vector2 GetMouseWorldPosition()
    {
        Vector3 mouseScreenPosition = Mouse.current.position.ReadValue();
        Vector3 mouseWorldPosition = Camera.main.ScreenToWorldPoint(mouseScreenPosition);
        return new Vector2(mouseWorldPosition.x, mouseWorldPosition.y);
    }

    [Header("Audio")]
    public AudioClip grappleStartSound; // Assign in Inspector
    public AudioClip grapplePullSound;  // Assign in Inspector
    public AudioClip grappleEndSound;   // Assign in Inspector
    private AudioSource audioSource;

    [Header("UI Elements")]
    [SerializeField] private Image cooldownDisplay; // Existing cooldown display for dodge
    [SerializeField] private Image grappleCooldownDisplay; // Assign an Image for grapple cooldown

    [Header("Spell")]
    [SerializeField] private GameObject fireballPrefab; // Prefab for the fireball
    [SerializeField] private Transform fireballSpawnPoint; // Where to spawn the fireball

    [Header("Colliders")]
    [SerializeField] public Collider2D colliderStanding;
    [SerializeField] public Collider2D colliderCrouching;

    [Header("Transparency Settings")]
    [SerializeField] public float hiddenAlpha = 0.5f;  // Alpha value when hiding (50% transparent)
    [SerializeField] public float normalAlpha = 1f;    // Alpha value when not hiding (100% opaque)

    [HideInInspector] public bool isFacingRight;
    [HideInInspector] public bool isCrouchingInBushes = false;
    [HideInInspector] public bool isInBush = false; // Indicates if the player is inside a bush
    [HideInInspector] public bool isCrouching = false;
    private bool isDodging = false;
    private Rigidbody2D rb;
    private Animator anim;
    private float moveInput;
    public bool isClimbingLadder = false; // Should be set by your ladder climbing logic

    private PlayerHealth playerHealth;
    private SpriteRenderer spriteRenderer; // Reference to SpriteRenderer

    /// <summary>
    /// Stops the flask usage animation and resets animation states.
    /// </summary>
    public void StopUseFlaskAnimation()
    {
        anim.ResetTrigger("UseFlask"); // Reset the trigger to stop the animation
        anim.SetBool("Casting", false); // Ensure casting state is false
        Debug.Log("UseFlask animation stopped.");
    }

    /// <summary>
    /// Triggers the flask usage animation.
    /// </summary>
    public void PlayUseFlaskAnimation()
    {
        anim.SetTrigger("UseFlask");
    }

    [Header("Spell Casting Settings")]
    [SerializeField] private float castingDuration = 1f; // Duration of the casting animation in seconds

    private bool wasMovingBeforeCast = false; // Flag to remember if the player was moving before casting

    private Coroutine castingCoroutine; // Reference to the current casting coroutine
    private bool isCasting = false; // Flag to track casting state

    [Header("Spell Cooldown Settings")]
    [SerializeField] private float spellCooldown = 0.2f; // Debounce time in seconds
    private float spellCooldownTimer = 0f;

    // Reference to PlayerAttack to check if attacking
    private PlayerAttack playerAttack;

    private void Start()
{
    rb = GetComponent<Rigidbody2D>();
    anim = GetComponent<Animator>();
    spriteRenderer = GetComponent<SpriteRenderer>();  // Get SpriteRenderer component
    playerAttack = GetComponent<PlayerAttack>(); // Get PlayerAttack component
    audioSource = GetComponent<AudioSource>(); // Initialize AudioSource
    if (audioSource == null)
    {
        Debug.LogError("AudioSource component missing on Player.");
    }

    StartDirectionCheck();

    // Initialize the cooldown display to full (ready to use)
    if (cooldownDisplay != null)
    {
        UpdateCooldownDisplay(1);
    }

    // Initialize the grapple cooldown display to full
    if (grappleCooldownDisplay != null)
    {
        UpdateGrappleCooldownUI(1);
    }

    // Get reference to PlayerHealth
    playerHealth = GetComponent<PlayerHealth>();
    if (playerHealth == null)
    {
        Debug.LogError("PlayerHealth component not found on the player object.");
    }

    // Store original move speed
    originalMoveSpeed = moveSpeed;

    // Ensure colliders are set correctly
    if (colliderStanding == null || colliderCrouching == null)
    {
        Debug.LogError("Please assign both colliderStanding and colliderCrouching in the inspector.");
    }

    // Enable standing collider and disable crouching collider at start
    colliderStanding.enabled = true;
    colliderCrouching.enabled = false;

    // Set initial transparency to normal (fully opaque)
    SetTransparency(normalAlpha);

    // Check if Speed Skill 1 is unlocked and adjust the dodge cooldown accordingly
    if (PlayerManager.Instance.IsSkillUnlocked("SpeedSkill1"))
    {
        dodgeCooldown = 4f; // Reduced cooldown if skill is unlocked
        Debug.Log("Speed Skill 1 unlocked, dodge cooldown set to 4 seconds.");
    }
    else
    {
        // If not unlocked, you can either leave the default or log it
        // Debug.Log("Speed Skill 1 not unlocked, dodge cooldown remains at default value.");
    }
}


    // Method to set the Layer Override dynamically
    public void SetLayerOverride(string newLayer)
    {
        int layer = LayerMask.NameToLayer(newLayer);
        if (layer == -1)
        {
            Debug.LogError($"Layer '{newLayer}' not found!");
            return;
        }
        gameObject.layer = layer;

        // If you have children objects that need to be on the same layer (like a sprite or collider)
        foreach (Transform child in transform)
        {
            child.gameObject.layer = layer;
        }
    }

    #region ControlManagement
    private bool areControlsDisabled = false;

    /// <summary>
    /// Disables player actions: movement, attack, dodge, spell, jump, crouch, and flask.
    /// Called via animation event at the start of UseFlask animation.
    /// </summary>
    public void DisablePlayerActions()
    {
        Debug.Log("DisablePlayerActions called: Disabling player actions.");

        // Disable User Input
        UserInput.instance.DisableControls();

        // Stop the player's movement
        rb.linearVelocity = Vector2.zero;
        moveInput = 0f;

        // Disable PlayerAttack
        PlayerAttack playerAttack = GetComponent<PlayerAttack>();
        if (playerAttack != null)
        {
            playerAttack.DisableAttack();
        }

        // Disable other actions if necessary
        // For example, disable jumping, dodging, etc.
        // You can add additional methods or flags here as needed
    }

    /// <summary>
    /// Enables player actions: movement, attack, dodge, spell, jump, crouch, and flask.
    /// Called via animation event at the end of UseFlask animation.
    /// </summary>
    public void EnablePlayerActions()
    {
        Debug.Log("EnablePlayerActions called: Enabling player actions.");

        // Enable User Input
        UserInput.instance.EnableControls();

        // Enable PlayerAttack
        PlayerAttack playerAttack = GetComponent<PlayerAttack>();
        if (playerAttack != null)
        {
            playerAttack.EnableAttack();
        }

        // Enable other actions if necessary
        // For example, enable jumping, dodging, etc.
        // You can add additional methods or flags here as needed
    }

    #endregion

    private void Update()
    {
        // Prevent any actions if casting or attacking
        if (isCasting) return;

        HandleCrouch();

        Move();
        Jump();

        // Cooldown timer decrement and UI update
        if (dodgeCooldownTimer > 0)
        {
            dodgeCooldownTimer -= Time.deltaTime;
            UpdateCooldownDisplay(1 - (dodgeCooldownTimer / dodgeCooldown));
        }

        // Spell cooldown timer decrement
        if (spellCooldownTimer > 0)
        {
            spellCooldownTimer -= Time.deltaTime;
        }

        // Check if the dodge is off cooldown and the player has pressed the dodge button
        if (UserInput.instance.controls.Dodge.Dodge.WasPressedThisFrame() && !isDodging && dodgeCooldownTimer <= 0 && !isCrouching)
        {
            StartCoroutine(Dodge());
        }

        

        // Update Bush Stealth State if necessary
        if (isCrouchingInBushes)
        {
            UpdateBushState();
        }

        // Grapple Cooldown Timer
        if (grappleCooldownTimerLocal > 0f)
        {
            grappleCooldownTimerLocal -= Time.deltaTime;
            UpdateGrappleCooldownUI(1 - (grappleCooldownTimerLocal / grappleCooldown));
        }

       
    }

    #region Crouch Methods

    private void HandleCrouch()
    {
        if (UserInput.instance.verticalInput < 0 && IsGrounded() && !isClimbingLadder)
        {
            if (!isCrouching)
            {
                StartCrouch();
            }
        }
        else
        {
            if (isCrouching)
            {
                StopCrouch();
            }
        }
    }

    private void StartCrouch()
    {
        isCrouching = true;
        anim.SetBool("isCrouching", true);

        // Disable standing collider and enable crouching collider
        colliderStanding.enabled = false;
        colliderCrouching.enabled = true;

        // Reduce movement speed while crouching
        moveSpeed = originalMoveSpeed * crouchingModifier;

        // Check if player is fully in bushes
        if (isInBush)
        {
            UpdateBushState();
        }
    }

    private void StopCrouch()
    {
        isCrouching = false;
        anim.SetBool("isCrouching", false);

        // Enable standing collider and disable crouching collider
        colliderStanding.enabled = true;
        colliderCrouching.enabled = false;

        // Reset movement speed
        moveSpeed = originalMoveSpeed;

        // Reset crouching in bushes
        isCrouchingInBushes = false;

        // Restore transparency if previously hidden
        SetTransparency(normalAlpha);
    }

    public bool IsCrouching()
    {
        return isCrouching;
    }

    #endregion

    #region Spell Casting

    /// <summary>
    /// Initiates the spell-casting process.
    /// Ensures that casting only occurs when grounded, not attacking, not crouching, and not moving.
    /// If moving, stops movement before casting and resumes after casting.
    /// </summary>
    public void CastSpell()
    {
        float manaCostPerSpell = PlayerManager.Instance.manaCostPerSpell;

        // Check if the player is crouching
        if (isCrouching)
        {
            Debug.Log("Cannot cast spell while crouching!");
            return;
        }

        // Check if the player is grounded
        if (!IsGrounded())
        {
            Debug.Log("Cannot cast spell while in the air!");
            return;
        }

        // Check if the player has enough mana
        if (playerHealth.GetMana() < manaCostPerSpell)
        {
            Debug.Log("Not enough mana to cast the spell!");
            return;
        }

        // Check if the player is moving
        if (Mathf.Abs(moveInput) > 0.1f)
        {
            // Player is moving; stop movement and cast spell
            if (!isCasting)
            {
                wasMovingBeforeCast = true;
                rb.linearVelocity = Vector2.zero;
                moveInput = 0f; // Stop movement input
                StartCoroutine(CastSpellCoroutine());
            }
        }
        else
        {
            // Player is not moving; cast spell normally
            if (!isCasting)
            {
                StartCoroutine(CastSpellCoroutine());
            }
        }
    }

    /// <summary>
    /// Coroutine to handle spell casting process.
    /// Sets casting state, triggers animation, waits for casting duration, and resets casting state.
    /// The fireball is launched via an animation event.
    /// </summary>
    /// <returns>IEnumerator for coroutine.</returns>
    private IEnumerator CastSpellCoroutine()
{
    isCasting = true;

    // Trigger the casting animation
    anim.SetTrigger("Spell");
    SetCastingTrue(); // Set Casting to true via animation event method

   // Determine the base mana cost
float manaCost = PlayerManager.Instance.manaCostPerSpell;

// Apply Mana Skill multiplier if active
manaCost *= PlayerManager.Instance.ManaCostMultiplier;

// Check if Mana Skill 1 is unlocked for the 5% chance of free spell
if (PlayerManager.Instance.IsSkillUnlocked("ManaSkill1"))
{
    float chance = Random.value; // Random float between 0 and 1
    if (chance <= 0.05f)
    {
        Debug.Log("ManaSkill1 triggered! Spell cast without mana cost.");
        manaCost = 0f;
    }
}

// Deduct mana if manaCost > 0
if (manaCost > 0)
{
    playerHealth.UseMana(manaCost);
}

    // Wait for the casting duration (synchronized with animation)
    yield return new WaitForSeconds(castingDuration);

    // Reset casting state
    SetCastingFalse(); // Set Casting to false via animation event method
    isCasting = false;

    // Resume movement if the player was moving before casting
    if (wasMovingBeforeCast)
    {
        wasMovingBeforeCast = false;
        // Movement will resume naturally based on player input in Update()
    }
}


    /// <summary>
    /// Instantiates and launches the fireball.
    /// This method is called via an animation event during the casting animation.
    /// </summary>
    public void LaunchFireball()
{
    // Cancel StealthSkill if active
    if (PlayerManager.Instance.IsStealthSkillActive)
    {
        PlayerManager.Instance.DeactivateStealthSkill();
        Debug.Log("Stealth Skill cancelled due to casting fireball.");
    }

    if (fireballPrefab == null || fireballSpawnPoint == null)
    {
        Debug.LogError("Fireball Prefab or Spawn Point is not assigned.");
        return;
    }

    // Instantiate the fireball at the spawn point
    GameObject fireball = Instantiate(fireballPrefab, fireballSpawnPoint.position, Quaternion.identity);
    Fireball fireballScript = fireball.GetComponent<Fireball>();

    if (fireballScript == null)
    {
        Debug.LogError("Fireball Prefab does not have a Fireball script attached.");
        Destroy(fireball);
        return;
    }

    // Set the fireball's damage and direction based on player's current stats
    fireballScript.SetDamage(PlayerManager.Instance.fireballDamage);
    float direction = isFacingRight ? 1f : -1f;
    fireballScript.SetDirection(direction);

    Debug.Log("Fireball launched in direction: " + (isFacingRight ? "Right" : "Left"));
}


    /// <summary>
    /// Called by animation event to set Casting to true.
    /// </summary>
    public void SetCastingTrue()
    {
        anim.SetBool("Casting", true);
        Debug.Log("Casting started.");
    }

    /// <summary>
    /// Called by animation event to set Casting to false.
    /// </summary>
    public void SetCastingFalse()
    {
        anim.SetBool("Casting", false);
        Debug.Log("Casting ended.");
    }

    #endregion

    #region Movement Functions

    private void Move()
    {
        if (isDodging) return;

        moveInput = UserInput.instance.moveInput.x;

        if (moveInput != 0)
        {
            anim.SetBool("isWalking", true);
            TurnCheck();
        }
        else
        {
            anim.SetBool("isWalking", false);
        }

        rb.linearVelocity = new Vector2(moveInput * moveSpeed, rb.linearVelocity.y);
    }

    private void Jump()
    {
        if (isDodging || isCrouching) return;

        if (UserInput.instance.controls.Jumping.Jump.WasPressedThisFrame() && IsGrounded())
        {
            isJumping = true;
            jumpTimeCounter = jumpTime;
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            anim.SetTrigger("jump");
        }

        if (UserInput.instance.controls.Jumping.Jump.IsPressed() && isJumping)
        {
            if (jumpTimeCounter > 0)
            {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
                jumpTimeCounter -= Time.deltaTime;
            }
            else
            {
                isJumping = false;
            }
        }

        if (UserInput.instance.controls.Jumping.Jump.WasReleasedThisFrame())
        {
            isJumping = false;
            isFalling = true;
        }

        if (!isJumping && CheckForLand())
        {
            anim.SetTrigger("land");
        }
    }

    #endregion

    #region Ground/Landed Check

    public bool IsGrounded()
    {
        Collider2D coll = isCrouching ? colliderCrouching : colliderStanding;
        RaycastHit2D groundHit = Physics2D.BoxCast(coll.bounds.center, coll.bounds.size, 0f, Vector2.down, extraHeight, whatIsGround);
       // Debug.Log($"IsGrounded Check: {(groundHit.collider != null ? "Grounded" : "Airborne")}");
        return groundHit.collider != null;
    }

    private bool CheckForLand()
    {
        if (isFalling && IsGrounded())
        {
            isFalling = false;
            return true;
        }
        return false;
    }

    #endregion

    #region Dodge Mechanic

    private IEnumerator Dodge()
    {
        isDodging = true;
        float originalGravity = rb.gravityScale;
        rb.gravityScale = 0; // Optional: Disable gravity during dodge

        anim.Play("Dash"); // Play the dash animation

        gameObject.layer = LayerMask.NameToLayer("PlayerDodging");

        PlayerSoundManager.Instance?.PlayDodgeSound();

        rb.linearVelocity = new Vector2((isFacingRight ? 1 : -1) * dodgeSpeed, 0);

        yield return new WaitForSeconds(dodgeDuration);

        rb.gravityScale = originalGravity; // Restore gravity
        rb.linearVelocity = Vector2.zero; // Stop the player after dodge
        gameObject.layer = LayerMask.NameToLayer("Player");
        isDodging = false;

        anim.Play("Idle");

        dodgeCooldownTimer = dodgeCooldown;
        UpdateCooldownDisplay(0);
    }

    private void UpdateCooldownDisplay(float progress)
    {
        if (cooldownDisplay != null)
        {
            cooldownDisplay.fillAmount = progress;
        }
    }

    #endregion

    #region Turn Checks

    private void StartDirectionCheck()
    {
        isFacingRight = rLeg.transform.position.x > lLeg.transform.position.x;
    }

    public void TurnCheck()
    {
        if ((moveInput > 0 && !isFacingRight) || (moveInput < 0 && isFacingRight))
        {
            Turn();
        }
    }

    private void Turn()
    {
        Vector3 newScale = transform.localScale;
        newScale.x *= -1;
        transform.localScale = newScale;
        isFacingRight = !isFacingRight;
    }

    #endregion

    #region Bush Stealth Mechanics

    public bool IsFullyInsideBush()
    {
        Collider2D playerCollider = isCrouching ? colliderCrouching : colliderStanding;

        Collider2D[] overlappingColliders = Physics2D.OverlapBoxAll(playerCollider.bounds.center, playerCollider.bounds.size, 0f);
        foreach (Collider2D collider in overlappingColliders)
        {
            if (collider.CompareTag("Bush"))
            {
                if (collider.bounds.Contains(playerCollider.bounds.min) && collider.bounds.Contains(playerCollider.bounds.max))
                {
                    return true;
                }
            }
        }
        return false;
    }

    public void UpdateBushState()
    {
        if (isInBush && isCrouching)
        {
            isCrouchingInBushes = IsFullyInsideBush();
            SetTransparency(hiddenAlpha);  // Make player transparent when hiding
        }
        else
        {
            isCrouchingInBushes = false;
            SetTransparency(normalAlpha);  // Restore normal transparency
        }
    }

    // Set this method to public to make it accessible from other classes
    public void SetTransparency(float alphaValue)
    {
        if (spriteRenderer != null)
        {
            Color color = spriteRenderer.color;
            color.a = alphaValue;
            spriteRenderer.color = color;  // Update the sprite's transparency
          //  Debug.Log($"SetTransparency called. New alpha: {alphaValue}");
        }
    }

    #endregion

    #region Grappling Hook

    /// <summary>
    /// Attempts to perform a grapple action.
    /// Called by PlayerManager via the New Input System.
    /// </summary>
    public void AttemptGrapple()
{
    if (!PlayerManager.Instance.hasGrapplingHook)
    {
        Debug.Log("Grappling Hook not unlocked yet.");
        return;
    }

    if (isGrappling)
    {
        Debug.Log("Already grappling.");
        return;
    }

    // **Check if grapple is on cooldown**
    if (grappleCooldownTimerLocal > 0f)
    {
        Debug.Log("Grappling Hook is on cooldown.");
        return;
    }

    Vector2 mousePosition = GetMouseWorldPosition();
    Vector2 playerPosition = transform.position;
    Vector2 direction = (mousePosition - playerPosition).normalized;

    UpdateFacingDirection(direction);

    Debug.Log($"AttemptGrapple() called in Player. Direction: {direction}");

    // Visualize the raycast
    Debug.DrawRay(playerPosition, direction * grappleRange, Color.red, 1f); // Duration: 1 second

    RaycastHit2D hit = Physics2D.Raycast(playerPosition, direction, grappleRange, hookableLayer);

    if (hit.collider != null && hit.collider.CompareTag("HookPoint"))
    {
        grapplePoint = hit.point;
        StartCoroutine(Grapple());

        // **Start cooldown only after successful grapple**
        grappleCooldownTimerLocal = grappleCooldown; // Set cooldown
        Debug.Log($"Grapple cooldown set to {grappleCooldown} seconds.");

        Debug.Log("Grapple action triggered towards hook point.");
    }
    else
    {
        Debug.Log("No hook point within range towards mouse position.");
    }
}


    private void UpdateFacingDirection(Vector2 direction)
    {
        if (direction.x > 0 && !isFacingRight)
        {
            Turn();
        }
        else if (direction.x < 0 && isFacingRight)
        {
            Turn();
        }
    }

    private IEnumerator Grapple()
    {
        isGrappling = true;

        // Disable player controls while grappling
        UserInput.instance.DisableControls();
        Debug.Log("Player controls disabled for grappling.");

        // Play grapple start sound
        PlayGrappleStartSound();

        // Initialize the grapple line
        if (grappleLine == null)
        {
            grappleLine = gameObject.AddComponent<LineRenderer>();
            grappleLine.startWidth = ropeThickness; // Use ropeThickness
            grappleLine.endWidth = ropeThickness;   // Use ropeThickness
            if (grappleRopeMaterial != null)
            {
                grappleLine.material = grappleRopeMaterial;
            }
            else
            {
                grappleLine.material = new Material(Shader.Find("Sprites/Default"));
            }
            grappleLine.startColor = ropeColor; // Set to ropeColor
            grappleLine.endColor = ropeColor;   // Set to ropeColor
            grappleLine.positionCount = 2;
            Debug.Log("LineRenderer for grappling hook initialized.");
        }
        else
        {
            // Update the width in case ropeThickness was changed in the inspector
            grappleLine.startWidth = ropeThickness;
            grappleLine.endWidth = ropeThickness;
        }

        // Set the line positions
        grappleLine.enabled = true;
        grappleLine.SetPosition(0, transform.position);
        grappleLine.SetPosition(1, transform.position); // Start at player's position
        Debug.Log($"Grapple line initialized at position: {transform.position}");

        // Animate rope extension
        yield return StartCoroutine(AnimateRope(transform.position, grapplePoint));

        // Wait for pull delay
        yield return new WaitForSeconds(pullDelay);

        // Play pull sound
        PlayGrapplePullSound();

        // Apply pull force
        rb.AddForce((grapplePoint - (Vector2)transform.position).normalized * pullStrength, ForceMode2D.Impulse);

        // Pull the player towards the grapple point
        while (Vector2.Distance(transform.position, grapplePoint) > 0.1f)
        {
            Vector2 direction = (grapplePoint - (Vector2)transform.position).normalized;
            rb.linearVelocity = direction * pullStrength;
            UpdateGrappleLine();
            yield return null;
        }

        // Snap to grapple point
        transform.position = grapplePoint;

        // Disable the grapple line
        grappleLine.enabled = false;

        // Play grapple end sound
        PlayGrappleEndSound();

        // Re-enable player controls
        UserInput.instance.EnableControls();

        isGrappling = false;

        Debug.Log("Grappling completed.");
    }

    /// <summary>
    /// Animates the rope extending towards the grapple point.
    /// </summary>
    /// <param name="start">Starting position (player).</param>
    /// <param name="end">Grapple point position.</param>
    /// <returns>IEnumerator for coroutine.</returns>
    private IEnumerator AnimateRope(Vector2 start, Vector2 end)
    {
        float distance = Vector2.Distance(start, end);
        float duration = distance / ropeSpeed;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            Vector2 currentPoint = Vector2.Lerp(start, end, t);
            grappleLine.SetPosition(1, currentPoint);
            elapsed += Time.deltaTime;
            yield return null;
        }

        grappleLine.SetPosition(1, end);
    }

    /// <summary>
    /// Updates the grapple line positions.
    /// </summary>
    private void UpdateGrappleLine()
    {
        if (grappleLine != null && isGrappling)
        {
            grappleLine.SetPosition(0, transform.position);
            grappleLine.SetPosition(1, grapplePoint);
        }
    }

    /// <summary>
    /// Plays the grapple start sound effect.
    /// </summary>
    private void PlayGrappleStartSound()
    {
        if (grappleStartSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(grappleStartSound);
            Debug.Log("Grapple start sound played.");
        }
        else
        {
            Debug.LogWarning("Grapple start sound not assigned or AudioSource missing.");
        }
    }

    /// <summary>
    /// Plays the grapple pull sound effect.
    /// </summary>
    private void PlayGrapplePullSound()
    {
        if (grapplePullSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(grapplePullSound);
            Debug.Log("Grapple pull sound played.");
        }
        else
        {
            Debug.LogWarning("Grapple pull sound not assigned or AudioSource missing.");
        }
    }

    /// <summary>
    /// Plays the grapple end sound effect.
    /// </summary>
    private void PlayGrappleEndSound()
    {
        if (grappleEndSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(grappleEndSound);
            Debug.Log("Grapple end sound played.");
        }
        else
        {
            Debug.LogWarning("Grapple end sound not assigned or AudioSource missing.");
        }
    }

    #endregion

    #region Swinging Mechanics

    /// <summary>
    /// Starts swinging from the grapple point.
    /// </summary>
    private void StartSwing()
    {
        if (!isGrappling) return;
        isSwinging = true;
        swingAnchor = grapplePoint;
        rb.gravityScale = 1f; // Ensure gravity affects the player
        rb.linearVelocity = Vector2.zero; // Reset velocity for swinging
        Debug.Log("Swinging started.");
    }

    /// <summary>
    /// Stops swinging and resumes normal movement.
    /// </summary>
    private void StopSwing()
    {
        isSwinging = false;
        rb.gravityScale = 0f; // Disable gravity during normal movement
        Debug.Log("Swinging stopped.");
    }

    private void FixedUpdate()
    {
        if (isSwinging)
        {
            Vector2 direction = (swingAnchor - rb.position).normalized;
            float distance = Vector2.Distance(rb.position, swingAnchor);
            float forceMagnitude = pullStrength / distance; // Adjust force based on distance
            rb.AddForce(direction * forceMagnitude);
        }
    }

    #endregion

    #region Grapple Cooldown UI

    /// <summary>
    /// Updates the grapple cooldown UI element.
    /// </summary>
    /// <param name="progress">Progress value between 0 and 1.</param>
    private void UpdateGrappleCooldownUI(float progress)
    {
        if (grappleCooldownDisplay != null)
        {
            grappleCooldownDisplay.fillAmount = progress;
        }
    }

    #endregion

    #region Additional Methods

    // Additional methods related to movement, casting, etc., remain unchanged

    #endregion
}
