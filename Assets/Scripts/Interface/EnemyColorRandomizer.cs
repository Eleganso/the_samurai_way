using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(SpriteColorSwapper))]
public class EnhancedEnemyColorRandomizer : MonoBehaviour
{
    [System.Serializable]
    public class ColorReplacement
    {
        public Color originalColor;
        public Color targetColor;
    }

    [System.Serializable]
    public class ColorScheme
    {
        public string name;
        public List<ColorReplacement> colorReplacements = new List<ColorReplacement>();
    }

    [System.Serializable]
    public class ColorSchemeGroup
    {
        public string name; // E.g., "Hair Colors", "Armor Colors", etc.
        public List<ColorScheme> possibleSchemes = new List<ColorScheme>();
    }

    [SerializeField] private List<ColorSchemeGroup> colorSchemeGroups = new List<ColorSchemeGroup>();
    [SerializeField] private bool randomizeOnAwake = true;
    [SerializeField] private int randomSeed = -1; // -1 for truly random, any other value for deterministic

    private SpriteColorSwapper colorSwapper;

    void Start()
    {
        // Make sure we have a valid reference to the color swapper
        if (colorSwapper == null)
        {
            colorSwapper = GetComponent<SpriteColorSwapper>();
            
            if (colorSwapper == null)
            {
                Debug.LogError("SpriteColorSwapper component not found on this GameObject!", this);
                return;
            }
        }
        
        // Make sure it's initialized
        if (!colorSwapper.IsInitialized())
        {
            colorSwapper.Initialize();
        }
        
        if (randomizeOnAwake)
        {
            RandomizeColors();
        }
    }

    public void RandomizeColors()
    {
        // Initialize random seed if specified
        if (randomSeed != -1)
        {
            Random.InitState(randomSeed);
        }
        else
        {
            Random.InitState(System.DateTime.Now.Millisecond);
        }

        // Make sure we have a valid reference to the color swapper
        if (colorSwapper == null)
        {
            colorSwapper = GetComponent<SpriteColorSwapper>();
            
            if (colorSwapper == null)
            {
                Debug.LogError("SpriteColorSwapper component not found on this GameObject!", this);
                return;
            }
        }

        // Clear existing swaps
        colorSwapper.ClearColorSwaps();
        
        // Keep track of how many swaps we've added
        int totalSwapsAdded = 0;
        
        // For each color scheme group, pick a random scheme and apply all its replacements
        foreach (var group in colorSchemeGroups)
        {
            if (group.possibleSchemes.Count == 0)
                continue;
                
            // Select a random scheme from this group
            int randomSchemeIndex = Random.Range(0, group.possibleSchemes.Count);
            ColorScheme selectedScheme = group.possibleSchemes[randomSchemeIndex];
            
            // Apply all color replacements from this scheme
            foreach (var replacement in selectedScheme.colorReplacements)
            {
                // Make sure we don't exceed the shader's limit of 10 swaps
                if (totalSwapsAdded >= 10)
                {
                    Debug.LogWarning($"Maximum of 10 color swaps reached. Skipping remaining colors in {group.name}.");
                    break;
                }
                
                // Add the color swap
                colorSwapper.AddColorSwap(
                    $"{group.name}_{selectedScheme.name}_{totalSwapsAdded}", 
                    replacement.originalColor,
                    replacement.targetColor
                );
                
                totalSwapsAdded++;
            }
            
            // If we've hit the maximum, stop adding more schemes
            if (totalSwapsAdded >= 10)
                break;
        }
    }

    // Apply a specific color scheme by name
    public void ApplyColorScheme(string groupName, string schemeName)
    {
        foreach (var group in colorSchemeGroups)
        {
            if (group.name == groupName)
            {
                foreach (var scheme in group.possibleSchemes)
                {
                    if (scheme.name == schemeName)
                    {
                        // Apply this specific scheme
                        ApplyScheme(group.name, scheme);
                        return;
                    }
                }
            }
        }
        
        Debug.LogWarning($"Color scheme '{schemeName}' not found in group '{groupName}'");
    }
    
    // Apply a specific color scheme by index
    public void ApplyColorScheme(string groupName, int schemeIndex)
    {
        foreach (var group in colorSchemeGroups)
        {
            if (group.name == groupName && 
                schemeIndex >= 0 && 
                schemeIndex < group.possibleSchemes.Count)
            {
                ApplyScheme(group.name, group.possibleSchemes[schemeIndex]);
                return;
            }
        }
        
        Debug.LogWarning($"Color scheme index {schemeIndex} not found in group '{groupName}'");
    }
    
    // Apply only the specified color scheme, preserving other colors
    private void ApplyScheme(string groupName, ColorScheme scheme)
    {
        // Remove any previous color swaps for this group
        List<string> swapsToKeep = new List<string>();
        
        // Get all current swaps
        for (int i = 0; i < colorSwapper.GetSwapCount(); i++)
        {
            var swap = colorSwapper.GetSwap(i);
            // Only keep swaps that don't start with this group name
            if (!swap.name.StartsWith(groupName + "_"))
            {
                swapsToKeep.Add(swap.name);
            }
        }
        
        // Clear all swaps
        colorSwapper.ClearColorSwaps();
        
        // Re-add the swaps we want to keep
        int totalSwaps = 0;
        
        // Add our new scheme's swaps
        foreach (var replacement in scheme.colorReplacements)
        {
            if (totalSwaps >= 10)
            {
                Debug.LogWarning("Maximum of 10 color swaps reached");
                break;
            }
            
            colorSwapper.AddColorSwap(
                $"{groupName}_{scheme.name}_{totalSwaps}",
                replacement.originalColor,
                replacement.targetColor
            );
            
            totalSwaps++;
        }
    }

#if UNITY_EDITOR
    // Helper method for setting up color schemes in the editor
    [ContextMenu("Add Example Color Schemes")]
    private void AddExampleColorSchemes()
    {
        // Clear existing schemes
        colorSchemeGroups.Clear();
        
        // Add example hair colors (with light and dark variants)
        ColorSchemeGroup hairColors = new ColorSchemeGroup { name = "HairColors" };
        
        // Blonde hair
        ColorScheme blondeHair = new ColorScheme { name = "Blonde" };
        blondeHair.colorReplacements.Add(new ColorReplacement { 
            originalColor = new Color(1f, 0.9f, 0.5f), 
            targetColor = new Color(1f, 0.9f, 0.5f) 
        });
        blondeHair.colorReplacements.Add(new ColorReplacement { 
            originalColor = new Color(0.8f, 0.7f, 0.2f), 
            targetColor = new Color(0.8f, 0.7f, 0.2f) 
        });
        
        // Red hair
        ColorScheme redHair = new ColorScheme { name = "Red" };
        redHair.colorReplacements.Add(new ColorReplacement { 
            originalColor = new Color(1f, 0.9f, 0.5f), 
            targetColor = new Color(1f, 0.5f, 0.5f) 
        });
        redHair.colorReplacements.Add(new ColorReplacement { 
            originalColor = new Color(0.8f, 0.7f, 0.2f), 
            targetColor = new Color(0.8f, 0.3f, 0.2f) 
        });
        
        // Black hair
        ColorScheme blackHair = new ColorScheme { name = "Black" };
        blackHair.colorReplacements.Add(new ColorReplacement { 
            originalColor = new Color(1f, 0.9f, 0.5f), 
            targetColor = new Color(0.3f, 0.3f, 0.3f) 
        });
        blackHair.colorReplacements.Add(new ColorReplacement { 
            originalColor = new Color(0.8f, 0.7f, 0.2f), 
            targetColor = new Color(0.1f, 0.1f, 0.1f) 
        });
        
        hairColors.possibleSchemes.Add(blondeHair);
        hairColors.possibleSchemes.Add(redHair);
        hairColors.possibleSchemes.Add(blackHair);
        
        // Add example armor colors (with 3 variants: metal, trim, and accent)
        ColorSchemeGroup armorColors = new ColorSchemeGroup { name = "ArmorColors" };
        
        // Silver armor
        ColorScheme silverArmor = new ColorScheme { name = "Silver" };
        silverArmor.colorReplacements.Add(new ColorReplacement { 
            originalColor = new Color(0.8f, 0.8f, 0.8f), 
            targetColor = new Color(0.8f, 0.8f, 0.8f) 
        });
        silverArmor.colorReplacements.Add(new ColorReplacement { 
            originalColor = new Color(0.6f, 0.6f, 0.6f), 
            targetColor = new Color(0.6f, 0.6f, 0.6f) 
        });
        silverArmor.colorReplacements.Add(new ColorReplacement { 
            originalColor = new Color(0.4f, 0.4f, 0.4f), 
            targetColor = new Color(0.4f, 0.4f, 0.4f) 
        });
        
        // Gold armor
        ColorScheme goldArmor = new ColorScheme { name = "Gold" };
        goldArmor.colorReplacements.Add(new ColorReplacement { 
            originalColor = new Color(0.8f, 0.8f, 0.8f), 
            targetColor = new Color(1.0f, 0.84f, 0.0f) 
        });
        goldArmor.colorReplacements.Add(new ColorReplacement { 
            originalColor = new Color(0.6f, 0.6f, 0.6f), 
            targetColor = new Color(0.85f, 0.65f, 0.0f) 
        });
        goldArmor.colorReplacements.Add(new ColorReplacement { 
            originalColor = new Color(0.4f, 0.4f, 0.4f), 
            targetColor = new Color(0.6f, 0.5f, 0.0f) 
        });
        
        // Bronze armor
        ColorScheme bronzeArmor = new ColorScheme { name = "Bronze" };
        bronzeArmor.colorReplacements.Add(new ColorReplacement { 
            originalColor = new Color(0.8f, 0.8f, 0.8f), 
            targetColor = new Color(0.8f, 0.5f, 0.2f) 
        });
        bronzeArmor.colorReplacements.Add(new ColorReplacement { 
            originalColor = new Color(0.6f, 0.6f, 0.6f), 
            targetColor = new Color(0.65f, 0.4f, 0.15f) 
        });
        bronzeArmor.colorReplacements.Add(new ColorReplacement { 
            originalColor = new Color(0.4f, 0.4f, 0.4f), 
            targetColor = new Color(0.5f, 0.3f, 0.1f) 
        });
        
        armorColors.possibleSchemes.Add(silverArmor);
        armorColors.possibleSchemes.Add(goldArmor);
        armorColors.possibleSchemes.Add(bronzeArmor);
        
        // Add the groups to our list
        colorSchemeGroups.Add(hairColors);
        colorSchemeGroups.Add(armorColors);
        
        Debug.Log("Added example color schemes. Adjust the original colors to match your sprite's actual colors.");
    }
#endif
}