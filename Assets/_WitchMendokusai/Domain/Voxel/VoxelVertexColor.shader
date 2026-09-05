Shader "WM/VoxelVertexColor"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (1,1,1,1)
        [NoScaleOffset] _MainTex("Voxel Texture Array", 2DArray) = "" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR;
                // mesher worldspace UV — face 방향 평면 좌표 (큰 절대값. worldScale 나눈 뒤 하드웨어 Repeat wrap)
                float2 uv : TEXCOORD0;
                // (layer, worldScale, 0, 0). layer < 0 = sentinel (텍스쳐 미할당)
                float4 faceData : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float4 color : COLOR;
                float2 uv : TEXCOORD1;
                float4 faceData : TEXCOORD2;
            };

            TEXTURE2D_ARRAY(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
            CBUFFER_END

            // ── Stochastic (hex-tiling) 샘플링 — Heitz-Neyret "tiling & blending" ──
            // 텍스쳐 반복 패턴을 영역별 랜덤 오프셋 + barycentric 블렌드로 분해.
            // worldScale 작아(고밀도=선명) 반복이 잦아도 그리드가 안 보이게.
            float2 HexHash(float2 p)
            {
                float2 r = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
                return frac(sin(r) * 43758.5453);
            }

            // 삼각 격자 — uv 가 속한 셀의 3 꼭짓점 + 가중치.
            void HexTriangleGrid(float2 uv, out float w1, out float w2, out float w3, out float2 v1, out float2 v2, out float2 v3)
            {
                float2 skewed = float2(uv.x - uv.y * 0.57735027, uv.y * 1.15470054);
                float2 baseId = floor(skewed);
                float3 temp = float3(frac(skewed), 0.0);
                temp.z = 1.0 - temp.x - temp.y;
                if (temp.z > 0.0)
                {
                    w1 = temp.z; w2 = temp.y; w3 = temp.x;
                    v1 = baseId; v2 = baseId + float2(0.0, 1.0); v3 = baseId + float2(1.0, 0.0);
                }
                else
                {
                    w1 = -temp.z; w2 = 1.0 - temp.y; w3 = 1.0 - temp.x;
                    v1 = baseId + float2(1.0, 1.0); v2 = baseId + float2(1.0, 0.0); v3 = baseId + float2(0.0, 1.0);
                }
            }

            // 3 탭 (셀별 랜덤 오프셋) + 가중 블렌드. mip 은 원본 uv 의 미분 (dx, dy) 으로 grad 샘플 — frac 불연속 회피.
            half4 SampleHexTiling(float2 uv, float layer, float2 dx, float2 dy)
            {
                float w1, w2, w3;
                float2 v1, v2, v3;
                HexTriangleGrid(uv, w1, w2, w3, v1, v2, v3);
                half4 c1 = SAMPLE_TEXTURE2D_ARRAY_GRAD(_MainTex, sampler_MainTex, uv + HexHash(v1), layer, dx, dy);
                half4 c2 = SAMPLE_TEXTURE2D_ARRAY_GRAD(_MainTex, sampler_MainTex, uv + HexHash(v2), layer, dx, dy);
                half4 c3 = SAMPLE_TEXTURE2D_ARRAY_GRAD(_MainTex, sampler_MainTex, uv + HexHash(v3), layer, dx, dy);
                float weightSum = max(w1 + w2 + w3, 1e-4);
                return (w1 * c1 + w2 * c2 + w3 * c3) / weightSum;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.color = IN.color;
                OUT.uv = IN.uv;
                OUT.faceData = IN.faceData;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                Light mainLight = GetMainLight();
                half NdotL = saturate(dot(normalize(IN.normalWS), mainLight.direction));
                half ambient = 0.3; // 너무 어둡지 않게 기본 앰비언트 0.3
                half diffuse = ambient + (NdotL * (1.0 - ambient));

                // faceData = (layer, worldScale, stochastic, 0). layer >= 0 → textured, < 0 → sentinel (vertex color path).
                half hasTexture = step(0.0, IN.faceData.x);

                // worldspace UV → worldScale 나눔 (array Repeat wrap 이 하드웨어로 mip 정상 처리).
                // worldScale 보장 > 0 (mesher 안전값 1f) — divide-by-zero 없음.
                float2 sampleUV = IN.uv / IN.faceData.y;
                float2 dx = ddx(sampleUV);
                float2 dy = ddy(sampleUV);
                float layerIndex = max(0.0, IN.faceData.x); // 음수 sentinel 은 layer 0 샘플 후 lerp out

                // stochastic 토글 (faceData.z): ON = hex-tiling(반복 분해), OFF = plain (마인크래프트식).
                // 면 단위 coherent 분기 — plain 블록은 3 탭 비용 회피.
                half4 texSample;
                if (IN.faceData.z > 0.5)
                    texSample = SampleHexTiling(sampleUV, layerIndex, dx, dy);
                else
                    texSample = SAMPLE_TEXTURE2D_ARRAY_GRAD(_MainTex, sampler_MainTex, sampleUV, layerIndex, dx, dy);

                // textured 면 = vertex color × texture, sentinel = vertex color × 1 (texture 무시)
                half4 textureMod = lerp(half4(1, 1, 1, 1), texSample, hasTexture);
                return IN.color * textureMod * _BaseColor * diffuse;
            }
            ENDHLSL
        }
    }
}
