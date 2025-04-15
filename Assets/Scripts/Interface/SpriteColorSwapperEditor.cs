using UnityEngine;
using UnityEditor;

#if UNITY_EDITOR
[CustomEditor(typeof(SpriteColorSwapper))]
public class SpriteColorSwapperEditor : Editor
{
    private SerializedProperty targetRendererProp;
    private SerializedProperty colorSwapMaterialProp;
    private SerializedProperty applyOnStartProp;
    private SerializedProperty colorSwapsProp;
    private SerializedProperty colorThresholdProp;
    
    void OnEnable()
    {
        targetRendererProp = serializedObject.FindProperty("targetRenderer");
        colorSwapMaterialProp = serializedObject.FindProperty("colorSwapMaterial");
        applyOnStartProp = serializedObject.FindProperty("applyOnStart");
        colorSwapsProp = serializedObject.FindProperty("colorSwaps");
        colorThresholdProp = serializedObject.FindProperty("colorThreshold");
    }
    
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        
        SpriteColorSwapper swapper = (SpriteColorSwapper)target;
        
        EditorGUILayout.LabelField("Sprite Color Swapper", EditorStyles.boldLabel);
        
        // Basic settings
        EditorGUILayout.PropertyField(targetRendererProp);
        EditorGUILayout.PropertyField(colorSwapMaterialProp);
        EditorGUILayout.PropertyField(applyOnStartProp);
        EditorGUILayout.PropertyField(colorThresholdProp);
        
        // Color swaps list
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Color Swaps (Max 10)", EditorStyles.boldLabel);
        
        // Warning if there are already 10 swaps
        if (colorSwapsProp.arraySize >= 10)
        {
            EditorGUILayout.HelpBox("Maximum of 10 color swaps reached. The shader supports up to 10 color swaps.", MessageType.Info);
        }
        else
        {
            if (GUILayout.Button("Add Color Swap"))
            {
                AddNewColorSwap();
            }
        }
        
        if (GUILayout.Button("Clear All Swaps"))
        {
            colorSwapsProp.ClearArray();
        }
        
        for (int i = 0; i < colorSwapsProp.arraySize; i++)
        {
            SerializedProperty swapProp = colorSwapsProp.GetArrayElementAtIndex(i);
            SerializedProperty nameProp = swapProp.FindPropertyRelative("name");
            SerializedProperty originalColorProp = swapProp.FindPropertyRelative("originalColor");
            SerializedProperty targetColorProp = swapProp.FindPropertyRelative("targetColor");
            
            EditorGUILayout.BeginVertical(GUI.skin.box);
            
            // Header with delete button
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(nameProp.stringValue, EditorStyles.boldLabel);
            
            if (GUILayout.Button("X", GUILayout.Width(20)))
            {
                colorSwapsProp.DeleteArrayElementAtIndex(i);
                break;
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.PropertyField(nameProp, new GUIContent("Name"));
            EditorGUILayout.PropertyField(originalColorProp, new GUIContent("Original Color"));
            EditorGUILayout.PropertyField(targetColorProp, new GUIContent("Target Color"));
            
            if (GUILayout.Button("Pick Original Color From Sprite"))
            {
                ShowColorPickerWindow(i);
            }
            
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
        }
        
        // Apply button
        EditorGUILayout.Space();
        if (GUILayout.Button("Apply Changes"))
        {
            serializedObject.ApplyModifiedProperties();
            
            if (Application.isPlaying)
            {
                swapper.ApplyColorSwaps();
            }
        }
        
        serializedObject.ApplyModifiedProperties();
    }
    
    private void AddNewColorSwap()
    {
        colorSwapsProp.arraySize++;
        SerializedProperty newSwap = colorSwapsProp.GetArrayElementAtIndex(colorSwapsProp.arraySize - 1);
        
        newSwap.FindPropertyRelative("name").stringValue = "New Swap";
        newSwap.FindPropertyRelative("originalColor").colorValue = Color.white;
        newSwap.FindPropertyRelative("targetColor").colorValue = Color.red;
    }
    
    private void ShowColorPickerWindow(int swapIndex)
    {
        SpriteColorSwapper swapper = (SpriteColorSwapper)target;
        SpriteRenderer renderer = null;
        
        // Try to get the target renderer from the swapper
        SerializedProperty rendererProp = serializedObject.FindProperty("targetRenderer");
        if (rendererProp != null && rendererProp.objectReferenceValue != null)
        {
            renderer = rendererProp.objectReferenceValue as SpriteRenderer;
        }
        
        // If not assigned, try to get it from the same GameObject
        if (renderer == null)
        {
            renderer = swapper.GetComponent<SpriteRenderer>();
        }
        
        if (renderer == null || renderer.sprite == null)
        {
            EditorUtility.DisplayDialog("Error", "No sprite found on the target renderer.", "OK");
            return;
        }
        
        ColorPickerWindow.ShowWindow(swapper, renderer.sprite, swapIndex);
    }
    
    private class ColorPickerWindow : EditorWindow
    {
        private static Texture2D spriteTexture;
        private static SpriteColorSwapper targetSwapper;
        private static int swapIndex;
        private static Vector2 scrollPosition;
        private static float zoom = 8.0f;
        
        public static void ShowWindow(SpriteColorSwapper swapper, Sprite sprite, int index)
        {
            targetSwapper = swapper;
            swapIndex = index;
            
            // Create texture from sprite
            spriteTexture = new Texture2D(
                (int)sprite.rect.width, 
                (int)sprite.rect.height, 
                TextureFormat.RGBA32, 
                false);
                
            Color[] pixels = sprite.texture.GetPixels(
                (int)sprite.rect.x, 
                (int)sprite.rect.y, 
                (int)sprite.rect.width, 
                (int)sprite.rect.height);
                
            spriteTexture.SetPixels(pixels);
            spriteTexture.Apply();
            
            // Create window
            ColorPickerWindow window = GetWindow<ColorPickerWindow>();
            window.titleContent = new GUIContent("Color Picker");
            window.minSize = new Vector2(300, 300);
            window.ShowUtility();
        }
        
        void OnGUI()
        {
            if (spriteTexture == null || targetSwapper == null)
            {
                Close();
                return;
            }
            
            GUILayout.Label("Click on a pixel to select its color", EditorStyles.boldLabel);
            
            // Zoom control
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Zoom:", GUILayout.Width(50));
            zoom = EditorGUILayout.Slider(zoom, 1.0f, 16.0f);
            EditorGUILayout.EndHorizontal();
            
            // Calculate display size
            float displayWidth = spriteTexture.width * zoom;
            float displayHeight = spriteTexture.height * zoom;
            
            // Begin scroll view
            Rect viewRect = new Rect(0, 0, displayWidth, displayHeight);
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.ExpandHeight(true));
            
            // Draw zoomed texture
            Rect texRect = GUILayoutUtility.GetRect(displayWidth, displayHeight);
            GUI.DrawTexture(texRect, spriteTexture, ScaleMode.StretchToFill);
            
            // Handle mouse clicks
            Event evt = Event.current;
            if (evt.type == EventType.MouseDown && evt.button == 0 && texRect.Contains(evt.mousePosition))
            {
                // Calculate pixel position
                int x = Mathf.FloorToInt((evt.mousePosition.x - texRect.x) / zoom);
                int y = spriteTexture.height - 1 - Mathf.FloorToInt((evt.mousePosition.y - texRect.y) / zoom);
                
                // Get pixel color
                Color pickedColor = spriteTexture.GetPixel(x, y);
                
                // Only use pixels with some opacity
                if (pickedColor.a > 0.1f)
                {
                    // Update serialized property
                    SerializedObject so = new SerializedObject(targetSwapper);
                    SerializedProperty colorSwapsProp = so.FindProperty("colorSwaps");
                    SerializedProperty swapProp = colorSwapsProp.GetArrayElementAtIndex(swapIndex);
                    SerializedProperty originalColorProp = swapProp.FindPropertyRelative("originalColor");
                    
                    originalColorProp.colorValue = pickedColor;
                    so.ApplyModifiedProperties();
                    
                    // Close window
                    Close();
                }
            }
            
            EditorGUILayout.EndScrollView();
            
            GUILayout.Label("Click on a colored pixel to pick its color");
        }
        
        void OnDestroy()
        {
            // Clean up
            if (spriteTexture != null)
            {
                DestroyImmediate(spriteTexture);
            }
        }
    }
}
#endif