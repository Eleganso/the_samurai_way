using UnityEngine;

namespace Enemies.Navigation
{
    // Navigation States Enum
    public enum NavigationState
    {
        Idle,        // Not moving
        Walking,     // Moving on ground
        Jumping,     // Performing a jump
        Climbing,    // Climbing a ladder
        Falling,     // In air, falling down
        PathPlanning // Temporarily paused to reconsider path
    }

    // Extension methods for NavigationState (optional)
    public static class NavigationStateExtensions
    {
        public static bool IsAirborne(this NavigationState state)
        {
            return state == NavigationState.Jumping || state == NavigationState.Falling;
        }
        
        public static bool CanChangeDirection(this NavigationState state)
        {
            return state == NavigationState.Walking || state == NavigationState.Idle;
        }
    }
}