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

                // faceData = (layer, worldScale, 0, 0). layer >= 0 → textured, < 0 → sentinel (vertex color path).
                half hasTexture = step(0.0, IN.faceData.x);

                // worldspace UV → worldScale 나눔 (frac 불요 — array Repeat wrap 이 하드웨어로 mip 정상 처리).
                // worldScale 보장 > 0 (mesher 안전값 1f) — divide-by-zero 없음.
                float2 sampleUV = IN.uv / IN.faceData.y;
                float layerIndex = max(0.0, IN.faceData.x); // 음수 sentinel 은 layer 0 샘플 후 lerp out
                half4 texSample = SAMPLE_TEXTURE2D_ARRAY(_MainTex, sampler_MainTex, sampleUV, layerIndex);

                // textured 면 = vertex color × texture, sentinel = vertex color × 1 (texture 무시)
                half4 textureMod = lerp(half4(1, 1, 1, 1), texSample, hasTexture);
                return IN.color * textureMod * _BaseColor * diffuse;
            }
            ENDHLSL
        }
    }
}
