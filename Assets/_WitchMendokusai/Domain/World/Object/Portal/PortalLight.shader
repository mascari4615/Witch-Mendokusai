// 땅에서 솟는 빛 기둥 포털. 판이 아니라 원통 메시 (PortalTube) 와 바닥 원판에 같이 씀
// 원통: 둘레를 따라 세로 빛줄기가 돌아가며 위로 흐르고 위로 갈수록 사라짐. 앞뒤 면이 다 보여 줄기가 겹치며 부피가 생김
// 원판 (_Disc = 1): 바닥에 깔린 둥근 빛 웅덩이. 흰 바닥 앞에서도 보이게 미리 곱한 알파
Shader "WM/PortalLight"
{
    Properties
    {
        _CoreColor ("Streak white", Color) = (1, 1, 1, 1)
        _MidColor ("Streak cyan", Color) = (0.42, 0.85, 1, 1)
        _OuterColor ("Streak pink", Color) = (0.92, 0.55, 1, 1)
        _RimColor ("Base glow", Color) = (0.6, 0.95, 1, 1)
        _Intensity ("Intensity", Float) = 1.35
        _Streaks ("Streak count around", Float) = 18
        _Speed ("Rise speed", Float) = 0.9
        _Flicker ("Flicker speed", Float) = 2.4
        _TopFade ("Top fade power", Range(0.5, 4)) = 1.15
        _BaseGlow ("Base glow", Range(0, 2)) = 1.0
        _Disc ("Ground disc mode", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend One OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            Name "Unlit"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float4 _CoreColor; float4 _MidColor; float4 _OuterColor; float4 _RimColor;
            float _Intensity; float _Streaks; float _Speed; float _Flicker; float _TopFade; float _BaseGlow; float _Disc;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float fogFactor : TEXCOORD1; float3 normalWS : TEXCOORD2; float3 viewWS : TEXCOORD3; };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewWS = _WorldSpaceCameraPos - positionWS;
                output.uv = input.uv;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            float Hash(float n)
            {
                return frac(sin(n * 127.1) * 43758.5453);
            }

            half4 Disc(float2 uv)
            {
                float radius = length(uv * 2 - 1);
                float pulse = 0.85 + 0.15 * sin(_Time.y * _Flicker);
                float pool = exp(-radius * radius * 3.5) * pulse;
                float ring = exp(-pow((radius - 0.62) * 9, 2)) * 0.5;
                float alpha = saturate((pool + ring) * smoothstep(1.0, 0.85, radius) * _BaseGlow);
                float3 color = lerp(_RimColor.rgb, _CoreColor.rgb, saturate(pool * 0.8)) * _Intensity;
                return half4(color * alpha, alpha);
            }

            half4 frag(Varyings input) : SV_Target
            {
                if (_Disc > 0.5)
                {
                    return Disc(input.uv);
                }
                float y = input.uv.y;
                float time = _Time.y;
                // 둘레 칸마다 위치, 굵기, 색, 일렁임이 다름. 칸 수를 정수로 맞춰 이음매가 없음
                float streaks = max(1, round(_Streaks));
                float column = input.uv.x * streaks;
                float cell = fmod(floor(column), streaks);
                float local = frac(column);
                float seed = Hash(cell + 1.7);
                float offset = (Hash(cell + 9.1) - 0.5) * 0.4;
                float width = lerp(0.12, 0.35, Hash(cell + 4.3));
                float streak = smoothstep(width, 0.0, abs(local - 0.5 - offset));
                float rise = 0.55 + 0.45 * sin((y * lerp(2.5, 5.0, seed) - time * _Speed * lerp(0.7, 1.4, seed)) * 6.2831);
                float flicker = 0.6 + 0.4 * sin(time * _Flicker * lerp(0.6, 1.5, Hash(cell + 2.2)) + seed * 6.2831);
                // 줄기마다 솟는 높이가 달라 윗끝이 들쭉날쭉
                float reach = lerp(0.45, 1.0, Hash(cell + 6.6));
                float lines = streak * rise * flicker * pow(saturate(1 - y / reach), _TopFade);
                // 원통 옆 가장자리 (시선과 비스듬한 면) 는 빛이 겹쳐 진하게, 정면은 옅게
                float facing = abs(dot(normalize(input.normalWS), normalize(input.viewWS)));
                float edge = 1 - facing;
                float fade = pow(saturate(1 - y), _TopFade) * smoothstep(0.0, 0.03, y);
                float baseGlow = exp(-y * 6) * _BaseGlow;
                float3 streakColor = seed < 0.5 ? lerp(_CoreColor.rgb, _MidColor.rgb, seed * 2) : lerp(_CoreColor.rgb, _OuterColor.rgb, (seed - 0.5) * 2);
                float haze = lerp(0.03, 0.28, edge * edge) * pow(saturate(1 - y), 2);
                float3 hazeColor = lerp(_CoreColor.rgb, _MidColor.rgb, 0.55) + (_OuterColor.rgb - _CoreColor.rgb) * 0.25 * saturate(y);
                float lineAlpha = saturate(lines * 2.2);
                float alpha = saturate(lineAlpha * smoothstep(0.0, 0.03, y) + haze * fade + baseGlow * 0.6);
                float3 color = (streakColor * lineAlpha * smoothstep(0.0, 0.03, y) + hazeColor * haze * fade + _RimColor.rgb * baseGlow * 0.6) / max(alpha, 1e-3);
                color = MixFog(color * _Intensity, input.fogFactor);
                return half4(color * alpha, alpha);
            }
            ENDHLSL
        }
    }
}
