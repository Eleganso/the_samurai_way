using UnityEngine;

[RequireComponent(typeof(SpriteColorSwapper))]
public class ColorSwapperTester : MonoBehaviour
{
    [SerializeField] private KeyCode applyKey = KeyCode.Space;
    [SerializeField] private Color originalColor1 = Color.white;
    [SerializeField] private Color targetColor1 = Color.red;
    [SerializeField] private Color originalColor2 = Color.white;
    [SerializeField] private Color targetColor2 = Color.blue;
    
    private SpriteColorSwapper colorSwapper;
    private bool swapApplied = false;
    
    void Start()
    {
        colorSwapper = GetComponent<SpriteColorSwapper>();
    }
    
    void Update()
    {
        if (Input.GetKeyDown(applyKey))
        {
            if (!swapApplied)
            {
                Debug.Log("Applying color swap");
                
                // Clear any existing swaps
                colorSwapper.ClearColorSwaps();
                
                // Add our test swaps
                colorSwapper.AddColorSwap("Test Swap 1", originalColor1, targetColor1);
                colorSwapper.AddColorSwap("Test Swap 2", originalColor2, targetColor2);
                
                // Apply the swaps
                colorSwapper.ApplyColorSwaps();
                
                swapApplied = true;
            }
            else
            {
                Debug.Log("Resetting colors");
                colorSwapper.ResetColors();
                swapApplied = false;
            }
        }
    }
}