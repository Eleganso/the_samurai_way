using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(SpriteColorSwapper))]
public class EnemyColorRandomizer : MonoBehaviour
{
    [System.Serializable]
    public class ColorVariantGroup
    {
        public string name; // E.g., "Hair Colors", "Armor Colors"
        public Color originalColor;
        public List<Color> possibleColors = new List<Color>();
    }

    [SerializeField] private List<ColorVariantGroup> colorVariantGroups = new List<ColorVariantGroup>();
    [SerializeField] private bool randomizeOnAwake = true;
    [SerializeField] private int randomSeed = -1; // -1 for truly random, any other value for deterministic

    private SpriteColorSwapper colorSwapper;

    private void Awake()
    {
        colorSwapper = GetComponent<SpriteColorSwapper>();
        
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

        // Clear existing swaps
        colorSwapper.ClearColorSwaps();
        
        // Add randomized swaps (up to 10 due to shader limitations)
        int swapsAdded = 0;
        foreach (var group in colorVariantGroups)
        {
            if (group.possibleColors.Count > 0 && swapsAdded < 10)
            {
                // Pick a random color from the group
                int randomIndex = Random.Range(0, group.possibleColors.Count);
                Color randomColor = group.possibleColors[randomIndex];
                
                // Add the swap to the color swapper
                colorSwapper.AddColorSwap(
                    group.name, 
                    group.originalColor, 
                    randomColor
                );
                
                swapsAdded++;
            }
        }
    }

    // Utility method to get a specific variant
    public void SetSpecificVariant(string groupName, int variantIndex)
    {
        foreach (var group in colorVariantGroups)
        {
            if (group.name == groupName && variantIndex >= 0 && variantIndex < group.possibleColors.Count)
            {
                colorSwapper.AddColorSwap(
                    group.name,
                    group.originalColor,
                    group.possibleColors[variantIndex]
                );
                break;
            }
        }
    }
}