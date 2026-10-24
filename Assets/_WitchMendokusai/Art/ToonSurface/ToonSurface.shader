// Two-tone toon surface for native content: a lit side and a tinted shade side with an adjustable boundary, plus an optional rim light.
// Property names for the base map, color, cull, blend and alpha clip match the standard surface factory, so it can replace the lit or unlit shader.
Shader "WM/Toon Surface"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base map", 2D) = "white" {}
        [MainColor] _BaseColor ("Base color", Color) = (1, 1, 1, 1)
        _ShadowColor ("Shade tint", Color) = (0.8, 0.86, 0.98, 1)
        _ShadowExtend ("Shade extent", Range(0, 1)) = 0.5
        _ShadeSoftness ("Shade edge softness", Range(0.001, 0.3)) = 0.04
        _LightStrength ("Light strength", Range(0, 2)) = 0.9
        _AmbientStrength ("Ambient strength", Range(0, 2)) = 0.4
        _RimStrength ("Rim strength", Range(0, 1)) = 0.0
        _RimPower ("Rim power", Range(0.5, 8)) = 3
        _Cutoff ("Alpha cutoff", Range(0, 1)) = 0.5
        [HideInInspector] _Cull ("Cull", Float) = 2
        [HideInInspector] _Surface ("Surface", Float) = 0
        [HideInInspector] _AlphaClip ("Alpha clip", Float) = 0
        [HideInInspector] _SrcBlend ("Src blend", Float) = 1
        [HideInInspector] _DstBlend ("Dst blend", Float) = 0
        [HideInInspector] _ZWrite ("Z write", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "ForwardToon"
            Tags { "LightMode" = "UniversalForward" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _ShadowColor;
                half _ShadowExtend;
                half _ShadeSoftness;
                half _LightStrength;
                half _AmbientStrength;
                half _RimStrength;
                half _RimPower;
                half _Cutoff;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 frag(Varyings input, bool isFrontFace : SV_IsFrontFace) : SV_Target
            {
                half4 base = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                #if defined(_ALPHATEST_ON)
                    clip(base.a - _Cutoff);
                #endif
                float3 normal = normalize(input.normalWS);
                normal = isFrontFace ? normal : -normal;
                Light light = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half facing = dot(normal, light.direction) * 0.5h + 0.5h;
                // A larger extent moves the boundary up so more of the surface falls into the shade side.
                half boundary = lerp(0.3h, 0.7h, _ShadowExtend);
                half lit = smoothstep(boundary - _ShadeSoftness, boundary + _ShadeSoftness, facing) * light.shadowAttenuation;
                half3 shade = base.rgb * _ShadowColor.rgb;
                half3 color = lerp(shade, base.rgb, lit);
                half3 lighting = light.color * _LightStrength + SampleSH(normal) * _AmbientStrength;
                color *= lighting;
                half3 view = normalize(GetWorldSpaceViewDir(input.positionWS));
                half rim = pow(1.0h - saturate(dot(normal, view)), _RimPower) * _RimStrength * lit;
                color += rim * light.color;
                return half4(color, base.a);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
