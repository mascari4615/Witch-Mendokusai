// Meadow grass blades drawn with GPU instancing (MeadowGrass.cs repeats one patch mesh around the camera).
// Colour and height come from world space noise so the repeated patch does not read as a pattern.
// Vertex colour alpha: 0 at the root, 1 at the tip. UV0: blade root in patch space, used to scale the blade height.
Shader "WM/MeadowGrass"
{
    Properties
    {
        _Noise ("Noise (r low, g detail)", 2D) = "gray" {}
        _RootColor ("Root", Color) = (0.12, 0.35, 0.05, 1)
        _RootColorB ("Root B", Color) = (0.17, 0.42, 0.07, 1)
        _TipColor ("Tip", Color) = (0.44, 0.72, 0.11, 1)
        _TipColorB ("Tip B", Color) = (0.55, 0.8, 0.14, 1)
        _ClumpScale ("Clump noise scale", Float) = 0.12
        _HueScale ("Hue noise scale", Float) = 0.04
        _ShadeRange ("Clump shade min, max", Vector) = (0.5, 1.05, 0, 0)
        _HeightRange ("Height scale min, max", Vector) = (0.6, 1.25, 0, 0)
        _Wrap ("Light wrap", Float) = 0.45
        _ShadowFloor ("Shadowed brightness", Range(0, 1)) = 0.55
    }
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
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_Noise); SAMPLER(sampler_Noise);
            CBUFFER_START(UnityPerMaterial)
            float4 _RootColor; float4 _RootColorB; float4 _TipColor; float4 _TipColorB;
            float _ClumpScale; float _HueScale; float4 _ShadeRange; float4 _HeightRange; float _Wrap; float _ShadowFloor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 rootOS : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 color : TEXCOORD1;
                float tip : TEXCOORD2;
                float fogFactor : TEXCOORD3;
            };

            Varyings vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output;
                float3 rootWS = TransformObjectToWorld(float3(input.rootOS.x, 0, input.rootOS.y));
                float clump = SAMPLE_TEXTURE2D_LOD(_Noise, sampler_Noise, rootWS.xz * _ClumpScale, 0).r;
                float hue = SAMPLE_TEXTURE2D_LOD(_Noise, sampler_Noise, rootWS.xz * _HueScale + 0.17, 0).g;
                float heightScale = lerp(_HeightRange.x, _HeightRange.y, clump);
                float3 offsetOS = input.positionOS.xyz - float3(input.rootOS.x, 0, input.rootOS.y);
                float3 positionOS = float3(input.rootOS.x, 0, input.rootOS.y) + offsetOS * heightScale;
                VertexPositionInputs positions = GetVertexPositionInputs(positionOS);
                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                float shade = lerp(_ShadeRange.x, _ShadeRange.y, saturate(clump * 1.4 - 0.2));
                float3 root = lerp(_RootColor.rgb, _RootColorB.rgb, hue);
                float3 tip = lerp(_TipColor.rgb, _TipColorB.rgb, hue);
                output.color = lerp(root, tip, input.color.a) * shade;
                output.tip = input.color.a;
                output.fogFactor = ComputeFogFactor(positions.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 normalWS = float3(0, 1, 0);
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                float diffuse = saturate((dot(normalWS, mainLight.direction) + _Wrap) / (1 + _Wrap));
                float shadow = lerp(_ShadowFloor, 1.0, mainLight.shadowAttenuation);
                float3 lit = input.color * (mainLight.color * diffuse * shadow * lerp(0.75, 1.1, input.tip) + SampleSH(normalWS));
                return half4(MixFog(lit, input.fogFactor), 1);
            }
            ENDHLSL
        }
    }
}
