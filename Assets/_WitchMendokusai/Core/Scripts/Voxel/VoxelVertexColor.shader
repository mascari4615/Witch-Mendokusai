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
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float4 color : COLOR;
                float2 uv : TEXCOORD1;
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
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                Light mainLight = GetMainLight();
                half NdotL = saturate(dot(normalize(IN.normalWS), mainLight.direction));
                half ambient = 0.3; // 너무 어둡지 않게 기본 앰비언트 0.3
                half diffuse = ambient + (NdotL * (1.0 - ambient));

                // mesher 가 atlas tile 못 찾으면 UV (-1,-1) sentinel emit → vertex color path.
                // step(0, uv.x) = 1 when uv.x >= 0 (atlas 면), 0 when sentinel.
                half hasAtlas = step(0, IN.uv.x);
                half4 atlasSample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                // hasAtlas=0: 1 (atlas 무시) → vertex color * base * diffuse 그대로
                // hasAtlas=1: atlasSample → vertex color (white 셋) * atlas * base * diffuse — biome tint 등 vertex color path 유지
                half4 textureMod = lerp(half4(1, 1, 1, 1), atlasSample, hasAtlas);
                return IN.color * textureMod * _BaseColor * diffuse;
            }
            ENDHLSL
        }
    }
}
