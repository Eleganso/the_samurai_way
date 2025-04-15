using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(EnhancedEnemyColorRandomizer))]
public class ColorSchemeTester : MonoBehaviour
{
    [SerializeField] private KeyCode randomizeKey = KeyCode.R;
    [SerializeField] private KeyCode resetKey = KeyCode.T;
    
    [System.Serializable]
    public class SchemeMapping
    {
        public string groupName;
        public string schemeName;
        public KeyCode activationKey;
    }
    
    [SerializeField] private List<SchemeMapping> schemeMappings = new List<SchemeMapping>();
    
    private EnhancedEnemyColorRandomizer colorRandomizer;
    
    void Start()
    {
        colorRandomizer = GetComponent<EnhancedEnemyColorRandomizer>();
        
        // Add some default key mappings if none exist
        if (schemeMappings.Count == 0)
        {
            schemeMappings.Add(new SchemeMapping { 
                groupName = "HairColors", 
                schemeName = "Blonde", 
                activationKey = KeyCode.Alpha1 
            });
            
            schemeMappings.Add(new SchemeMapping { 
                groupName = "HairColors", 
                schemeName = "Red", 
                activationKey = KeyCode.Alpha2 
            });
            
            schemeMappings.Add(new SchemeMapping { 
                groupName = "HairColors", 
                schemeName = "Black", 
                activationKey = KeyCode.Alpha3 
            });
            
            schemeMappings.Add(new SchemeMapping { 
                groupName = "ArmorColors", 
                schemeName = "Silver", 
                activationKey = KeyCode.Alpha4 
            });
            
            schemeMappings.Add(new SchemeMapping { 
                groupName = "ArmorColors", 
                schemeName = "Gold", 
                activationKey = KeyCode.Alpha5 
            });
            
            schemeMappings.Add(new SchemeMapping { 
                groupName = "ArmorColors", 
                schemeName = "Bronze", 
                activationKey = KeyCode.Alpha6 
            });
        }
    }
    
    void Update()
    {
        // Randomize all colors
        if (Input.GetKeyDown(randomizeKey))
        {
            colorRandomizer.RandomizeColors();
            Debug.Log("Randomized all color schemes");
        }
        
        // Reset colors
        if (Input.GetKeyDown(resetKey))
        {
            SpriteColorSwapper swapper = GetComponent<SpriteColorSwapper>();
            if (swapper != null)
            {
                swapper.ClearColorSwaps();
                Debug.Log("Reset all colors to default");
            }
        }
        
        // Check for key presses to apply specific color schemes
        foreach (var mapping in schemeMappings)
        {
            if (Input.GetKeyDown(mapping.activationKey))
            {
                colorRandomizer.ApplyColorScheme(mapping.groupName, mapping.schemeName);
                Debug.Log($"Applied color scheme: {mapping.groupName} > {mapping.schemeName}");
            }
        }
    }
}