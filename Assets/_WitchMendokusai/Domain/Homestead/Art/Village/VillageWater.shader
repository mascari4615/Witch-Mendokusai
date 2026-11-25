// 마을 못과 개울의 수면. 반투명이라 파낸 바닥과 돌이 그대로 비침
// 정점 색 r 은 수면 아래 바닥까지의 깊이 (0 물가, 1 가장 깊은 곳). 깊이 텍스처와 화면 복사 없이 씀
// 겹: 깊이에 따른 물빛, 바닥에 어리는 햇빛 무늬, 하늘 반사, 해 반짝임, 물가 거품
Shader "WM/VillageWater"
{
    Properties
    {
        _Noise ("Noise", 2D) = "gray" {}
        _ShallowColor ("Shallow color", Color) = (0.36, 0.60, 0.56, 1)
        _DeepColor ("Deep color", Color) = (0.07, 0.22, 0.28, 1)
        _ShallowAlpha ("Alpha at shore", Range(0, 1)) = 0.18
        _DeepAlpha ("Alpha at depth", Range(0, 1)) = 0.86
        _DepthCurve ("Depth curve", Range(0.3, 3)) = 0.75
        _WaveScale ("Wave scale (1/m)", Float) = 0.17
        _WaveSpeed ("Wave speed", Float) = 0.03
        _WaveStrength ("Wave strength", Float) = 0.5
        _Reflect ("Sky reflection", Range(0, 1)) = 0.75
        _GlintPower ("Glint sharpness", Float) = 260
        _GlintStrength ("Glint strength", Float) = 2.4
        _CausticScale ("Caustic scale (1/m)", Float) = 0.42
        _CausticStrength ("Caustic strength", Float) = 0.5
        _MaxDepth ("Depth at red 1 (m)", Float) = 1.2
        _FoamWidth ("Foam width (depth)", Range(0.005, 0.3)) = 0.07
        _FoamStrength ("Foam strength", Range(0, 1)) = 0.5
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent-20" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_Noise);
            SAMPLER(sampler_Noise);

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                float _ShallowAlpha;
                float _DeepAlpha;
                float _DepthCurve;
                float _WaveScale;
                float _WaveSpeed;
                float _WaveStrength;
                float _Reflect;
                float _GlintPower;
                float _GlintStrength;
                float _CausticScale;
                float _CausticStrength;
                float _MaxDepth;
                float _FoamWidth;
                float _FoamStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 positionOS : TEXCOORD1;
                float depth : TEXCOORD2;
                float fogFactor : TEXCOORD3;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.positionOS = input.positionOS.xz;
                output.depth = input.color.r;
                output.fogFactor = ComputeFogFactor(position.positionCS.z);
                return output;
            }

            float Height(float2 uv)
            {
                float time = _Time.y * _WaveSpeed;
                float wide = SAMPLE_TEXTURE2D(_Noise, sampler_Noise, uv + float2(time, time * 0.7)).r;
                float cross = SAMPLE_TEXTURE2D(_Noise, sampler_Noise, uv * 1.7 + float2(-time * 1.3, time * 0.9)).g;
                float fine = SAMPLE_TEXTURE2D(_Noise, sampler_Noise, uv * 4.1 + float2(time * 2.3, -time * 1.9)).r;
                return wide * 0.5 + cross * 0.35 + fine * 0.15;
            }

            // 햇빛 무늬. 흐르는 두 무늬가 겹치는 가는 선
            float Caustic(float2 xz)
            {
                float time = _Time.y * _WaveSpeed * 1.6;
                float2 uv = xz * _CausticScale * 0.35;
                float first = SAMPLE_TEXTURE2D(_Noise, sampler_Noise, uv + float2(time, -time * 0.6)).r;
                float second = SAMPLE_TEXTURE2D(_Noise, sampler_Noise, uv * 1.31 + float2(-time * 0.8, time)).g;
                float thread = 1.0 - abs(first - second) * 5.0;
                return pow(saturate(thread), 4.0);
            }

            half4 frag(Varyings input) : SV_Target
            {
                float depth = saturate(input.depth);
                float2 uv = input.positionOS * _WaveScale;
                const float STEP = 0.012;
                float center = Height(uv);
                float2 slope = float2(Height(uv + float2(STEP, 0)) - center, Height(uv + float2(0, STEP)) - center) / STEP * 0.06;
                float calm = smoothstep(0.0, 0.25, depth);
                float3 normalWS = normalize(float3(-slope.x * _WaveStrength * calm, 1.0, -slope.y * _WaveStrength * calm));
                float3 viewWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                Light light = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                float shadow = light.shadowAttenuation;
                half3 ambient = SampleSH(float3(0, 1, 0));

                float through = pow(depth, _DepthCurve);
                half3 body = lerp(_ShallowColor.rgb, _DeepColor.rgb, through);
                body *= ambient + light.color * saturate(light.direction.y) * lerp(0.45, 1.0, shadow) * 0.6;
                float alpha = lerp(_ShallowAlpha, _DeepAlpha, through);

                // 바닥까지 내려간 자리에 어린 빛. 시선 방향으로 깊이만큼 밀어 물속에 있게
                float2 bed = input.positionWS.xz - viewWS.xz / max(viewWS.y, 0.2) * depth * _MaxDepth * 0.75 + normalWS.xz * 0.5;
                float caustic = Caustic(bed) * _CausticStrength * shadow * saturate(light.direction.y * 1.5) * smoothstep(0.02, 0.2, depth) * (1.0 - through * 0.75);

                float fresnel = 0.03 + 0.97 * pow(1.0 - saturate(dot(normalWS, viewWS)), 5.0);
                float3 reflected = reflect(-viewWS, normalWS);
                half3 sky = GlossyEnvironmentReflection(reflected, 0.12, 1.0);
                float mirror = saturate(fresnel * 1.6 + 0.06) * _Reflect;

                float3 halfway = normalize(light.direction + viewWS);
                float glint = pow(saturate(dot(normalWS, halfway)), _GlintPower) * _GlintStrength * shadow;

                // 물가 거품. 가는 띠를 무늬로 끊음
                float ripple = SAMPLE_TEXTURE2D(_Noise, sampler_Noise, input.positionOS * 0.6 + _Time.y * 0.015).r;
                float edge = 1.0 - smoothstep(0.0, _FoamWidth, depth + (ripple - 0.5) * _FoamWidth * 1.4);
                float foam = edge * _FoamStrength * smoothstep(0.0, 0.012, depth);

                half3 color = lerp(body, sky, mirror);
                alpha = saturate(max(alpha, mirror));
                color += light.color * caustic;
                alpha = saturate(alpha + caustic * 0.25);
                half3 foamColor = ambient + light.color * lerp(0.4, 0.9, shadow) * 0.7;
                color = lerp(color, foamColor, foam);
                alpha = saturate(max(alpha, foam * 0.85));
                color += light.color * glint;
                alpha = saturate(alpha + glint);
                alpha *= smoothstep(0.0, 0.02, input.depth);
                return half4(MixFog(color, input.fogFactor), alpha);
            }
            ENDHLSL
        }
    }
}
