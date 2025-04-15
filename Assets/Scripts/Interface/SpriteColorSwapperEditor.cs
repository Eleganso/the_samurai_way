using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

#if UNITY_EDITOR
[CustomEditor(typeof(EnhancedEnemyColorRandomizer))]
public class EnhancedEnemyColorRandomizerEditor : Editor
{
    private SerializedProperty colorSchemeGroupsProp;
    private SerializedProperty randomizeOnAwakeProp;
    private SerializedProperty randomSeedProp;
    
    private Dictionary<string, bool> groupFoldouts = new Dictionary<string, bool>();
    private Dictionary<string, Dictionary<string, bool>> schemeFoldouts = 
        new Dictionary<string, Dictionary<string, bool>>();
    
    private GUIStyle headerStyle;
    private GUIStyle subHeaderStyle;
    
    private Texture2D spriteTexture;
    private SpriteRenderer spriteRenderer;
    
    void OnEnable()
    {
        colorSchemeGroupsProp = serializedObject.FindProperty("colorSchemeGroups");
        randomizeOnAwakeProp = serializedObject.FindProperty("randomizeOnAwake");
        randomSeedProp = serializedObject.FindProperty("randomSeed");
        
        // Find sprite renderer in hierarchy
        EnhancedEnemyColorRandomizer targetScript = (EnhancedEnemyColorRandomizer)target;
        spriteRenderer = targetScript.GetComponent<SpriteRenderer>();
        
        if (spriteRenderer != null && spriteRenderer.sprite != null)
        {
            // Get sprite texture
            spriteTexture = GetReadableTexture(spriteRenderer.sprite);
        }
    }
    
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        
        // Initialize styles
        if (headerStyle == null)
        {
            headerStyle = new GUIStyle(EditorStyles.boldLabel);
            headerStyle.fontSize = 14;
            
            subHeaderStyle = new GUIStyle(EditorStyles.boldLabel);
            subHeaderStyle.fontSize = 12;
        }
        
        // Title
        EditorGUILayout.LabelField("Enhanced Enemy Color Randomizer", headerStyle);
        EditorGUILayout.Space();
        
        // Basic settings
        EditorGUILayout.PropertyField(randomizeOnAwakeProp);
        EditorGUILayout.PropertyField(randomSeedProp);
        
        // Buttons for testing
        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Randomize Now (Editor)"))
        {
            EnhancedEnemyColorRandomizer randomizer = (EnhancedEnemyColorRandomizer)target;
            // Make sure the script has properly initialized
            if (randomizer.GetComponent<SpriteColorSwapper>() == null)
            {
                Debug.LogError("SpriteColorSwapper component is missing on this GameObject! Please add it first.");
                return;
            }
            randomizer.RandomizeColors();
        }
        
        if (GUILayout.Button("Reset Colors"))
        {
            EnhancedEnemyColorRandomizer randomizer = (EnhancedEnemyColorRandomizer)target;
            SpriteColorSwapper swapper = randomizer.GetComponent<SpriteColorSwapper>();
            if (swapper != null)
            {
                swapper.ClearColorSwaps();
            }
        }
        EditorGUILayout.EndHorizontal();
        
        // Color schemes section
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Color Schemes", headerStyle);
        
        // Add button
        if (GUILayout.Button("Add Color Scheme Group"))
        {
            AddNewGroup();
        }
        
        // Display color scheme groups
        for (int i = 0; i < colorSchemeGroupsProp.arraySize; i++)
        {
            DisplayColorSchemeGroup(colorSchemeGroupsProp.GetArrayElementAtIndex(i), i);
        }
        
        // Color picker
        if (spriteTexture != null)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Sprite Color Picker", headerStyle);
            
            // Draw a scaled version of the sprite for color picking
            float maxWidth = EditorGUIUtility.currentViewWidth - 40;
            float scale = Mathf.Min(1, maxWidth / spriteTexture.width);
            float displayWidth = spriteTexture.width * scale;
            float displayHeight = spriteTexture.height * scale;
            
            Rect textureRect = GUILayoutUtility.GetRect(displayWidth, displayHeight);
            EditorGUI.DrawPreviewTexture(textureRect, spriteTexture);
            
            EditorGUILayout.LabelField("Click on sprite to copy color", EditorStyles.miniLabel);
            
            Event evt = Event.current;
            if (evt.type == EventType.MouseDown && textureRect.Contains(evt.mousePosition))
            {
                // Calculate pixel coordinates
                int x = Mathf.FloorToInt((evt.mousePosition.x - textureRect.x) / scale);
                int y = Mathf.FloorToInt((evt.mousePosition.y - textureRect.y) / scale);
                
                // Make sure we're within bounds
                if (x >= 0 && x < spriteTexture.width && y >= 0 && y < spriteTexture.height)
                {
                    // Get pixel color
                    Color pixelColor = spriteTexture.GetPixel(x, y);
                    
                    // If alpha is not zero, copy to clipboard
                    if (pixelColor.a > 0.01f)
                    {
                        // Format in a way that's easy to copy into code
                        string colorValues = $"R: {pixelColor.r:F3}, G: {pixelColor.g:F3}, B: {pixelColor.b:F3}, A: {pixelColor.a:F3}";
                        EditorGUIUtility.systemCopyBuffer = colorValues;
                        Debug.Log($"Copied color: {colorValues}");
                    }
                }
            }
        }
        
        serializedObject.ApplyModifiedProperties();
    }
    
    private void DisplayColorSchemeGroup(SerializedProperty groupProp, int groupIndex)
    {
        SerializedProperty nameProp = groupProp.FindPropertyRelative("name");
        SerializedProperty schemesProp = groupProp.FindPropertyRelative("possibleSchemes");
        
        string groupName = nameProp.stringValue;
        if (string.IsNullOrEmpty(groupName))
            groupName = "Group " + groupIndex;
            
        // Make sure we have a foldout entry for this group
        if (!groupFoldouts.ContainsKey(groupName))
            groupFoldouts[groupName] = false;
            
        // Group header with foldout
        EditorGUILayout.BeginVertical(GUI.skin.box);
        EditorGUILayout.BeginHorizontal();
        
        groupFoldouts[groupName] = EditorGUILayout.Foldout(groupFoldouts[groupName], "", true);
        EditorGUILayout.PropertyField(nameProp, GUIContent.none);
        
        if (GUILayout.Button("X", GUILayout.Width(20)))
        {
            colorSchemeGroupsProp.DeleteArrayElementAtIndex(groupIndex);
            return;
        }
        EditorGUILayout.EndHorizontal();
        
        // If group is expanded, show details
        if (groupFoldouts[groupName])
        {
            EditorGUI.indentLevel++;
            
            // Add new scheme button
            if (GUILayout.Button("Add Color Scheme"))
            {
                AddNewScheme(schemesProp);
            }
            
            // Display each color scheme
            for (int i = 0; i < schemesProp.arraySize; i++)
            {
                DisplayColorScheme(schemesProp.GetArrayElementAtIndex(i), groupName, i);
            }
            
            EditorGUI.indentLevel--;
        }
        
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space();
    }
    
    private void DisplayColorScheme(SerializedProperty schemeProp, string groupName, int schemeIndex)
    {
        SerializedProperty schemeNameProp = schemeProp.FindPropertyRelative("name");
        SerializedProperty replacementsProp = schemeProp.FindPropertyRelative("colorReplacements");
        
        string schemeName = schemeNameProp.stringValue;
        if (string.IsNullOrEmpty(schemeName))
            schemeName = "Scheme " + schemeIndex;
            
        // Make sure we have nested dictionaries for scheme foldouts
        if (!schemeFoldouts.ContainsKey(groupName))
            schemeFoldouts[groupName] = new Dictionary<string, bool>();
            
        if (!schemeFoldouts[groupName].ContainsKey(schemeName))
            schemeFoldouts[groupName][schemeName] = false;
            
        // Scheme header with foldout
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();
        
        schemeFoldouts[groupName][schemeName] = EditorGUILayout.Foldout(
            schemeFoldouts[groupName][schemeName], "", true);
        EditorGUILayout.PropertyField(schemeNameProp, GUIContent.none);
        
        if (GUILayout.Button("X", GUILayout.Width(20)))
        {
            replacementsProp.DeleteArrayElementAtIndex(schemeIndex);
            return;
        }
        EditorGUILayout.EndHorizontal();
        
        // If scheme is expanded, show details
        if (schemeFoldouts[groupName][schemeName])
        {
            EditorGUI.indentLevel++;
            
            // Add new color replacement button
            if (GUILayout.Button("Add Color Replacement"))
            {
                AddNewColorReplacement(replacementsProp);
            }
            
            // Display each color replacement
            for (int i = 0; i < replacementsProp.arraySize; i++)
            {
                DisplayColorReplacement(replacementsProp.GetArrayElementAtIndex(i), i);
            }
            
            EditorGUI.indentLevel--;
        }
        
        EditorGUILayout.EndVertical();
    }
    
    private void DisplayColorReplacement(SerializedProperty replacementProp, int index)
    {
        SerializedProperty originalColorProp = replacementProp.FindPropertyRelative("originalColor");
        SerializedProperty targetColorProp = replacementProp.FindPropertyRelative("targetColor");
        
        EditorGUILayout.BeginHorizontal();
        
        // Color fields with labels
        EditorGUILayout.BeginVertical();
        EditorGUILayout.LabelField("Original", EditorStyles.miniLabel);
        EditorGUILayout.PropertyField(originalColorProp, GUIContent.none);
        EditorGUILayout.EndVertical();
        
        EditorGUILayout.BeginVertical();
        EditorGUILayout.LabelField("Target", EditorStyles.miniLabel);
        EditorGUILayout.PropertyField(targetColorProp, GUIContent.none);
        EditorGUILayout.EndVertical();
        
        if (GUILayout.Button("X", GUILayout.Width(20)))
        {
            replacementProp.DeleteCommand();
            return;
        }
        
        EditorGUILayout.EndHorizontal();
    }
    
    private void AddNewGroup()
    {
        int index = colorSchemeGroupsProp.arraySize;
        colorSchemeGroupsProp.InsertArrayElementAtIndex(index);
        SerializedProperty newGroup = colorSchemeGroupsProp.GetArrayElementAtIndex(index);
        
        // Set default values
        newGroup.FindPropertyRelative("name").stringValue = "New Group";
        SerializedProperty schemes = newGroup.FindPropertyRelative("possibleSchemes");
        schemes.ClearArray();
    }
    
    private void AddNewScheme(SerializedProperty schemesProp)
    {
        int index = schemesProp.arraySize;
        schemesProp.InsertArrayElementAtIndex(index);
        SerializedProperty newScheme = schemesProp.GetArrayElementAtIndex(index);
        
        // Set default values
        newScheme.FindPropertyRelative("name").stringValue = "New Scheme";
        SerializedProperty replacements = newScheme.FindPropertyRelative("colorReplacements");
        replacements.ClearArray();
    }
    
    private void AddNewColorReplacement(SerializedProperty replacementsProp)
    {
        int index = replacementsProp.arraySize;
        replacementsProp.InsertArrayElementAtIndex(index);
        SerializedProperty newReplacement = replacementsProp.GetArrayElementAtIndex(index);
        
        // Set default values
        newReplacement.FindPropertyRelative("originalColor").colorValue = Color.white;
        newReplacement.FindPropertyRelative("targetColor").colorValue = Color.red;
    }
    
    // Helper to get a readable texture from a sprite
    private Texture2D GetReadableTexture(Sprite sprite)
    {
        if (sprite == null || sprite.texture == null)
            return null;
            
        Texture2D original = sprite.texture;
        
        // Create a temporary texture with the sprite's area
        Texture2D copy = new Texture2D((int)sprite.rect.width, (int)sprite.rect.height, TextureFormat.RGBA32, false);
        
        // Set pixels from the sprite's texture area
        Color[] pixels = original.GetPixels(
            (int)sprite.rect.x, 
            (int)sprite.rect.y, 
            (int)sprite.rect.width, 
            (int)sprite.rect.height);
            
        copy.SetPixels(pixels);
        copy.Apply();
        
        return copy;
    }
}
#endif