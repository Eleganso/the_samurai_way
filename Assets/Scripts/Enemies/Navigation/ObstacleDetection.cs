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
    // First, check if we're directly standing on actual ground
    if (groundCheckTransform != null)
    {
        // Cast a ray directly downward with a short distance
        RaycastHit2D hit = Physics2D.Raycast(
            groundCheckTransform.position,
            Vector2.down,
            0.3f,  // Short distance to check just below feet
            groundLayer
        );
        
        // If we hit something on the ground layer that's not our own collider
        if (hit.collider != null && 
            hit.collider.transform != enemyTransform && 
            !hit.collider.transform.IsChildOf(enemyTransform))
        {
//            Debug.Log($"Ground detected using raycast: {hit.collider.name} on layer {LayerMask.LayerToName(hit.collider.gameObject.layer)}");
            return true;
        }
    }
    
    // If first method fails, use the overlap approach but ONLY check for specific ground colliders
    Collider2D[] colliders = Physics2D.OverlapCircleAll(
        groundCheckTransform.position, 
        0.3f  // Smaller radius to avoid detecting too far
    );
    
    foreach (Collider2D col in colliders)
    {
        // Skip if it's the enemy itself or a trigger
        if (col.transform == enemyTransform || 
            col.transform.IsChildOf(enemyTransform) || 
            col.isTrigger)
            continue;
        
        // Only accept ground layer objects
        if ((groundLayer.value & (1 << col.gameObject.layer)) != 0)
        {
            Debug.DrawLine(groundCheckTransform.position, col.transform.position, Color.green);
            return true;
        }
    }
    
    // No valid ground found
    return false;
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
    
    // Cast a ray to detect obstacles
    RaycastHit2D hit = Physics2D.Raycast(origin, direction, obstacleDetectionDistance, obstacleLayer);
    
    // Make sure we're not detecting our own colliders
    if (hit.collider != null)
    {
        if (hit.collider.transform == enemyTransform || hit.collider.transform.IsChildOf(enemyTransform))
        {
            // Don't consider our own colliders as obstacles
            Debug.Log($"Detected own collider as obstacle: {hit.collider.name}, ignoring it");
            // Visual debugging
            Debug.DrawRay(origin, direction * obstacleDetectionDistance, Color.blue);
            return false;
        }
        
        // Valid obstacle detected
        Debug.Log($"Obstacle detected: {hit.collider.name} on layer {LayerMask.LayerToName(hit.collider.gameObject.layer)}");
        Debug.DrawRay(origin, direction * hit.distance, Color.red);
        return true;
    }
    
    // No obstacle
    Debug.DrawRay(origin, direction * obstacleDetectionDistance, Color.green);
    return false;
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
        
        // Skip our own colliders
        if (jumpHeightResults[i].collider != null && 
            (jumpHeightResults[i].collider.transform == enemyTransform || 
            jumpHeightResults[i].collider.transform.IsChildOf(enemyTransform)))
        {
            jumpHeightResults[i] = new RaycastHit2D();  // Clear the result
        }
        
        // Visual debugging
        Debug.DrawRay(rayOrigin, direction * obstacleDetectionDistance, 
            jumpHeightResults[i].collider != null ? Color.red : Color.green);
    }
    
    // Find the highest point that has no obstacle
    for (int i = jumpHeightOffsets.Length - 1; i >= 0; i--)
    {
        if (jumpHeightResults[i].collider == null)
        {
            Debug.Log($"Found clear jump height at offset {jumpHeightOffsets[i]}");
            return jumpHeightOffsets[i];
        }
    }
    
    Debug.Log("Cannot jump over this obstacle");
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