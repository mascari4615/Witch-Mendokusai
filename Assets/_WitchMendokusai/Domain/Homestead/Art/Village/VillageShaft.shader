// 햇살 줄기. 판 하나가 해 쪽으로 뻗고 카메라를 향해 돎
// 정점: POSITION 은 발, TEXCOORD0 은 (가로 -1..1, 세로 0..1), TEXCOORD1 은 (길이, 반폭)
Shader "WM/VillageShaft"
{
    Properties
    {
        _Strength ("Strength", Range(0, 1)) = 0.2
        _NearFade ("Near fade (start, end in m)", Vector) = (5, 14, 0, 0)
        _Drift ("Streak drift speed", Float) = 0.06
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Blend One One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "VillageSky.hlsl"

            float _VillageDaylight;

            CBUFFER_START(UnityPerMaterial)
                float _Strength;
                float4 _NearFade;
                float _Drift;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float2 size : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 noise : TEXCOORD1;
                float fade : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                float3 foot = TransformObjectToWorld(input.positionOS.xyz);
                float3 axis = normalize(_VillageSunDirection.xyz + float3(0, 1e-4, 0));
                float3 toCamera = _WorldSpaceCameraPos - foot;
                float3 side = normalize(cross(axis, toCamera) + float3(1e-4, 0, 0));
                // 해와 눈이 나란하면 판이 눕지 않아 사라지게
                float facing = length(cross(axis, normalize(toCamera)));
                float3 world = foot + axis * (input.uv.y * input.size.x) + side * (input.uv.x * input.size.y);
                Varyings output;
                output.positionCS = TransformWorldToHClip(world);
                output.uv = input.uv;
                output.noise = float2(foot.x * 0.71 + foot.z * 0.37 + input.uv.x * 1.6, _Time.y * _Drift);
                output.fade = smoothstep(_NearFade.x, _NearFade.y, distance(world, _WorldSpaceCameraPos)) * smoothstep(0.15, 0.6, facing) * saturate(axis.y * 3.0) * smoothstep(0.15, 0.5, _VillageDaylight);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float across = 1.0 - abs(input.uv.x);
                float along = smoothstep(0.0, 0.25, input.uv.y) * smoothstep(1.0, 0.45, input.uv.y);
                float streak = 0.55 + 0.45 * SkyNoise(float2(input.noise.x * 3.0, input.noise.y * 4.0));
                float glow = across * across * along * streak * input.fade * _Strength;
                return half4(_VillageSunColor.rgb * float3(1.0, 0.94, 0.78) * glow, 0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
