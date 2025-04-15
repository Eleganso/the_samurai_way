Shader "Custom/ColorSwapShader"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _ColorThreshold ("Color Threshold", Range(0.0, 1.0)) = 0.1
        
        // Color swap pairs (10 total)
        _ColorToReplace1 ("Color to Replace 1", Color) = (1,1,1,1)
        _NewColor1 ("New Color 1", Color) = (1,1,1,1)
        _ColorToReplace2 ("Color to Replace 2", Color) = (1,1,1,1)
        _NewColor2 ("New Color 2", Color) = (1,1,1,1)
        _ColorToReplace3 ("Color to Replace 3", Color) = (1,1,1,1)
        _NewColor3 ("New Color 3", Color) = (1,1,1,1)
        _ColorToReplace4 ("Color to Replace 4", Color) = (1,1,1,1)
        _NewColor4 ("New Color 4", Color) = (1,1,1,1)
        _ColorToReplace5 ("Color to Replace 5", Color) = (1,1,1,1)
        _NewColor5 ("New Color 5", Color) = (1,1,1,1)
        _ColorToReplace6 ("Color to Replace 6", Color) = (1,1,1,1)
        _NewColor6 ("New Color 6", Color) = (1,1,1,1)
        _ColorToReplace7 ("Color to Replace 7", Color) = (1,1,1,1)
        _NewColor7 ("New Color 7", Color) = (1,1,1,1)
        _ColorToReplace8 ("Color to Replace 8", Color) = (1,1,1,1)
        _NewColor8 ("New Color 8", Color) = (1,1,1,1)
        _ColorToReplace9 ("Color to Replace 9", Color) = (1,1,1,1)
        _NewColor9 ("New Color 9", Color) = (1,1,1,1)
        _ColorToReplace10 ("Color to Replace 10", Color) = (1,1,1,1)
        _NewColor10 ("New Color 10", Color) = (1,1,1,1)
        
        // Required for sprite renderer flipping to work
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [HideInInspector] _AlphaTex ("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
        CGPROGRAM
            #pragma vertex SpriteVert
            #pragma fragment SwapColorsFrag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma multi_compile _ PIXELSNAP_ON
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "UnitySprites.cginc"

            float _ColorThreshold;
            
            // Define all 10 color pairs
            float4 _ColorToReplace1, _NewColor1;
            float4 _ColorToReplace2, _NewColor2;
            float4 _ColorToReplace3, _NewColor3;
            float4 _ColorToReplace4, _NewColor4;
            float4 _ColorToReplace5, _NewColor5;
            float4 _ColorToReplace6, _NewColor6;
            float4 _ColorToReplace7, _NewColor7;
            float4 _ColorToReplace8, _NewColor8;
            float4 _ColorToReplace9, _NewColor9;
            float4 _ColorToReplace10, _NewColor10;

            // Helper function to calculate luminance ratio
            float GetLumRatio(float3 original, float3 reference) {
                float lum = dot(original, float3(0.3, 0.59, 0.11));
                float refLum = dot(reference, float3(0.3, 0.59, 0.11));
                return lum / max(refLum, 0.001);
            }

            fixed4 SwapColorsFrag(v2f IN) : SV_Target
            {
                fixed4 c = SampleSpriteTexture(IN.texcoord);
                
                // Apply vertex color
                fixed4 col = c * IN.color;
                
                // Skip pixels with very low alpha
                if (col.a < 0.01)
                    return fixed4(0, 0, 0, 0);
                
                // Only process pixels with enough alpha
                if (col.a > 0.5) {
                    // Using an array-like approach to reduce code duplication
                    // First calculate all distances
                    float distances[10];
                    distances[0] = length(col.rgb - _ColorToReplace1.rgb);
                    distances[1] = length(col.rgb - _ColorToReplace2.rgb);
                    distances[2] = length(col.rgb - _ColorToReplace3.rgb);
                    distances[3] = length(col.rgb - _ColorToReplace4.rgb);
                    distances[4] = length(col.rgb - _ColorToReplace5.rgb);
                    distances[5] = length(col.rgb - _ColorToReplace6.rgb);
                    distances[6] = length(col.rgb - _ColorToReplace7.rgb);
                    distances[7] = length(col.rgb - _ColorToReplace8.rgb);
                    distances[8] = length(col.rgb - _ColorToReplace9.rgb);
                    distances[9] = length(col.rgb - _ColorToReplace10.rgb);
                    
                    // Find the closest match and apply the color swap
                    if (distances[0] < _ColorThreshold) {
                        col.rgb = _NewColor1.rgb * GetLumRatio(col.rgb, _ColorToReplace1.rgb);
                    }
                    else if (distances[1] < _ColorThreshold) {
                        col.rgb = _NewColor2.rgb * GetLumRatio(col.rgb, _ColorToReplace2.rgb);
                    }
                    else if (distances[2] < _ColorThreshold) {
                        col.rgb = _NewColor3.rgb * GetLumRatio(col.rgb, _ColorToReplace3.rgb);
                    }
                    else if (distances[3] < _ColorThreshold) {
                        col.rgb = _NewColor4.rgb * GetLumRatio(col.rgb, _ColorToReplace4.rgb);
                    }
                    else if (distances[4] < _ColorThreshold) {
                        col.rgb = _NewColor5.rgb * GetLumRatio(col.rgb, _ColorToReplace5.rgb);
                    }
                    else if (distances[5] < _ColorThreshold) {
                        col.rgb = _NewColor6.rgb * GetLumRatio(col.rgb, _ColorToReplace6.rgb);
                    }
                    else if (distances[6] < _ColorThreshold) {
                        col.rgb = _NewColor7.rgb * GetLumRatio(col.rgb, _ColorToReplace7.rgb);
                    }
                    else if (distances[7] < _ColorThreshold) {
                        col.rgb = _NewColor8.rgb * GetLumRatio(col.rgb, _ColorToReplace8.rgb);
                    }
                    else if (distances[8] < _ColorThreshold) {
                        col.rgb = _NewColor9.rgb * GetLumRatio(col.rgb, _ColorToReplace9.rgb);
                    }
                    else if (distances[9] < _ColorThreshold) {
                        col.rgb = _NewColor10.rgb * GetLumRatio(col.rgb, _ColorToReplace10.rgb);
                    }
                }
                
                return col;
            }
        ENDCG
        }
    }
}