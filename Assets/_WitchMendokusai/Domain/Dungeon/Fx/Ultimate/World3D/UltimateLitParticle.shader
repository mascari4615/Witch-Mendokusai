// 궁극기 3D 연출의 빛 받는 입자 (먼지, 연기). 연출 광원과 주광이 입자를 실제로 비춤
// - 법선 대신 카메라 쪽과 위쪽의 중간 방향, 감싼 램버트 (반대편도 절반 밝기). 연기가 광원 쪽만 밝아짐
// - 텍스처 알파 x 꼭짓점 색 (입자 색). 알파 합성, 깊이 쓰기 없음
// - 소프트 입자: 카메라 깊이 텍스처로 바닥과 맞닿은 가장자리를 녹임. 깊이 텍스처가 없으면 _SoftDistance 0 으로 끔
Shader "WM/Fx/UltimateLitParticle"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
		_BaseColor ("Base Color", Color) = (1, 1, 1, 1)
		[HDR] _EmissionColor ("Emission", Color) = (0, 0, 0, 1)
		_Wrap ("Wrap", Range(0, 1)) = 0.5
		_SoftDistance ("Soft Distance", Float) = 0.6
	}

	SubShader
	{
		Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

		Pass
		{
			Name "ForwardLit"
			Tags { "LightMode" = "UniversalForward" }
			Blend SrcAlpha OneMinusSrcAlpha
			ZWrite Off
			Cull Off

			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment Frag
			#pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
			#pragma multi_compile_fog
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

			TEXTURE2D(_MainTex);
			SAMPLER(sampler_MainTex);
			CBUFFER_START(UnityPerMaterial)
				float4 _MainTex_ST;
				float4 _BaseColor;
				float4 _EmissionColor;
				float _Wrap;
				float _SoftDistance;
			CBUFFER_END

			struct Attributes
			{
				float4 positionOS : POSITION;
				float4 color : COLOR;
				float2 uv : TEXCOORD0;
			};

			struct Varyings
			{
				float4 positionCS : SV_POSITION;
				float4 color : COLOR;
				float2 uv : TEXCOORD0;
				float3 positionWS : TEXCOORD1;
				float4 screenPosition : TEXCOORD2;
				float fog : TEXCOORD3;
			};

			Varyings Vert(Attributes input)
			{
				Varyings output;
				VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
				output.positionCS = position.positionCS;
				output.positionWS = position.positionWS;
				output.screenPosition = ComputeScreenPos(position.positionCS);
				output.color = input.color;
				output.uv = TRANSFORM_TEX(input.uv, _MainTex);
				output.fog = ComputeFogFactor(position.positionCS.z);
				return output;
			}

			float Wrapped(float3 normal, float3 direction)
			{
				return saturate((dot(normal, direction) + _Wrap) / (1.0 + _Wrap));
			}

			float4 Frag(Varyings input) : SV_Target
			{
				float4 texel = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
				float4 color = texel * input.color * _BaseColor;
				float3 normal = normalize(normalize(GetWorldSpaceViewDir(input.positionWS)) + float3(0, 1, 0));

				float3 light = SampleSH(normal);
				Light mainLight = GetMainLight();
				light += mainLight.color * Wrapped(normal, mainLight.direction);
				#if defined(_ADDITIONAL_LIGHTS)
				uint count = GetAdditionalLightsCount();
				for (uint index = 0u; index < count; ++index)
				{
					Light extra = GetAdditionalLight(index, input.positionWS);
					// 연기 속 광원은 방향보다 거리가 중요. 감싼 정도를 더 크게
					light += extra.color * extra.distanceAttenuation * (0.35 + 0.65 * Wrapped(normal, extra.direction));
				}
				#endif
				color.rgb = min(color.rgb * light + _EmissionColor.rgb * color.a, 40.0);

				if (_SoftDistance > 0.0)
				{
					float2 uv = input.screenPosition.xy / input.screenPosition.w;
					float sceneDepth = LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
					float particleDepth = LinearEyeDepth(input.positionCS.z, _ZBufferParams);
					color.a *= saturate((sceneDepth - particleDepth) / _SoftDistance);
				}
				color.rgb = MixFog(color.rgb, input.fog);
				return color;
			}
			ENDHLSL
		}
	}
}
