Shader "GridPuzzle/VoxelOutline"
{
    Properties
    {
        [MainColor] _BaseColor ("Base Color", Color) = (1,1,1,1)
        _OutlineColor ("Outline Color", Color) = (0,0,0,1)
        _OutlineWidth ("Outline Width", Range(0, 0.5)) = 0.05
    }
    SubShader
    {
        Tags 
        { 
            "RenderType"="Opaque" 
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }
        
        // 1. Outline Pass (Fixed Aspect Ratio)
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            ZWrite On
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
                UNITY_DEFINE_INSTANCED_PROP(float4, _OutlineColor)
                UNITY_DEFINE_INSTANCED_PROP(float, _OutlineWidth)
            UNITY_INSTANCING_BUFFER_END(Props)

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                
                float width = UNITY_ACCESS_INSTANCED_PROP(Props, _OutlineWidth);
                float4 posCS = TransformObjectToHClip(input.positionOS.xyz);
                float3 normWS = TransformObjectToWorldNormal(input.normalOS);
                float3 normCS = TransformWorldToHClipDir(normWS);
                
                // Aspect Ratio Correction: 
                // In 720x1280, X needs to be wider to match Y on screen
                float aspect = _ScreenParams.y / _ScreenParams.x;
                float2 offset = normalize(normCS.xy);
                
                offset.x *= aspect; // Boost horizontal offset to match screen pixels
                
                posCS.xy += offset * width * posCS.w * 0.1;
                
                output.positionCS = posCS;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return UNITY_ACCESS_INSTANCED_PROP(Props, _OutlineColor);
            }
            ENDHLSL
        }

        // 2. Base Color Pass (With Enhanced Shading)
        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
            UNITY_INSTANCING_BUFFER_END(Props)

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float4 baseColor = UNITY_ACCESS_INSTANCED_PROP(Props, _BaseColor);
                
                float3 normalWS = normalize(input.normalWS);
                Light mainLight = GetMainLight();
                
                // Enhanced Shading for stronger 3D feel
                float nDotL = dot(normalWS, mainLight.direction);
                float shadow = saturate(nDotL * 0.5 + 0.5); // Half-Lambert for softer but visible shading
                
                // Add a bit of rim light to pop the edges
                float3 viewDirWS = normalize(GetCameraPositionWS() - input.normalWS); // Simplified
                float rim = 1.0 - saturate(dot(normalWS, viewDirWS));
                rim = pow(rim, 3) * 0.2;
                
                float3 finalColor = baseColor.rgb * (mainLight.color * shadow + rim);
                return half4(finalColor, baseColor.a);
            }
            ENDHLSL
        }
    }
}
