// 땅에서 솟는 빛 포털. 겉면을 칠한 통이 아니라 상자 안의 빛 덩어리를 픽셀마다 셈
// - 빛줄기 (두 겹) 와 밑동 바늘빛 (세 겹) 은 선분. 시선과 선분 사이 거리로 바로 구해서 걸음 (ray march) 이 없고 잡티도 없음
// - 몸통 안개만 짧게 걸어서 더함. 바닥 빛은 시선이 바닥과 만나는 점에서 구함
// - 깊이는 빛이 모인 자리의 평균으로 씀. 깊이 텍스처 없이도 앞의 풀과 인물은 빛을 가리고 뒤의 것은 빛에 덮임
// - 메시는 유니티 기본 정육면체. 중심은 바닥, 스케일 y 는 빛 높이의 두 배 (아래 절반은 바닥에 묻힘)
//   인물 그림은 깊이를 안 쓰고 중심 거리로 정렬되므로, 빛의 중심을 바닥에 둬야 인물과 앞뒤가 맞음
Shader "WM/PortalLight"
{
    Properties
    {
        _CoreColor ("Core white", Color) = (0.97, 0.98, 1, 1)
        _EdgeColor ("Edge blue", Color) = (0.56, 0.72, 1, 1)
        _TopColor ("Top pink violet", Color) = (0.95, 0.76, 1, 1)
        _SplashColor ("Base sky blue", Color) = (0.7, 0.9, 1, 1)
        _Intensity ("Brightness", Range(0.2, 3)) = 1
        _Radius ("Radius in height units", Range(0.05, 0.25)) = 0.194
        _Speed ("Motion speed", Range(0, 3)) = 1
        _RayDensity ("Ray density", Range(0, 40)) = 22
        _HazeDensity ("Haze density", Range(0, 6)) = 2.6
        _NeedleDensity ("Needle density", Range(0, 60)) = 24
        _NeedleLength ("Needle length", Range(0.2, 1.1)) = 1
        _NeedleWidth ("Needle width", Range(0.2, 3)) = 1
        _PoolDensity ("Ground pool density", Range(0, 60)) = 16
        _GroundGlow ("Ground glow density", Range(0, 10)) = 3
        _MoteSize ("Mote size", Range(0, 0.1)) = 0.018
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" "DisableBatching" = "True" }
        Blend One OneMinusSrcAlpha
        ZWrite Off
        ZTest LEqual
        // 뒷면만 그려서 카메라가 상자 안에 들어가도 보임. 깊이는 조각 셰이더가 따로 씀
        Cull Front
        Pass
        {
            Name "Unlit"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float4 _CoreColor; float4 _EdgeColor; float4 _TopColor; float4 _SplashColor;
            float _Intensity; float _Radius; float _Speed; float _RayDensity; float _HazeDensity; float _NeedleDensity; float _NeedleLength; float _NeedleWidth; float _PoolDensity; float _GroundGlow; float _MoteSize;
            CBUFFER_END

            // 치수는 높이 3.4 m 를 기준으로 맞춘 값. 물체가 커지면 비율대로 커짐
            #define DESIGN_HEIGHT 3.4
            #define OUTER_RAYS 15
            #define INNER_RAYS 9
            #define HAZE_STEPS 8
            #define MOTES 10
            #define NEEDLE_TOP 0.95
            #define SQRT_PI 1.7724539

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float fogFactor : TEXCOORD1; };

            // 한 픽셀에 모인 빛. color 와 distance 는 depth 로 나누기 전 합
            struct Gather { float3 color; float depth; float distance; };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            float Hash(float n)
            {
                return frac(sin(n * 127.1) * 43758.5453);
            }

            void Add(inout Gather gather, float3 color, float amount, float distanceOnRay)
            {
                gather.color += color * amount;
                gather.depth += amount;
                gather.distance += distanceOnRay * amount;
            }

            // 시선 (origin, direction) 과 선분 (a, b) 이 가장 가까운 곳. x 는 거리, y 는 선분 위 위치 0~1, z 는 시선 위 거리, w 는 비스듬한 만큼 길어지는 배수
            float4 NearSegment(float3 origin, float3 direction, float rayEnd, float3 a, float3 b)
            {
                float3 axis = b - a;
                float3 offset = origin - a;
                float axisLength2 = dot(axis, axis);
                float along = dot(direction, axis);
                float offsetRay = dot(direction, offset);
                float offsetAxis = dot(axis, offset);
                float denominator = max(axisLength2 - along * along, 1e-4);
                float s = saturate((offsetAxis - along * offsetRay) / denominator);
                float t = clamp(along * s - offsetRay, 0, rayEnd);
                float gap = length(origin + direction * t - (a + axis * s));
                float sine = sqrt(saturate(denominator / max(axisLength2, 1e-4)));
                return float4(gap, s, t, 1 / max(sine, 0.35));
            }

            // 빛줄기 한 바퀴. 줄기마다 굵기, 높이, 솟는 때가 다름
            void AddRays(inout Gather gather, float3 origin, float3 direction, float rayEnd, int count, float radius, float seedBase, float thin, float thick, float time, float outerRadius)
            {
                for (int i = 0; i < count; i++)
                {
                    float seed = i + seedBase;
                    float angle = (i + 0.5 + (Hash(seed) - 0.5) * 0.6) / count * TWO_PI;
                    float2 outward = float2(cos(angle), sin(angle));
                    float baseRadius = radius * lerp(0.88, 1.06, Hash(seed + 2));
                    float width = lerp(thin, thick, Hash(seed + 4));
                    float pulse = sin((time * lerp(0.35, 0.7, Hash(seed + 6)) + Hash(seed + 8)) * TWO_PI);
                    float top = lerp(0.62, 1, Hash(seed + 10)) * (0.86 + 0.14 * sin(time * lerp(0.9, 1.7, Hash(seed + 12)) + seed * 2.4));
                    float grow = 0.5 + 0.5 * pulse;
                    float3 a = float3(outward.x * baseRadius, 0, outward.y * baseRadius);
                    float3 b = float3(outward.x * baseRadius * 1.07, DESIGN_HEIGHT, outward.y * baseRadius * 1.07);
                    float4 near = NearSegment(origin, direction, rayEnd, a, b);
                    float height = near.y;
                    float fade = smoothstep(top, top * 0.15, height) * lerp(0.35, 1, grow);
                    float flow = 0.75 + 0.25 * sin((height * 2.4 - time * 1.3 + Hash(seed)) * TWO_PI);
                    float amount = exp(-near.x * near.x / (width * width)) * fade * flow * _RayDensity * width * SQRT_PI * near.w;
                    float3 tint = lerp(_EdgeColor.rgb, _TopColor.rgb, smoothstep(0.2, 0.7, height));
                    float edge = smoothstep(outerRadius * 0.45, outerRadius * 0.95, baseRadius);
                    Add(gather, lerp(_CoreColor.rgb, tint, saturate(edge * 0.55 + height * 0.25)), amount, near.z);
                }
            }

            // 밑동에서 위와 바깥으로 곧게 솟는 바늘빛 한 바퀴
            void AddNeedles(inout Gather gather, float3 origin, float3 direction, float rayEnd, int count, float seedBase, float reach, float rise, float width, float time, float outerRadius)
            {
                for (int i = 0; i < count; i++)
                {
                    float seed = i + seedBase;
                    float angle = (i + 0.5 + (Hash(seed) - 0.5) * 0.5) / count * TWO_PI;
                    float2 outward = float2(cos(angle), sin(angle));
                    float phase = frac(time * (0.9 + Hash(seed + 3) * 0.5) + Hash(seed + 5));
                    float size = lerp(0.6, 1, Hash(seed + 7)) * lerp(0.6, 1, sin(phase * PI));
                    float start = outerRadius * 0.8;
                    float3 a = float3(outward.x * start, 0.02, outward.y * start);
                    float3 b = a + float3(outward.x * reach, rise, outward.y * reach) * size * _NeedleLength;
                    float4 near = NearSegment(origin, direction, rayEnd, a, b);
                    float taper = width * _NeedleWidth * pow(saturate(1 - near.y), 0.8) + 0.004;
                    float amount = smoothstep(taper, taper * 0.2, near.x) * (1 - near.y * 0.35) * _NeedleDensity * taper * 1.2 * min(near.w, 1.6);
                    float radius = length((a + (b - a) * near.y).xz);
                    Add(gather, lerp(_CoreColor.rgb, _SplashColor.rgb, saturate((radius - outerRadius * 0.95) / 0.6)), amount, near.z);
                }
            }

            half4 frag(Varyings input, out float outDepth : SV_Depth) : SV_Target
            {
                // 물체 자리 (바닥) 와 높이에서 설계 치수 (3.4 m) 공간으로
                float3 base = float3(UNITY_MATRIX_M._m03, UNITY_MATRIX_M._m13, UNITY_MATRIX_M._m23);
                float worldHeight = length(float3(UNITY_MATRIX_M._m01, UNITY_MATRIX_M._m11, UNITY_MATRIX_M._m21)) * 0.5;
                float unit = worldHeight / DESIGN_HEIGHT;
                float3 direction = -GetWorldSpaceNormalizeViewDir(input.positionWS);
                // 직교 카메라에서도 같은 식을 쓰려고 상자 뒷면에서 시선을 거슬러 시작점을 잡음
                float back = min(length(input.positionWS - _WorldSpaceCameraPos), worldHeight * 8);
                float3 startWS = input.positionWS - direction * back;
                float3 origin = (startWS - base) / unit;
                float rayEnd = back / unit;
                float outerRadius = _Radius * DESIGN_HEIGHT;
                // 포털마다 박자를 다르게
                float time = _Time.y * _Speed + frac(dot(base.xz, float2(0.37, 0.71))) * 17;

                // 바닥과 만나는 곳에서 시선을 끊음. 바닥 아래의 빛은 없음
                bool hitsGround = direction.y < -1e-3;
                float groundDistance = hitsGround ? -origin.y / direction.y : rayEnd;
                rayEnd = min(rayEnd, max(groundDistance, 0));

                // 시선이 축에 가장 가까이 가는 곳. 기둥에 닿지 않는 픽셀은 줄기와 안개를, 밑동에 닿지 않는 픽셀은 바늘을 건너뜀
                float2 flat = direction.xz;
                float flatLength2 = max(dot(flat, flat), 1e-3);
                float nearAxis = clamp(-dot(origin.xz, flat) / flatLength2, 0, rayEnd);
                bool touchesColumn = length(origin.xz + flat * nearAxis) < outerRadius * 1.2 + 0.16;
                float slope = abs(direction.y) < 1e-3 ? 1e-3 : direction.y;
                float lowA = -origin.y / slope;
                float lowB = (NEEDLE_TOP - origin.y) / slope;
                float lowFrom = max(min(lowA, lowB), 0);
                float lowTo = min(max(lowA, lowB), rayEnd + 0.1);
                float nearLow = clamp(nearAxis, lowFrom, max(lowTo, lowFrom));
                bool touchesNeedles = lowTo >= lowFrom && length(origin.xz + flat * nearLow) < outerRadius * 0.8 + 0.72;

                Gather gather = (Gather)0;
                [branch] if (touchesColumn)
                {
                    AddRays(gather, origin, direction, rayEnd, OUTER_RAYS, outerRadius * 0.9, 3, 0.035, 0.085, time, outerRadius);
                    AddRays(gather, origin, direction, rayEnd, INNER_RAYS, outerRadius * 0.52, 33, 0.03, 0.07, time, outerRadius);
                }
                [branch] if (touchesNeedles)
                {
                    AddNeedles(gather, origin, direction, rayEnd, 17, 1.3, 0.62, 0.34, 0.06, time, outerRadius);
                    AddNeedles(gather, origin, direction, rayEnd, 15, 41.7, 0.46, 0.62, 0.06, time, outerRadius);
                    AddNeedles(gather, origin, direction, rayEnd, 12, 81.1, 0.22, 0.86, 0.055, time, outerRadius);
                }

                // 몸통 안개. 축에 가장 가까운 곳 둘레만 짧게 걸음
                [branch] if (touchesColumn)
                {
                    float halfSpan = min(outerRadius * 1.1 / sqrt(flatLength2), DESIGN_HEIGHT);
                    float from = max(nearAxis - halfSpan, 0);
                    float to = min(nearAxis + halfSpan, rayEnd);
                    float stepSize = max(to - from, 0) / HAZE_STEPS;
                    for (int i = 0; i < HAZE_STEPS; i++)
                    {
                        float t = from + (i + 0.5) * stepSize;
                        float3 p = origin + direction * t;
                        float height = saturate(p.y / DESIGN_HEIGHT);
                        float radius = length(p.xz);
                        float fade = smoothstep(0.95, 0.08, height);
                        float ratio = radius / (outerRadius * 0.86);
                        float amount = exp(-ratio * ratio * ratio * ratio) * fade * fade * _HazeDensity * stepSize;
                        float edge = smoothstep(outerRadius * 0.45, outerRadius * 0.95, radius);
                        float3 tint = lerp(_EdgeColor.rgb, _TopColor.rgb, smoothstep(0.2, 0.7, height));
                        Add(gather, lerp(_CoreColor.rgb, tint, saturate(edge * 0.9 + height * 0.4)), amount, t);
                    }
                }

                if (hitsGround && groundDistance > 0)
                {
                    float3 p = origin + direction * groundDistance;
                    float radius = length(p.xz);
                    float slant = max(-direction.y, 0.2);
                    float ratio = radius / (outerRadius * 1.08);
                    float pool = exp(-pow(ratio, 6)) * _PoolDensity / 13 / slant;
                    float glow = exp(-pow(radius / 0.7, 2.5)) * _GroundGlow / 9 / slant;
                    // 바닥 바로 위로 조금 띄워 바닥 면과 깜빡이지 않게
                    float lifted = max(groundDistance - 0.08 / slant, 0.02);
                    Add(gather, lerp(_CoreColor.rgb, _SplashColor.rgb, smoothstep(outerRadius * 0.9, outerRadius * 1.5, radius)), pool, lifted);
                    Add(gather, _SplashColor.rgb, glow, lifted);
                }

                [branch] if (touchesColumn) for (int m = 0; m < MOTES; m++)
                {
                    float life = frac(time * 0.22 * (0.7 + Hash(m + 2) * 0.6) + Hash(m));
                    float angle = Hash(m + 8) * TWO_PI;
                    float radius = outerRadius * lerp(0.55, 1.35, Hash(m + 4));
                    float3 mote = float3(cos(angle) * radius, life * 2.1 + 0.15, sin(angle) * radius) - origin;
                    float t = dot(mote, direction);
                    if (t < 0 || t > rayEnd) continue;
                    float gap = length(mote - direction * t) / max(_MoteSize * lerp(0.6, 1.2, Hash(m + 11)), 1e-4);
                    Add(gather, _CoreColor.rgb, (0.4 + 0.6 * Hash(m + 15)) * exp(-gap * gap) * sin(life * PI) * 2, t);
                }

                float alpha = 1 - exp(-gather.depth);
                clip(alpha - 0.004);
                float3 color = gather.color / max(gather.depth, 1e-4) * _Intensity;
                float meanDistance = max(min(gather.distance / max(gather.depth, 1e-4), rayEnd), 0.05);
                float4 positionCS = TransformWorldToHClip(startWS + direction * meanDistance * unit);
                float depth = positionCS.z / positionCS.w;
                #if !UNITY_REVERSED_Z
                depth = depth * 0.5 + 0.5;
                #endif
                outDepth = saturate(depth);
                color = MixFog(color, input.fogFactor);
                return half4(color * alpha, alpha);
            }
            ENDHLSL
        }
    }
}
