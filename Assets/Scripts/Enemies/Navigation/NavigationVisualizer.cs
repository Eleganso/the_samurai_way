using UnityEngine;

namespace Enemies.Navigation
{
    // Attach this to enemies to visualize their navigation
    public class NavigationVisualizer : MonoBehaviour
    {
        [SerializeField] private Color groundCheckColor = Color.green;
        [SerializeField] private Color obstacleCheckColor = Color.red;
        [SerializeField] private Color edgeCheckColor = Color.yellow;
        [SerializeField] private Color ladderCheckColor = Color.blue;
        [SerializeField] private Color targetLineColor = Color.magenta;
        
        private EnemyNavigationController navigationController;
        private ObstacleDetection obstacleDetection;
        private JumpController jumpController;
        private ClimbController climbController;
        
        private void Start()
        {
            navigationController = GetComponent<EnemyNavigationController>();
            obstacleDetection = GetComponent<ObstacleDetection>();
            jumpController = GetComponent<JumpController>();
            climbController = GetComponent<ClimbController>();
        }
        
        private void OnDrawGizmos()
        {
            if (!Application.isPlaying) return;
            
            if (navigationController != null)
            {
                // Draw the current state
                #if UNITY_EDITOR
                string stateName = navigationController.enabled ? 
                    "Navigation Active" : "Navigation Disabled";
                UnityEditor.Handles.Label(transform.position + Vector3.up * 2, stateName);
                #endif
            }
        }
    }
}