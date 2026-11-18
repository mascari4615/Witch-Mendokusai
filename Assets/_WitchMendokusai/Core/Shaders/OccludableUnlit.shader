// 조명 없는 면 + 캐릭터 가림 컷아웃 (WMOcclusionCutout.hlsl)
// 텍스처와 색만. 반투명은 알파에 컷아웃 비율을 곱함
// 속성 이름은 URP Unlit 과 같음. 재질 셰이더만 바꿔도 값 유지
Shader "WM/Occludable Unlit"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base map", 2D) = "white" {}
        [MainColor] _BaseColor ("Base color", Color) = (1, 1, 1, 1)
        [Toggle(_WM_OCCLUDABLE)] _WM_Occludable ("Occludable (cut out in front of the character)", Float) = 1
        [HideInInspector] _WM_ObjectFade ("Object fade (set per renderer)", Float) = 0
        [HideInInspector] _Cull ("Cull", Float) = 2
        [HideInInspector] _Surface ("Surface", Float) = 0
        [HideInInspector] _SrcBlend ("Src blend", Float) = 1
        [HideInInspector] _DstBlend ("Dst blend", Float) = 0
        [HideInInspector] _SrcBlendAlpha ("Src blend alpha", Float) = 1
        [HideInInspector] _DstBlendAlpha ("Dst blend alpha", Float) = 0
        [HideInInspector] _ZWrite ("Z write", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "WMOcclusionCutout.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _WM_ObjectFade;
        CBUFFER_END

        // 원과 카메라 앞 비움 + 물체 단위 페이드 (OcclusionFadeTarget 이 렌더러마다 넣는 _WM_ObjectFade)
        float Coverage(float4 screenPosition, float3 positionWS)
        {
            float coverage = WMCutoutCoverage(screenPosition, positionWS);
            #if defined(_WM_OCCLUDABLE)
                coverage = max(coverage, _WM_ObjectFade);
            #endif
            return coverage;
        }

        struct Attributes
        {
            float4 positionOS : POSITION;
            float2 uv : TEXCOORD0;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
            float3 positionWS : TEXCOORD1;
            float4 screenPosition : TEXCOORD2;
        };

        Varyings vert(Attributes input)
        {
            Varyings output;
            VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
            output.positionCS = position.positionCS;
            output.positionWS = position.positionWS;
            output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
            output.screenPosition = ComputeScreenPos(position.positionCS);
            return output;
        }
        ENDHLSL

        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForwardOnly" }
            Blend [_SrcBlend] [_DstBlend], [_SrcBlendAlpha] [_DstBlendAlpha]
            ZWrite [_ZWrite]
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _WM_OCCLUDABLE
            #pragma shader_feature_local_fragment _SURFACE_TYPE_TRANSPARENT

            half4 frag(Varyings input) : SV_Target
            {
                half4 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                float coverage = Coverage(input.screenPosition, input.positionWS);
                #if defined(_SURFACE_TYPE_TRANSPARENT)
                    color.a *= 1.0 - coverage;
                #else
                    WMCutoutClip(coverage, input.positionCS.xy);
                    color.a = 1;
                #endif
                return color;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment fragDepth
            #pragma shader_feature_local _WM_OCCLUDABLE

            half fragDepth(Varyings input) : SV_Target
            {
                WMCutoutClip(Coverage(input.screenPosition, input.positionWS), input.positionCS.xy);
                return input.positionCS.z;
            }
            ENDHLSL
        }
    }
}
