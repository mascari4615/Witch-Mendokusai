// 가벼운 lit 면 + 캐릭터 가림 컷아웃 (WMOcclusionCutout.hlsl)
// 텍스처, 색, 알파 잘라내기, 발광, 금속도와 매끈함, 주광 그림자 받기, 추가 광원, 안개. 노멀맵, 라이트맵, 세부 맵 없음
// 속성 이름은 URP Lit 과 같음. 재질 셰이더만 바꿔도 값 유지
// 컷아웃은 재질의 Occludable 켜기 값 (_WM_OCCLUDABLE). 바닥처럼 발밑 면은 끔
Shader "WM/Occludable Lit"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base map", 2D) = "white" {}
        [MainColor] _BaseColor ("Base color", Color) = (1, 1, 1, 1)
        _Metallic ("Metallic", Range(0, 1)) = 0
        _Smoothness ("Smoothness", Range(0, 1)) = 0.1
        _Cutoff ("Alpha cutoff", Range(0, 1)) = 0.5
        [Toggle(_ALPHATEST_ON)] _AlphaClip ("Alpha clip", Float) = 0
        [Toggle(_VERTEX_COLOR)] _VertexColor ("Vertex color tint (0.5 is unchanged)", Float) = 0
        _AmbientBounce ("Warm bounce in shade (replaces sky tint)", Range(0, 1)) = 0
        _AmbientBounceColor ("Bounce color", Color) = (1.4, 1.28, 1.12, 1)
        [ToggleOff(_SPECULARHIGHLIGHTS_OFF)] _SpecularHighlights ("Specular highlights", Float) = 1
        [ToggleOff(_ENVIRONMENTREFLECTIONS_OFF)] _EnvironmentReflections ("Environment reflections", Float) = 1
        [HDR] _EmissionColor ("Emission color", Color) = (0, 0, 0, 1)
        _EmissionMap ("Emission map", 2D) = "white" {}
        [Toggle(_EMISSION)] _EmissionOn ("Emission", Float) = 0
        [Toggle(_WM_OCCLUDABLE)] _WM_Occludable ("Occludable (cut out in front of the character)", Float) = 1
        [HideInInspector] _WM_ObjectFade ("Object fade (set per renderer)", Float) = 0
        [HideInInspector] _Cull ("Cull", Float) = 2
        [HideInInspector] _Surface ("Surface", Float) = 0
        [HideInInspector] _SrcBlend ("Src blend", Float) = 1
        [HideInInspector] _DstBlend ("Dst blend", Float) = 0
        [HideInInspector] _SrcBlendAlpha ("Src blend alpha", Float) = 1
        [HideInInspector] _DstBlendAlpha ("Dst blend alpha", Float) = 0
        [HideInInspector] _ZWrite ("Z write", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "WMOcclusionCutout.hlsl"
        #include "WMWind.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);
        TEXTURE2D(_EmissionMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _Metallic;
            half _Smoothness;
            half _Cutoff;
            half4 _EmissionColor;
            half _WM_ObjectFade;
            half _AmbientBounce;
            half4 _AmbientBounceColor;
        CBUFFER_END

        // 원과 카메라 앞 비움 + 물체 단위 페이드 (OcclusionFadeTarget 이 렌더러마다 넣는 _WM_ObjectFade)
        float Coverage(float4 screenPosition, float3 positionWS)
        {
            float coverage = WMCutoutCoverage(screenPosition, positionWS);
            #if defined(_WM_OCCLUDABLE)
                coverage = max(coverage, _WM_ObjectFade);
            #endif
            return coverage;
        }

        // 잎 판 (_ALPHATEST_ON) 만 바람에 흔들림
        float3 SwayedPosition(float3 positionOS)
        {
            float3 positionWS = TransformObjectToWorld(positionOS);
            #if defined(_ALPHATEST_ON)
                positionWS += WMWind(positionWS, 1.0);
            #endif
            return positionWS;
        }

        // 알파 잘라내기 (잎 판처럼 _ALPHATEST_ON 재질)
        void AlphaTest(float2 uv)
        {
            #if defined(_ALPHATEST_ON)
                clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).a * _BaseColor.a - _Cutoff);
            #endif
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend [_SrcBlend] [_DstBlend], [_SrcBlendAlpha] [_DstBlendAlpha]
            ZWrite [_ZWrite]
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _WM_OCCLUDABLE
            #pragma shader_feature_local _VERTEX_COLOR
            #pragma shader_feature_local_fragment _EMISSION
            #pragma shader_feature_local_fragment _SURFACE_TYPE_TRANSPARENT
            #pragma shader_feature_local_fragment _ALPHAPREMULTIPLY_ON
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local_fragment _SPECULARHIGHLIGHTS_OFF
            #pragma shader_feature_local_fragment _ENVIRONMENTREFLECTIONS_OFF
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                #if defined(_VERTEX_COLOR)
                    half4 color : COLOR;
                #endif
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float4 screenPosition : TEXCOORD3;
                float fogFactor : TEXCOORD4;
                #if defined(_VERTEX_COLOR)
                    half3 tint : TEXCOORD5;
                #endif
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionWS = SwayedPosition(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.screenPosition = ComputeScreenPos(output.positionCS);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                #if defined(_VERTEX_COLOR)
                    output.tint = input.color.rgb * 2.0;
                #endif
                return output;
            }

            half4 frag(Varyings input, bool isFrontFace : SV_IsFrontFace) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half4 base = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                #if defined(_VERTEX_COLOR)
                    base.rgb *= input.tint;
                #endif
                #if defined(_ALPHATEST_ON)
                    clip(base.a - _Cutoff);
                #endif
                float coverage = Coverage(input.screenPosition, input.positionWS);
                #if defined(_SURFACE_TYPE_TRANSPARENT)
                    base.a *= 1.0 - coverage;
                #else
                    WMCutoutClip(coverage, input.positionCS.xy);
                #endif

                float3 normalWS = normalize(input.normalWS);
                normalWS = isFrontFace ? normalWS : -normalWS;

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.positionCS = input.positionCS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                inputData.fogCoord = input.fogFactor;
                half3 ambient = SampleSH(normalWS);
                inputData.bakedGI = lerp(ambient, Luminance(ambient) * _AmbientBounceColor.rgb, _AmbientBounce);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = base.rgb;
                surface.alpha = base.a;
                surface.metallic = _Metallic;
                surface.smoothness = _Smoothness;
                surface.occlusion = 1;
                surface.normalTS = half3(0, 0, 1);
                #if defined(_EMISSION)
                    surface.emission = SAMPLE_TEXTURE2D(_EmissionMap, sampler_BaseMap, input.uv).rgb * _EmissionColor.rgb;
                #endif

                half4 color = UniversalFragmentPBR(inputData, surface);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                #if defined(_SURFACE_TYPE_TRANSPARENT)
                    color.a = surface.alpha;
                #else
                    color.a = 1;
                #endif
                return color;
            }
            ENDHLSL
        }

        // 그림자 넣기. 컷아웃 없음 (비운 자리도 그림자는 그대로)
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vertShadow
            #pragma fragment fragShadow
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            ShadowVaryings vertShadow(ShadowAttributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionWS = SwayedPosition(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                ShadowVaryings output;
                output.positionCS = positionCS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 fragShadow(ShadowVaryings input) : SV_Target
            {
                AlphaTest(input.uv);
                return 0;
            }
            ENDHLSL
        }

        // 깊이 텍스처. 비운 자리는 깊이도 비움 (뒤 캐릭터가 깊이 비교로 가려지지 않게)
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vertDepth
            #pragma fragment fragDepth
            #pragma shader_feature_local _WM_OCCLUDABLE
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_instancing

            struct DepthAttributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct DepthVaryings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float4 screenPosition : TEXCOORD1;
                float2 uv : TEXCOORD2;
            };

            DepthVaryings vertDepth(DepthAttributes input)
            {
                DepthVaryings output;
                UNITY_SETUP_INSTANCE_ID(input);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.positionWS = SwayedPosition(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.screenPosition = ComputeScreenPos(output.positionCS);
                return output;
            }

            half fragDepth(DepthVaryings input) : SV_Target
            {
                AlphaTest(input.uv);
                WMCutoutClip(Coverage(input.screenPosition, input.positionWS), input.positionCS.xy);
                return input.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vertDepthNormals
            #pragma fragment fragDepthNormals
            #pragma shader_feature_local _WM_OCCLUDABLE
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_instancing

            struct DepthNormalsAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct DepthNormalsVaryings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float4 screenPosition : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float2 uv : TEXCOORD3;
            };

            DepthNormalsVaryings vertDepthNormals(DepthNormalsAttributes input)
            {
                DepthNormalsVaryings output;
                UNITY_SETUP_INSTANCE_ID(input);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.positionWS = SwayedPosition(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.screenPosition = ComputeScreenPos(output.positionCS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 fragDepthNormals(DepthNormalsVaryings input, bool isFrontFace : SV_IsFrontFace) : SV_Target
            {
                AlphaTest(input.uv);
                WMCutoutClip(Coverage(input.screenPosition, input.positionWS), input.positionCS.xy);
                float3 normalWS = normalize(input.normalWS);
                normalWS = isFrontFace ? normalWS : -normalWS;
                return half4(NormalizeNormalPerPixel(normalWS), 0.0);
            }
            ENDHLSL
        }
    }
}
