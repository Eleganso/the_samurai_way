using UnityEngine;
using Enemies.Navigation;

namespace Enemies.Navigation
{
    /// <summary>
    /// Base navigation behavior for all enemy types.
    /// Attach this to any enemy that needs to use the enhanced navigation system.
    /// </summary>
    [RequireComponent(typeof(EnemyNavigationController))]
    public class BaseEnemyNavigation : MonoBehaviour, INavigationBehavior
    {
        [Header("Navigation Settings")]
        [SerializeField] protected bool preferLadders = true;
        [SerializeField] protected bool canJumpOverObstacles = true;
        [SerializeField] protected float jumpObstacleHeightThreshold = 1.0f;
        [SerializeField] public float attackRange = 1.5f;
        
        // References
        protected EnemyNavigationController navigationController;
        protected Transform targetTransform;
        protected IEnemyActions enemyActions;
        protected IEnemyAggro enemyAggro;
        
        // Navigation state tracking
        protected bool isPerformingAction = false;
        protected float lastDirectionChangeTime = 0f;
        protected const float DIRECTION_CHANGE_COOLDOWN = 0.5f;
        
        protected virtual void Awake()
        {
            // Get required components
            navigationController = GetComponent<EnemyNavigationController>();
            enemyActions = GetComponent<IEnemyActions>();
            enemyAggro = GetComponent<IEnemyAggro>();
            
            if (navigationController == null)
            {
                Debug.LogError($"{gameObject.name} requires an EnemyNavigationController component!");
            }
        }
        
        protected virtual void Start()
        {
            // Find target (usually player)
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                targetTransform = player.transform;
                
                // Set the target in the navigation controller
                if (navigationController != null)
                {
                    navigationController.SetTarget(targetTransform);
                }
            }
            else
            {
                Debug.LogWarning("Player not found! Enemy navigation target is null.");
            }
        }
        
        /// <summary>
        /// Called when navigation is determining if it should jump
        /// Implements INavigationBehavior
        /// </summary>
        public virtual bool ShouldJump(bool isObstacleAhead, bool isEdgeAhead, bool isTargetAbove)
        {
            // Don't jump during actions
            if (isPerformingAction)
                return false;
            
            // Check if we can and should jump over obstacles
            if (isObstacleAhead && canJumpOverObstacles)
            {
                // Use obstacle detection to determine height
                ObstacleDetection detector = GetComponent<ObstacleDetection>();
                if (detector != null)
                {
                    bool isFacingRight = transform.localScale.x > 0;
                    float obstacleHeight = detector.GetObstacleHeight(isFacingRight);
                    
                    // Only jump if obstacle isn't too high
                    if (obstacleHeight > 0 && obstacleHeight <= jumpObstacleHeightThreshold)
                    {
                        Debug.Log($"Enemy decides to jump over obstacle with height {obstacleHeight}");
                        return true;
                    }
                }
            }
            
            // Jump over small gaps if target is on the other side
            if (isEdgeAhead && targetTransform != null)
            {
                // Determine if target is on the other side of the edge
                bool isFacingRight = transform.localScale.x > 0;
                float targetDirection = Mathf.Sign(targetTransform.position.x - transform.position.x);
                bool targetInJumpDirection = (isFacingRight && targetDirection > 0) || 
                                           (!isFacingRight && targetDirection < 0);
                
                if (targetInJumpDirection)
                {
                    // Check if gap is small enough to jump
                    JumpController jumper = GetComponent<JumpController>();
                    if (jumper != null && jumper.CanJumpAcross(isFacingRight))
                    {
                        Debug.Log("Enemy decides to jump across gap to reach target");
                        return true;
                    }
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// Called when navigation is determining if it should climb
        /// Implements INavigationBehavior
        /// </summary>
        public virtual bool ShouldClimb(bool isLadderDetected, bool isTargetAbove, bool isObstacleAhead)
        {
            // Don't climb during actions
            if (isPerformingAction)
                return false;
            
            // Must have a ladder in range
            if (!isLadderDetected)
                return false;
            
            // Must have a target
            if (targetTransform == null)
                return false;
            
            // Determine if target requires climbing
            Vector2 toTarget = targetTransform.position - transform.position;
            
            // If target is significantly above us, climb
            if (toTarget.y > 1.5f)
            {
                Debug.Log("Enemy decides to climb to reach target above");
                return preferLadders;
            }
            
            // If there's an obstacle we can't jump over, try ladder
            if (isObstacleAhead && !ShouldJump(true, false, isTargetAbove))
            {
                bool isFacingRight = transform.localScale.x > 0;
                float obstacleDir = isFacingRight ? 1 : -1;
                float targetDir = Mathf.Sign(toTarget.x);
                
                // If target is behind obstacle, use ladder
                if (Mathf.Sign(targetDir) == obstacleDir)
                {
                    Debug.Log("Enemy decides to climb to navigate around obstacle");
                    return preferLadders;
                }
            }
            
            // Check if target is on a platform above that's not directly reachable
            LayerMask obstacleLayer = navigationController.ObstacleLayer;
            RaycastHit2D hit = Physics2D.Linecast(transform.position, targetTransform.position, obstacleLayer);
            if (hit.collider != null && hit.collider.transform != targetTransform)
            {
                Debug.Log("Enemy decides to climb because direct path to target is blocked");
                return preferLadders;
            }
            
            return false;
        }
        
        /// <summary>
        /// Called when an obstacle blocks the path
        /// Implements INavigationBehavior
        /// </summary>
        public virtual void OnPathBlocked(Vector2 obstaclePosition)
        {
            // Don't change strategy during actions
            if (isPerformingAction)
                return;
                
            // Check if enough time has passed since last direction change
            if (Time.time - lastDirectionChangeTime < DIRECTION_CHANGE_COOLDOWN)
                return;
                
            // Get current facing direction
            bool isFacingRight = transform.localScale.x > 0;
            
            if (targetTransform == null)
                return;
            
            // Try to be smart about navigation - check if we should flank
            Vector2 toTarget = targetTransform.position - transform.position;
            float targetDir = Mathf.Sign(toTarget.x);
            bool isTargetBehindObstacle = (isFacingRight && targetDir > 0) || 
                                          (!isFacingRight && targetDir < 0);
            
            // If the target is behind the obstacle and we can't jump over it
            if (isTargetBehindObstacle && !ShouldJump(true, false, toTarget.y > 1.0f))
            {
                // Check for ladder first
                if (ShouldClimb(false, toTarget.y > 1.0f, true))
                {
                    // Let the navigation controller handle climbing
                    return;
                }
                
                // Try to navigate around - first check if we're flipped
                if ((isFacingRight && targetTransform.position.x < transform.position.x) ||
                   (!isFacingRight && targetTransform.position.x > transform.position.x))
                {
                    // We're facing away from target, so flip to face target
                    FlipFacing();
                    lastDirectionChangeTime = Time.time;
                    Debug.Log("Enemy flips to face target behind obstacle");
                }
                else
                {
                    // We're facing toward target but blocked - try to find a way around
                    FlipFacing();
                    lastDirectionChangeTime = Time.time;
                    Debug.Log("Enemy flips to try flanking around obstacle");
                }
            }
            else
            {
                // Just flip and try another direction
                FlipFacing();
                lastDirectionChangeTime = Time.time;
                Debug.Log("Enemy changes direction due to blocked path");
            }
        }
        
        /// <summary>
        /// Called when reaching a navigation destination
        /// Implements INavigationBehavior
        /// </summary>
        public virtual void OnReachedDestination(Vector2 position)
        {
            // When we reach a waypoint destination, check if target is in range for attack
            if (targetTransform != null)
            {
                float dist = Vector2.Distance(transform.position, targetTransform.position);
                
                // If close enough to attack, start attack sequence
                if (dist < attackRange)
                {
                    // Make sure we're facing the target
                    FaceTarget();
                    
                    // Begin attack if not already attacking
                    if (!isPerformingAction && enemyActions != null)
                    {
                        // If the enemy implements IEnemyActions, it can handle attack logic
                        // Most enemies would trigger an attack animation here
                        isPerformingAction = true;
                        
                        // Pause navigation during attack
                        if (navigationController != null)
                        {
                            navigationController.PauseNavigation(1.0f);
                        }
                        
                        // Call your attack method (implementation depends on your enemy)
                        // For example: enemyActions.StartAttack();
                        
                        // Reset action flag after delay
                        Invoke("EndAction", 1.0f);
                    }
                }
            }
        }
        
        /// <summary>
        /// Turn to face target (usually player)
        /// </summary>
        protected virtual void FaceTarget()
        {
            if (targetTransform == null) return;
            
            float direction = transform.position.x > targetTransform.position.x ? -1 : 1;
            transform.localScale = new Vector3(direction * Mathf.Abs(transform.localScale.x), 
                                              transform.localScale.y, 
                                              transform.localScale.z);
        }
        
        /// <summary>
        /// Flip the facing direction
        /// </summary>
        protected virtual void FlipFacing()
        {
            Vector3 scale = transform.localScale;
            scale.x *= -1;
            transform.localScale = scale;
            
            Debug.Log($"Enemy flipped to face {(scale.x > 0 ? "right" : "left")}");
        }
        
        /// <summary>
        /// End the current action
        /// </summary>
        protected virtual void EndAction()
        {
            isPerformingAction = false;
        }
        
        /// <summary>
        /// Set whether the enemy is performing an action (to prevent movement)
        /// </summary>
        public virtual void SetPerformingAction(bool isPerforming)
        {
            isPerformingAction = isPerforming;
            
            // When starting/stopping an action, update navigation
            if (navigationController != null)
            {
                if (isPerforming)
                {
                    navigationController.PauseNavigation(1.0f);
                }
                else if (enemyAggro != null && enemyAggro.IsAggroed)
                {
                    navigationController.EnableNavigation(true);
                }
            }
        }
    }
}