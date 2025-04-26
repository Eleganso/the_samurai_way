using UnityEngine;
using System.Collections.Generic;

namespace Enemies.Navigation
{
    /// <summary>
    /// Centralized manager for layer avoidance settings that can be applied to all enemies.
    /// Add this to a game manager or scene controller.
    /// </summary>
    public class LayerAvoidanceManager : MonoBehaviour
    {
        [System.Serializable]
        public class LayerSettings
        {
            public string layerName;
            public bool shouldAvoid = true;
            public Color debugColor = Color.red;
        }
        
        [Header("Layer Avoidance Settings")]
        [SerializeField] private bool enableLayerAvoidance = true;
        [SerializeField] private List<LayerSettings> avoidedLayers = new List<LayerSettings>();
        
        [Header("Application Settings")]
        [SerializeField] private bool applyToAllEnemiesOnStart = true;
        [SerializeField] private bool applyToNewEnemies = true;
        [SerializeField] private string enemyTag = "Enemy";
        
        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = true;
        [SerializeField] private bool visualizeAvoidedAreas = true;
        
        // Cached layer mask for avoided layers
        private LayerMask avoidedLayersMask;
        
        private void Awake()
        {
            // Build the avoided layers mask from the settings
            BuildAvoidedLayersMask();
        }
        
        private void Start()
        {
            if (applyToAllEnemiesOnStart && enableLayerAvoidance)
            {
                ApplyToAllEnemies();
            }
        }
        
        private void BuildAvoidedLayersMask()
        {
            avoidedLayersMask = 0;
            
            foreach (LayerSettings layer in avoidedLayers)
            {
                if (layer.shouldAvoid)
                {
                    int layerIndex = LayerMask.NameToLayer(layer.layerName);
                    if (layerIndex != -1)
                    {
                        avoidedLayersMask |= (1 << layerIndex);
                    }
                    else
                    {
                        Debug.LogWarning($"Layer '{layer.layerName}' not found in project settings.");
                    }
                }
            }
            
            if (showDebugInfo)
            {
                Debug.Log($"Avoided layers: {LayerMaskToString(avoidedLayersMask)}");
            }
        }
        
        /// <summary>
        /// Apply avoidance settings to all enemies in the scene
        /// </summary>
        public void ApplyToAllEnemies()
        {
            if (!enableLayerAvoidance) return;
            
            // Find all enemies
            GameObject[] enemies;
            if (!string.IsNullOrEmpty(enemyTag))
            {
                enemies = GameObject.FindGameObjectsWithTag(enemyTag);
            }
            else
            {
                // Fallback to finding all EnemyNavigationController components
                EnemyNavigationController[] navControllers = FindObjectsOfType<EnemyNavigationController>();
                enemies = new GameObject[navControllers.Length];
                for (int i = 0; i < navControllers.Length; i++)
                {
                    enemies[i] = navControllers[i].gameObject;
                }
            }
            
            // Apply to each enemy
            foreach (GameObject enemy in enemies)
            {
                ApplyToEnemy(enemy);
            }
            
            if (showDebugInfo)
            {
                Debug.Log($"Applied layer avoidance settings to {enemies.Length} enemies");
            }
        }
        
        /// <summary>
        /// Apply avoidance settings to a specific enemy
        /// </summary>
        public void ApplyToEnemy(GameObject enemy)
        {
            if (!enableLayerAvoidance) return;
            
            // Apply to EnemyNavigationController
            EnemyNavigationController navController = enemy.GetComponent<EnemyNavigationController>();
            if (navController != null)
            {
                SetAvoidedLayersOnComponent(navController);
            }
            
            // Apply to ObstacleDetection
            ObstacleDetection obstacleDetection = enemy.GetComponent<ObstacleDetection>();
            if (obstacleDetection != null)
            {
                SetAvoidedLayersOnObstacleDetection(obstacleDetection);
            }
            
            // Apply to ImprovedPathPlanning if present
            ImprovedPathPlanning pathPlanning = enemy.GetComponent<ImprovedPathPlanning>();
            if (pathPlanning != null)
            {
                SetAvoidedLayersOnPathPlanning(pathPlanning);
            }
        }
        
        /// <summary>
        /// Set avoided layers on EnemyNavigationController using reflection
        /// </summary>
        private void SetAvoidedLayersOnComponent(EnemyNavigationController controller)
        {
            var field = controller.GetType().GetField("avoidedLayers", 
                System.Reflection.BindingFlags.NonPublic | 
                System.Reflection.BindingFlags.Public | 
                System.Reflection.BindingFlags.Instance);
                
            if (field != null)
            {
                field.SetValue(controller, avoidedLayersMask);
                
                if (showDebugInfo)
                {
                    Debug.Log($"Set avoided layers on {controller.name}");
                }
            }
            else if (showDebugInfo)
            {
                Debug.LogWarning($"Could not find avoidedLayers field on {controller.name}");
            }
        }
        
        /// <summary>
        /// Set avoided layers on ObstacleDetection using reflection
        /// </summary>
        private void SetAvoidedLayersOnObstacleDetection(ObstacleDetection detector)
        {
            var field = detector.GetType().GetField("avoidedLayers", 
                System.Reflection.BindingFlags.NonPublic | 
                System.Reflection.BindingFlags.Public | 
                System.Reflection.BindingFlags.Instance);
                
            if (field != null)
            {
                field.SetValue(detector, avoidedLayersMask);
            }
        }
        
        /// <summary>
        /// Set avoided layers on ImprovedPathPlanning using reflection
        /// </summary>
        private void SetAvoidedLayersOnPathPlanning(ImprovedPathPlanning pathPlanning)
        {
            var field = pathPlanning.GetType().GetField("avoidedLayers", 
                System.Reflection.BindingFlags.NonPublic | 
                System.Reflection.BindingFlags.Public | 
                System.Reflection.BindingFlags.Instance);
                
            if (field != null)
            {
                field.SetValue(pathPlanning, avoidedLayersMask);
            }
        }
        
        /// <summary>
        /// Toggle layer avoidance on or off
        /// </summary>
        public void SetLayerAvoidanceEnabled(bool enabled)
        {
            enableLayerAvoidance = enabled;
            
            if (enabled)
            {
                // Re-apply to all enemies when enabling
                ApplyToAllEnemies();
            }
            else
            {
                // Clear layer avoidance when disabling
                ClearAllEnemyAvoidance();
            }
        }
        
        /// <summary>
        /// Clear layer avoidance from all enemies
        /// </summary>
        private void ClearAllEnemyAvoidance()
        {
            // Set an empty layer mask on all enemies
            LayerMask emptyMask = 0;
            
            // Find all EnemyNavigationController components
            EnemyNavigationController[] navControllers = FindObjectsOfType<EnemyNavigationController>();
            foreach (EnemyNavigationController controller in navControllers)
            {
                var field = controller.GetType().GetField("avoidedLayers", 
                    System.Reflection.BindingFlags.NonPublic | 
                    System.Reflection.BindingFlags.Public | 
                    System.Reflection.BindingFlags.Instance);
                    
                if (field != null)
                {
                    field.SetValue(controller, emptyMask);
                }
            }
        }
        
        /// <summary>
        /// Convert LayerMask to string for debugging
        /// </summary>
        private string LayerMaskToString(LayerMask mask)
        {
            var names = new System.Text.StringBuilder();
            for (int i = 0; i < 32; i++)
            {
                if (((1 << i) & mask.value) != 0)
                {
                    if (names.Length > 0) names.Append(", ");
                    names.Append(LayerMask.LayerToName(i));
                }
            }
            return names.Length > 0 ? names.ToString() + $" ({mask.value})" : "Nothing";
        }
        
        /// <summary>
        /// Draw visualization of avoided areas in the scene view
        /// </summary>
        private void OnDrawGizmos()
        {
            if (!visualizeAvoidedAreas || !Application.isPlaying) return;
            
            // Find all colliders on avoided layers
            foreach (LayerSettings layerSetting in avoidedLayers)
            {
                if (!layerSetting.shouldAvoid) continue;
                
                int layerIndex = LayerMask.NameToLayer(layerSetting.layerName);
                if (layerIndex == -1) continue;
                
                LayerMask layerMask = 1 << layerIndex;
                
                // Only get colliders that are visible in the scene view
                Collider2D[] colliders = Physics2D.OverlapAreaAll(
                    Camera.main.ViewportToWorldPoint(new Vector2(0, 0)),
                    Camera.main.ViewportToWorldPoint(new Vector2(1, 1)),
                    layerMask
                );
                
                Color drawColor = layerSetting.debugColor;
                drawColor.a = 0.3f; // Make it semi-transparent
                Gizmos.color = drawColor;
                
                foreach (Collider2D collider in colliders)
                {
                    // Draw different shapes based on collider type
                    if (collider is BoxCollider2D boxCollider)
                    {
                        Vector3 size = boxCollider.size;
                        Gizmos.DrawCube(boxCollider.bounds.center, size);
                        Gizmos.DrawWireCube(boxCollider.bounds.center, size);
                    }
                    else if (collider is CircleCollider2D circleCollider)
                    {
                        Gizmos.DrawSphere(circleCollider.bounds.center, circleCollider.radius);
                        Gizmos.DrawWireSphere(circleCollider.bounds.center, circleCollider.radius);
                    }
                    else
                    {
                        // For other collider types, just draw the bounds
                        Gizmos.DrawCube(collider.bounds.center, collider.bounds.size);
                    }
                    
                    #if UNITY_EDITOR
                    UnityEditor.Handles.color = drawColor;
                    UnityEditor.Handles.Label(collider.bounds.center, $"Avoided: {layerSetting.layerName}");
                    #endif
                }
            }
        }
    }
}