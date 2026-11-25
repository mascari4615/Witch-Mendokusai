// 숲 스테이지 풀잎. 정점 색으로 뿌리에서 끝까지 그라데이션, 법선은 위로 고정해 부드러운 명암, 그림자 받음
// 캐릭터 둘레 원과 카메라 바로 앞 풀잎은 점무늬로 비움 (Core/Shaders/WMOcclusionCutout.hlsl)
// 원형: 숲 시안 forestz21 연구 셰이더 (memo wm/design/style-pass-20261116)
Shader "WM/ForestGrass"
{
    Properties { _Wrap ("Light wrap", Float) = 0.5 }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Cull Off
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #define _WM_OCCLUDABLE
            #include "../../../../Core/Shaders/WMOcclusionCutout.hlsl"
            #include "../../../../Core/Shaders/WMWind.hlsl"
            CBUFFER_START(UnityPerMaterial) float _Wrap; CBUFFER_END
            struct A { float4 pos : POSITION; float4 col : COLOR; };
            struct V { float4 cs : SV_POSITION; float3 ws : TEXCOORD0; float4 col : COLOR; float fog : TEXCOORD1; float4 sp : TEXCOORD2; };
            V vert(A i)
            {
                V o; o.ws = TransformObjectToWorld(i.pos.xyz);
                // 뿌리는 색 알파 0, 끝은 1. 짧은 잎이라 세 배로
                o.ws += WMWind(o.ws, i.col.a * 3.0);
                o.cs = TransformWorldToHClip(o.ws); o.col = i.col; o.fog = ComputeFogFactor(o.cs.z); o.sp = ComputeScreenPos(o.cs); return o;
            }
            half4 frag(V i) : SV_Target
            {
                WMCutoutClip(WMCutoutCoverage(i.sp, i.ws), i.cs.xy);
                float3 n = float3(0, 1, 0);
                Light L = GetMainLight(TransformWorldToShadowCoord(i.ws));
                float nl = saturate((dot(n, L.direction) + _Wrap) / (1 + _Wrap));
                // 끝 (col.a 큼) 은 빛을 더 받고 뿌리는 풀잎 자기 그늘
                float sh = lerp(0.22, 1.0, L.shadowAttenuation);
                float3 c = SRGBToLinear(i.col.rgb);
                float3 lit = c * (L.color * nl * sh * lerp(0.75, 1.1, i.col.a) + SampleSH(n));
                return half4(MixFog(lit, i.fog), 1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ColorMask R
            HLSLPROGRAM
            #pragma vertex v
            #pragma fragment f
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #define _WM_OCCLUDABLE
            #include "../../../../Core/Shaders/WMOcclusionCutout.hlsl"
            #include "../../../../Core/Shaders/WMWind.hlsl"
            struct DV { float4 cs : SV_POSITION; float3 ws : TEXCOORD0; float4 sp : TEXCOORD1; };
            DV v(float4 p : POSITION, float4 col : COLOR) { DV o; o.ws = TransformObjectToWorld(p.xyz); o.ws += WMWind(o.ws, col.a * 3.0); o.cs = TransformWorldToHClip(o.ws); o.sp = ComputeScreenPos(o.cs); return o; }
            half f(DV i) : SV_Target { WMCutoutClip(WMCutoutCoverage(i.sp, i.ws), i.cs.xy); return 0; }
            ENDHLSL
        }
    }
}
