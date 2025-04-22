using UnityEngine;

namespace Enemies.Navigation
{
    public interface INavigationBehavior
    {
        // Called when navigation is determining if it should jump
        bool ShouldJump(bool isObstacleAhead, bool isEdgeAhead, bool isTargetAbove);
        
        // Called when navigation is determining if it should climb
        bool ShouldClimb(bool isLadderDetected, bool isTargetAbove, bool isObstacleAhead);
        
        // Called when an obstacle is detected but can't be navigated
        void OnPathBlocked(Vector2 obstaclePosition);
        
        // Called when reaching a destination
        void OnReachedDestination(Vector2 position);
    }
}