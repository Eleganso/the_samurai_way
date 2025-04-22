using UnityEngine;

public class ObstacleDetection : MonoBehaviour
{
    private Transform enemyTransform;
    private Transform groundCheckTransform;
    private Transform wallCheckTransform;
    
    private float obstacleDetectionDistance;
    private float edgeDetectionDistance;
    private LayerMask groundLayer;
    private LayerMask obstacleLayer;

    // Jump obstacle data
    private readonly RaycastHit2D[] jumpHeightResults = new RaycastHit2D[5];
    private readonly float[] jumpHeightOffsets = { 0.5f, 1.0f, 1.5f, 2.0f, 2.5f };

    public void Initialize(Transform enemy, Transform groundCheck, Transform wallCheck, 
                          float obstacleDistance, float edgeDistance, 
                          LayerMask ground, LayerMask obstacle)
    {
        enemyTransform = enemy;
        groundCheckTransform = groundCheck;
        wallCheckTransform = wallCheck;
        obstacleDetectionDistance = obstacleDistance;
        edgeDetectionDistance = edgeDistance;
        groundLayer = ground;
        obstacleLayer = obstacle;
    }

    public bool IsGrounded()
{
    // Override for testing - uncomment to force grounded state
    // return true;

    // Validate parameters
    if (groundLayer.value == 0)
    {
        Debug.LogError("Ground layer mask is zero! Please set the correct layer in the inspector.");
        return false;
    }

    if (groundCheckTransform == null)
    {
        // Calculate a position below the entity's feet
        Vector2 feetPosition = (Vector2)enemyTransform.position + new Vector2(0, -0.75f);
        bool grounded = Physics2D.OverlapCircle(feetPosition, 0.5f, groundLayer);
        
        Debug.DrawRay(feetPosition, Vector2.down * 0.2f, grounded ? Color.green : Color.red);
        return grounded;
    }
    
    // APPROACH 1: Try a very wide radius to ensure detection
    float radius = 0.8f; // Increased radius for better detection
    bool isGroundedWithTransform = Physics2D.OverlapCircle(groundCheckTransform.position, radius, groundLayer);
    
    // APPROACH 2: If that fails, try multiple detection points spread horizontally
    if (!isGroundedWithTransform)
    {
        // Try multiple points along a horizontal line
        for (float offset = -0.5f; offset <= 0.5f; offset += 0.25f)
        {
            Vector2 checkPos = (Vector2)groundCheckTransform.position + new Vector2(offset, 0);
            isGroundedWithTransform = Physics2D.OverlapCircle(checkPos, 0.3f, groundLayer);
            
            if (isGroundedWithTransform)
            {
                Debug.DrawRay(checkPos, Vector2.down * 0.3f, Color.green);
                break;
            }
        }
    }
    
    // APPROACH 3: Try ignoring layers and check all colliders below
    if (!isGroundedWithTransform)
    {
        // Cast against everything, then check if what we hit is ground
        RaycastHit2D hit = Physics2D.Raycast(
            groundCheckTransform.position, 
            Vector2.down, 
            0.75f
        );
        
        if (hit.collider != null)
        {
            // Check if the hit object is on the ground layer
            if ((groundLayer.value & (1 << hit.collider.gameObject.layer)) != 0)
            {
                isGroundedWithTransform = true;
                Debug.Log($"Ground detected using raycast: {hit.collider.name} on layer {LayerMask.LayerToName(hit.collider.gameObject.layer)}");
            }
            else
            {
                // Log what we hit, even if it's not ground
                Debug.Log($"Hit {hit.collider.name} on layer {LayerMask.LayerToName(hit.collider.gameObject.layer)} but it's not on Ground layer");
            }
        }
    }
    
    // Visual debugging
    Color debugColor = isGroundedWithTransform ? Color.green : Color.red;
    Debug.DrawRay(groundCheckTransform.position, Vector2.down * 0.5f, debugColor);
    
    if (!isGroundedWithTransform && Time.frameCount % 60 == 0) // Limit log frequency
    {
        // Get all nearby colliders for debugging
        Collider2D[] colliders = Physics2D.OverlapCircleAll(groundCheckTransform.position, 1.0f);
        string nearbyColliders = "";
        foreach (var collider in colliders)
        {
            nearbyColliders += $"{collider.name} (Layer: {LayerMask.LayerToName(collider.gameObject.layer)}), ";
        }
        
        Debug.Log($"Not grounded at GroundCheck position: {groundCheckTransform.position}, " +
                  $"Looking for Layer: {LayerMaskToLayerName(groundLayer)}. " +
                  $"Nearby colliders: {nearbyColliders}");
    }
    
    return isGroundedWithTransform;
}
    
    // Helper method to convert layer mask to readable layer name
private string LayerMaskToLayerName(LayerMask layerMask)
{
    // Find all set bits which represent layers
    string result = "";
    for (int i = 0; i < 32; i++)
    {
        if (((1 << i) & layerMask.value) != 0)
        {
            result += (result.Length > 0 ? ", " : "") + LayerMask.LayerToName(i);
        }
    }
    return string.IsNullOrEmpty(result) ? "None" : result;
}

    public bool IsObstacleAhead(bool isFacingRight)
    {
        Vector2 direction = isFacingRight ? Vector2.right : Vector2.left;
        Vector2 origin;
        
        if (wallCheckTransform != null)
        {
            // Use the WallCheck transform
            origin = wallCheckTransform.position;
        }
        else
        {
            // Fallback to a calculated position
            origin = (Vector2)enemyTransform.position + new Vector2(isFacingRight ? 0.25f : -0.25f, 0);
        }
        
        RaycastHit2D hit = Physics2D.Raycast(origin, direction, obstacleDetectionDistance, obstacleLayer);
        
        // Visual debugging
        Debug.DrawRay(origin, direction * obstacleDetectionDistance, hit.collider != null ? Color.red : Color.green);
        
        return hit.collider != null;
    }

    public bool IsEdgeAhead(bool isFacingRight)
    {
        if (groundCheckTransform == null) return false;
        
        Vector2 direction = isFacingRight ? Vector2.right : Vector2.left;
        Vector2 origin = groundCheckTransform.position;
        
        // Move the origin forward to check for an edge
        origin += direction * edgeDetectionDistance;
        
        // Cast down to see if there's ground below
        RaycastHit2D hit = Physics2D.Raycast(
            origin,
            Vector2.down,
            1.0f,
            groundLayer
        );
        
        // Visual debugging
        Debug.DrawRay(origin, Vector2.down * 1.0f, hit.collider == null ? Color.yellow : Color.green);
        
        return hit.collider == null;
    }

    public float GetObstacleHeight(bool isFacingRight)
    {
        Vector2 direction = isFacingRight ? Vector2.right : Vector2.left;
        Vector2 origin = wallCheckTransform != null ? 
                        (Vector2)wallCheckTransform.position : 
                        (Vector2)enemyTransform.position + new Vector2(0, 0.5f);
        
        // Cast rays at different heights to find the top of the obstacle
        for (int i = 0; i < jumpHeightOffsets.Length; i++)
        {
            Vector2 rayOrigin = origin + new Vector2(0, jumpHeightOffsets[i]);
            jumpHeightResults[i] = Physics2D.Raycast(
                rayOrigin,
                direction,
                obstacleDetectionDistance,
                obstacleLayer
            );
            
            // Visual debugging
            Debug.DrawRay(rayOrigin, direction * obstacleDetectionDistance, 
                jumpHeightResults[i].collider != null ? Color.red : Color.green);
        }
        
        // Find the highest point that has no obstacle
        for (int i = jumpHeightOffsets.Length - 1; i >= 0; i--)
        {
            if (jumpHeightResults[i].collider == null)
            {
                return jumpHeightOffsets[i];
            }
        }
        
        return 0; // Can't jump over this obstacle
    }

    public float GetEdgeDistance(bool isFacingRight)
    {
        if (groundCheckTransform == null) return 0;
        
        Vector2 direction = isFacingRight ? Vector2.right : Vector2.left;
        Vector2 origin = groundCheckTransform.position;
        
        // Cast multiple rays forward at different distances
        for (float dist = 0.5f; dist <= 5.0f; dist += 0.5f)
        {
            Vector2 checkPoint = origin + direction * dist;
            RaycastHit2D hit = Physics2D.Raycast(checkPoint, Vector2.down, 1.0f, groundLayer);
            
            // Visual debugging
            Debug.DrawRay(checkPoint, Vector2.down * 1.0f, hit.collider == null ? Color.yellow : Color.green);
            
            if (hit.collider == null)
            {
                // This is where the edge starts
                return dist;
            }
        }
        
        return 0; // No edge found within 5 units
    }

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying || enemyTransform == null) return;

        // Draw ground check
        if (groundCheckTransform != null)
        {
            Gizmos.color = IsGrounded() ? Color.green : Color.red;
            Gizmos.DrawWireSphere(groundCheckTransform.position, 0.2f);
        }
        
        // Draw obstacle detection
        if (wallCheckTransform != null)
        {
            Vector2 direction = transform.localScale.x > 0 ? Vector2.right : Vector2.left;
            Vector2 origin = wallCheckTransform.position;
            Gizmos.color = IsObstacleAhead(transform.localScale.x > 0) ? Color.red : Color.green;
            Gizmos.DrawRay(origin, direction * obstacleDetectionDistance);
        }
        
        // Draw edge detection
        if (groundCheckTransform != null)
        {
            Vector2 direction = transform.localScale.x > 0 ? Vector2.right : Vector2.left;
            Vector2 origin = groundCheckTransform.position;
            Vector2 edgeCheckPoint = origin + direction * edgeDetectionDistance;
            
            Gizmos.color = IsEdgeAhead(transform.localScale.x > 0) ? Color.yellow : Color.green;
            Gizmos.DrawLine(origin, edgeCheckPoint);
            Gizmos.DrawRay(edgeCheckPoint, Vector2.down);
        }
    }
}