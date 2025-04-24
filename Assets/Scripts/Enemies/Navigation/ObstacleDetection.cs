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
    if (groundCheckTransform == null) return false;
    
    // APPROACH 1: Larger detection circle
    Collider2D[] colliders = Physics2D.OverlapCircleAll(
        groundCheckTransform.position, 
        1.5f,  // Much larger radius to ensure ground detection
        groundLayer
    );
    
    foreach (Collider2D col in colliders)
    {
        // Skip if it's the enemy itself or a trigger
        if (col.transform == enemyTransform || 
            col.transform.IsChildOf(enemyTransform) || 
            col.isTrigger)
            continue;
        
        // Ground detected!
        Debug.DrawLine(groundCheckTransform.position, col.transform.position, Color.green);
        return true;
    }
    
    // APPROACH 2: Multiple downward raycasts with offset
    float[] horizontalOffsets = new float[] { -1.0f, -0.5f, 0f, 0.5f, 1.0f };
    
    foreach (float offset in horizontalOffsets)
    {
        Vector2 rayOrigin = (Vector2)groundCheckTransform.position + new Vector2(offset, 0f);
        RaycastHit2D hit = Physics2D.Raycast(
            rayOrigin,
            Vector2.down,
            1.2f,  // Check further down
            groundLayer
        );
        
        // Visual debugging
        Debug.DrawRay(rayOrigin, Vector2.down * 1.2f, hit.collider != null ? Color.green : Color.red);
        
        if (hit.collider != null && 
            hit.collider.transform != enemyTransform && 
            !hit.collider.transform.IsChildOf(enemyTransform))
        {
            return true;
        }
    }
    
    // No valid ground found
    return false;
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
        
        // Use OverlapCircleAll to get ALL colliders in front of us
        Collider2D[] colliders = Physics2D.OverlapCircleAll(
            origin + direction * (obstacleDetectionDistance / 2), 
            obstacleDetectionDistance / 2
        );
        
        bool foundObstacle = false;
        Collider2D obstacleCollider = null;
        
        // Check each collider to find valid obstacles
        foreach (Collider2D col in colliders)
        {
            // Skip if it's the enemy's own collider or a child collider
            if (col.transform == enemyTransform || col.transform.IsChildOf(enemyTransform))
            {
                continue;
            }
            
            // Skip if it's a trigger
            if (col.isTrigger)
            {
                continue;
            }
                
            // Check if it's on the obstacle layer
            if ((obstacleLayer.value & (1 << col.gameObject.layer)) != 0)
            {
                // This is a valid obstacle!
                Debug.Log($"Obstacle detected: {col.name} on layer {LayerMask.LayerToName(col.gameObject.layer)}");
                foundObstacle = true;
                obstacleCollider = col;
                break;
            }
        }
        
        // Alternative method with raycast for comparison
        RaycastHit2D hit = Physics2D.Raycast(origin, direction, obstacleDetectionDistance, obstacleLayer);
        if (hit.collider != null)
        {
            if (hit.collider.transform == enemyTransform || hit.collider.transform.IsChildOf(enemyTransform))
            {
                // Skip our own colliders
            }
            else
            {
              //  Debug.Log($"Raycast hit obstacle: {hit.collider.name} at distance {hit.distance}");
                // Use this as a fallback if the first method fails
                if (!foundObstacle)
                {
                    foundObstacle = true;
                    obstacleCollider = hit.collider;
                }
            }
        }
        
        // Visual debugging
        Color debugColor = foundObstacle ? Color.red : Color.green;
        Debug.DrawRay(origin, direction * obstacleDetectionDistance, debugColor);
        
        if (foundObstacle && obstacleCollider != null)
        {
            Debug.DrawLine(origin, obstacleCollider.transform.position, Color.yellow);
        }
        
        return foundObstacle;
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
        // Draw the large circle for ground detection
        Gizmos.color = IsGrounded() ? Color.green : Color.red;
        Gizmos.DrawWireSphere(groundCheckTransform.position, 1.5f);
        
        // Also visualize the raycast positions
        float[] horizontalOffsets = new float[] { -1.0f, -0.5f, 0f, 0.5f, 1.0f };
        
        foreach (float offset in horizontalOffsets)
        {
            Vector2 rayOrigin = (Vector2)groundCheckTransform.position + new Vector2(offset, 0f);
            
            // Only draw these additional visualizations in play mode
            if (Application.isPlaying)
            {
                RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.down, 1.2f, groundLayer);
                Gizmos.color = hit.collider != null ? Color.green : Color.yellow;
                Gizmos.DrawLine(rayOrigin, rayOrigin + Vector2.down * 1.2f);
            }
        }
    }
    
    // Rest of the method remains unchanged
    if (wallCheckTransform != null)
    {
        Vector2 direction = transform.localScale.x > 0 ? Vector2.right : Vector2.left;
        Vector2 origin = wallCheckTransform.position;
        Gizmos.color = IsObstacleAhead(transform.localScale.x > 0) ? Color.red : Color.green;
        Gizmos.DrawRay(origin, direction * obstacleDetectionDistance);
    }
    
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