// 서 있는 소용돌이 포털. 세로 타원 안에서 빛 줄기가 가운데로 감겨 들어가고 테두리가 밝게 빛남
// 흰 바닥 앞에서도 파랑이 보이게 알파 섞기 (미리 곱한 알파). 판은 세로축으로만 카메라를 향함 (원통 빌보드). 텍스처 없이 각도와 반지름 식으로 그림
Shader "WM/PortalSwirl"
{
    Properties
    {
        _CoreColor ("Core", Color) = (1, 1, 1, 1)
        _MidColor ("Mid", Color) = (0.45, 0.9, 1, 1)
        _OuterColor ("Outer", Color) = (0.15, 0.35, 1, 1)
        _RimColor ("Rim", Color) = (0.7, 0.95, 1, 1)
        _Intensity ("Intensity", Float) = 1.15
        _Arms ("Swirl arms", Float) = 3
        _Twist ("Twist (turns from rim to core)", Float) = 1.6
        _Speed ("Turn speed", Float) = 1.4
        _RimWidth ("Rim width", Range(0.01, 0.4)) = 0.12
        _Pulse ("Pulse speed", Float) = 2.2
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" "DisableBatching" = "True" }
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
            float _Intensity; float _Arms; float _Twist; float _Speed; float _RimWidth; float _Pulse;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float fogFactor : TEXCOORD1; };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 centerWS = TransformObjectToWorld(float3(0, 0, 0));
                float3 scale = float3(length(UNITY_MATRIX_M._m00_m10_m20), length(UNITY_MATRIX_M._m01_m11_m21), length(UNITY_MATRIX_M._m02_m12_m22));
                float3 toCamera = _WorldSpaceCameraPos - centerWS;
                toCamera.y = 0;
                toCamera = length(toCamera) > 1e-4 ? normalize(toCamera) : float3(0, 0, -1);
                float3 right = normalize(cross(float3(0, 1, 0), toCamera));
                float3 positionWS = centerWS + right * input.positionOS.x * scale.x + float3(0, 1, 0) * input.positionOS.y * scale.y;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = input.uv;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 p = input.uv * 2 - 1;
                float radius = length(p);
                if (radius > 1) discard;
                float angle = atan2(p.y, p.x) / (2 * PI);
                float time = _Time.y;
                float spiral = frac(angle * _Arms + (1 - radius) * _Twist * _Arms - time * _Speed);
                float arms = smoothstep(0.0, 0.35, spiral) * smoothstep(1.0, 0.55, spiral);
                float fine = 0.5 + 0.5 * sin((angle * 9 + (1 - radius) * 7 - time * _Speed * 2.3) * 2 * PI);
                float swirl = saturate(arms * 0.75 + fine * 0.25);
                float core = smoothstep(0.32, 0.0, radius);
                float3 body = lerp(_OuterColor.rgb, _MidColor.rgb, saturate(swirl * (1.2 - radius)));
                body = lerp(body, _CoreColor.rgb, core * core);
                float pulse = 0.85 + 0.15 * sin(time * _Pulse);
                float rim = smoothstep(1 - _RimWidth, 1 - _RimWidth * 0.35, radius) * smoothstep(1.0, 1 - _RimWidth * 0.3, radius);
                float fade = smoothstep(1.0, 0.82, radius);
                float alpha = saturate(fade * (0.55 + swirl * 0.45 + core) + rim);
                float3 color = body * (0.55 + swirl * 0.6 + core) * fade + _RimColor.rgb * rim * 1.3;
                color = MixFog(color * _Intensity * pulse, input.fogFactor);
                return half4(color * alpha, alpha);
            }
            ENDHLSL
        }
    }
}
