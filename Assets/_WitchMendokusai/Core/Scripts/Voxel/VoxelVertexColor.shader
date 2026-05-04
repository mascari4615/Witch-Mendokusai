Shader "WM/VoxelVertexColor"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (1,1,1,1)
        _MainTex("Atlas (RGBA)", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR;
                // mesher worldspace UV — face 방향에 맞는 평면 좌표 (큰 절대값. 셰이더가 frac 으로 wrap)
                float2 uv : TEXCOORD0;
                // atlas 슬롯 + 반복 주기 — (xMin, yMin, atlasSize, worldScale). atlasSize=0 = sentinel
                float4 tileRect : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float4 color : COLOR;
                float2 uv : TEXCOORD1;
                float4 tileRect : TEXCOORD2;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float4 _MainTex_ST;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.color = IN.color;
                OUT.uv = IN.uv;
                OUT.tileRect = IN.tileRect;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                Light mainLight = GetMainLight();
                half NdotL = saturate(dot(normalize(IN.normalWS), mainLight.direction));
                half ambient = 0.3; // 너무 어둡지 않게 기본 앰비언트 0.3
                half diffuse = ambient + (NdotL * (1.0 - ambient));

                // tileRect = (atlas xMin, yMin, atlasSize, worldScale).
                // atlasSize > 0 → atlas 면, == 0 → sentinel (텍스쳐 미할당) → vertex color path.
                half hasAtlas = step(0.0001, IN.tileRect.z);

                // worldspace UV → frac wrap (worldScale m 마다 반복) → atlas tile rect 안 매핑.
                // worldScale 보장 > 0 (mesher 안전값 1f 처리) — divide-by-zero 없음.
                float2 wrappedUV = frac(IN.uv / IN.tileRect.w);
                float2 atlasUV = IN.tileRect.xy + wrappedUV * IN.tileRect.z;
                half4 atlasSample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, atlasUV);

                // atlas 면 = vertex color × atlas, sentinel = vertex color × 1 (atlas 무시)
                half4 textureMod = lerp(half4(1, 1, 1, 1), atlasSample, hasAtlas);
                return IN.color * textureMod * _BaseColor * diffuse;
            }
            ENDHLSL
        }
    }
}
