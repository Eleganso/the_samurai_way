using UnityEngine;
using System.Collections.Generic;

public class SpriteColorSwapper : MonoBehaviour
{
    [System.Serializable]
    public class ColorSwapPair
    {
        public string name; // For editor clarity (e.g., "Hair", "Armor")
        public Color originalColor = Color.white;
        public Color targetColor = Color.white;
    }

    [Header("Settings")]
    [SerializeField] private SpriteRenderer targetRenderer;
    [SerializeField] private Material colorSwapMaterial;
    [SerializeField] private bool applyOnStart = true;
    [SerializeField] private List<ColorSwapPair> colorSwaps = new List<ColorSwapPair>();
    [Range(0.01f, 1.0f)]
    [SerializeField] private float colorThreshold = 0.1f;
    
    private Material instancedMaterial;
    private bool initialized = false;

    void Start()
    {
        if (applyOnStart)
        {
            Initialize();
            ApplyColorSwaps();
        }
    }

    void OnDestroy()
    {
        // Clean up instantiated resources
        if (instancedMaterial != null)
        {
            if (Application.isPlaying)
            {
                Destroy(instancedMaterial);
            }
            else
            {
                DestroyImmediate(instancedMaterial);
            }
        }
    }

    public bool IsInitialized()
    {
        return initialized;
    }

    public void Initialize()
    {
        if (initialized)
            return;

        // Find sprite renderer if not assigned
        if (targetRenderer == null)
            targetRenderer = GetComponent<SpriteRenderer>();
            
        if (targetRenderer == null)
        {
            Debug.LogError("SpriteColorSwapper: No SpriteRenderer found!", this);
            return;
        }

        // Load the shader if material not assigned
        if (colorSwapMaterial == null)
        {
            Shader shader = Shader.Find("Custom/ColorSwapShader");
            if (shader == null)
            {
                Debug.LogError("SpriteColorSwapper: Color swap shader not found! Make sure it's in your project.", this);
                return;
            }
            
            colorSwapMaterial = new Material(shader);
        }

        // Create instanced material
        instancedMaterial = new Material(colorSwapMaterial);
        
        // Set material on renderer
        targetRenderer.material = instancedMaterial;
        
        initialized = true;
    }

    public void ApplyColorSwaps()
    {
        if (!initialized)
            Initialize();
            
        if (!initialized)
            return;
        
        // Set the color threshold
        instancedMaterial.SetFloat("_ColorThreshold", colorThreshold);
        
        // Apply each color swap (up to 10 supported by shader)
        for (int i = 0; i < Mathf.Min(colorSwaps.Count, 10); i++)
        {
            string colorToReplaceProperty = "_ColorToReplace" + (i + 1);
            string newColorProperty = "_NewColor" + (i + 1);
            
            instancedMaterial.SetColor(colorToReplaceProperty, colorSwaps[i].originalColor);
            instancedMaterial.SetColor(newColorProperty, colorSwaps[i].targetColor);
        }
        
        // Clear any unused slots
        for (int i = colorSwaps.Count; i < 10; i++)
        {
            string colorToReplaceProperty = "_ColorToReplace" + (i + 1);
            string newColorProperty = "_NewColor" + (i + 1);
            
            instancedMaterial.SetColor(colorToReplaceProperty, Color.black);
            instancedMaterial.SetColor(newColorProperty, Color.black);
        }
    }

    // Reset to original appearance
    public void ResetColors()
    {
        if (targetRenderer != null)
        {
            targetRenderer.material = null; // Reset to default sprite material
        }
    }

    // Utility method to add a new color swap pair
    public void AddColorSwap(string name, Color originalColor, Color targetColor)
    {
        // Only add if we have fewer than 10 swaps (shader limitation)
        if (colorSwaps.Count < 10)
        {
            ColorSwapPair newSwap = new ColorSwapPair
            {
                name = name,
                originalColor = originalColor,
                targetColor = targetColor
            };
            
            colorSwaps.Add(newSwap);
            
            if (initialized)
            {
                ApplyColorSwaps();
            }
        }
        else
        {
            Debug.LogWarning("SpriteColorSwapper: Maximum of 10 color swaps reached. Cannot add more.");
        }
    }

    // Get the number of current color swaps
    public int GetSwapCount()
    {
        return colorSwaps.Count;
    }
    
    // Get a specific color swap
    public ColorSwapPair GetSwap(int index)
    {
        if (index >= 0 && index < colorSwaps.Count)
            return colorSwaps[index];
            
        return null;
    }
    
    // Utility method to clear all color swaps
    public void ClearColorSwaps()
    {
        colorSwaps.Clear();
        
        if (initialized)
        {
            ApplyColorSwaps();
        }
    }
}