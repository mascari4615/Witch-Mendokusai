// 궁극기 3D 연출의 실제 메시용 재질 (검, 솟는 바위, 메시 파편)
// - 빛 받음: 주광 (그림자 포함) + 추가 광원 (점광, 스포트) + 환경광. 연출 광원이 메시를 실제로 비춤
// - 프레넬 테두리 빛, 자체 발광, 금속 반사 한 줄
// - 디졸브: 물체 공간 높이 + 3D 잡음으로 화소를 버림. _Dissolve 0 이면 온전, 1 이면 전부 사라짐. 경계에 발광 띠
// - 꼭짓점 색 곱하기 (_UseVertexColor 1): 입자 메시 모드에서 입자 색을 받음
// - 그림자 투사 패스와 깊이 패스 포함 (디졸브 반영)
Shader "WM/Fx/UltimateMeshGlow"
{
	Properties
	{
		_BaseColor ("Base Color", Color) = (0.7, 0.7, 0.75, 1)
		[HDR] _EmissionColor ("Emission", Color) = (0, 0, 0, 1)
		[HDR] _RimColor ("Rim", Color) = (0, 0, 0, 1)
		_RimPower ("Rim Power", Range(0.5, 8)) = 3
		_Specular ("Specular", Range(0, 1)) = 0.4
		_Smoothness ("Smoothness", Range(1, 128)) = 32
		_UseVertexColor ("Use Vertex Color", Range(0, 1)) = 0
		_Dissolve ("Dissolve", Range(0, 1)) = 0
		[HDR] _DissolveEdgeColor ("Dissolve Edge", Color) = (2, 1.6, 1, 1)
		_DissolveEdgeWidth ("Dissolve Edge Width", Range(0, 0.3)) = 0.06
		_DissolveRange ("Dissolve Height Range (min, max)", Vector) = (0, 1, 0, 0)
		_DissolveDirection ("Dissolve Direction (1 아래부터, -1 위부터)", Float) = 1
		_NoiseScale ("Noise Scale", Float) = 6
	}

	HLSLINCLUDE
	#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

	CBUFFER_START(UnityPerMaterial)
		float4 _BaseColor;
		float4 _EmissionColor;
		float4 _RimColor;
		float _RimPower;
		float _Specular;
		float _Smoothness;
		float _UseVertexColor;
		float _Dissolve;
		float4 _DissolveEdgeColor;
		float _DissolveEdgeWidth;
		float4 _DissolveRange;
		float _DissolveDirection;
		float _NoiseScale;
	CBUFFER_END

	float Hash3(float3 cell)
	{
		cell = frac(cell * 0.3183099 + 0.1);
		cell *= 17.0;
		return frac(cell.x * cell.y * cell.z * (cell.x + cell.y + cell.z));
	}

	float ValueNoise(float3 position)
	{
		float3 cell = floor(position);
		float3 fraction = frac(position);
		fraction = fraction * fraction * (3.0 - 2.0 * fraction);
		float bottom = lerp(lerp(Hash3(cell), Hash3(cell + float3(1, 0, 0)), fraction.x), lerp(Hash3(cell + float3(0, 1, 0)), Hash3(cell + float3(1, 1, 0)), fraction.x), fraction.y);
		float top = lerp(lerp(Hash3(cell + float3(0, 0, 1)), Hash3(cell + float3(1, 0, 1)), fraction.x), lerp(Hash3(cell + float3(0, 1, 1)), Hash3(cell + float3(1, 1, 1)), fraction.x), fraction.y);
		return lerp(bottom, top, fraction.z);
	}

	// 0 미만이면 버릴 화소. edge 는 경계 띠 세기 (0~1)
	float DissolveMask(float3 positionOS, out float edge)
	{
		float height = saturate((positionOS.y - _DissolveRange.x) / max(0.0001, _DissolveRange.y - _DissolveRange.x));
		height = _DissolveDirection > 0 ? height : 1.0 - height;
		// 높이 0.65 + 잡음 0.35 = 0~1. _Dissolve 0 이면 전부 남고 1 이면 전부 사라짐
		float noise = ValueNoise(positionOS * _NoiseScale) * 0.35;
		float value = (height * 0.65 + noise + 0.0001) - (_Dissolve * 1.0001);
		edge = 1.0 - saturate(value / max(0.0001, _DissolveEdgeWidth));
		edge *= step(0.0001, _Dissolve);
		return value;
	}
	ENDHLSL

	SubShader
	{
		Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

		Pass
		{
			Name "ForwardLit"
			Tags { "LightMode" = "UniversalForward" }
			Cull Back
			ZWrite On

			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment Frag
			#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
			#pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
			#pragma multi_compile_fragment _ _SHADOWS_SOFT
			#pragma multi_compile_fog
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

			struct Attributes
			{
				float4 positionOS : POSITION;
				float3 normalOS : NORMAL;
				float4 color : COLOR;
			};

			struct Varyings
			{
				float4 positionCS : SV_POSITION;
				float3 positionWS : TEXCOORD0;
				float3 normalWS : TEXCOORD1;
				float3 positionOS : TEXCOORD2;
				float4 color : COLOR;
				float fog : TEXCOORD3;
			};

			Varyings Vert(Attributes input)
			{
				Varyings output;
				VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
				output.positionCS = position.positionCS;
				output.positionWS = position.positionWS;
				output.normalWS = TransformObjectToWorldNormal(input.normalOS);
				output.positionOS = input.positionOS.xyz;
				output.color = input.color;
				output.fog = ComputeFogFactor(position.positionCS.z);
				return output;
			}

			float3 Shade(Light light, float3 normal, float3 viewDirection, float3 albedo)
			{
				float attenuation = light.distanceAttenuation * light.shadowAttenuation;
				float diffuse = saturate(dot(normal, light.direction));
				float3 halfVector = SafeNormalize(light.direction + viewDirection);
				float specular = pow(saturate(dot(normal, halfVector)), _Smoothness) * _Specular;
				return light.color * attenuation * (albedo * diffuse + specular);
			}

			float4 Frag(Varyings input) : SV_Target
			{
				float edge;
				clip(DissolveMask(input.positionOS, edge));
				float3 normal = normalize(input.normalWS);
				float3 viewDirection = normalize(GetWorldSpaceViewDir(input.positionWS));
				float3 albedo = _BaseColor.rgb * lerp(float3(1, 1, 1), input.color.rgb, _UseVertexColor);

				float3 color = albedo * SampleSH(normal);
				Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
				color += Shade(mainLight, normal, viewDirection, albedo);
				#if defined(_ADDITIONAL_LIGHTS)
				uint count = GetAdditionalLightsCount();
				for (uint index = 0u; index < count; ++index)
				{
					Light light = GetAdditionalLight(index, input.positionWS);
					color += Shade(light, normal, viewDirection, albedo);
				}
				#endif

				float rim = pow(1.0 - saturate(dot(normal, viewDirection)), _RimPower);
				color += _RimColor.rgb * rim;
				color += _EmissionColor.rgb * lerp(1.0, input.color.a, _UseVertexColor);
				color += _DissolveEdgeColor.rgb * edge;
				// 광원 바로 옆 화소가 반정밀도 한계를 넘어 빛 번짐에 NaN 이 번지지 않게
				color = min(color, 40.0);
				color = MixFog(color, input.fog);
				return float4(color, 1);
			}
			ENDHLSL
		}

		Pass
		{
			Name "ShadowCaster"
			Tags { "LightMode" = "ShadowCaster" }
			ZWrite On
			ZTest LEqual
			ColorMask 0
			Cull Back

			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment Frag
			#pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

			float3 _LightDirection;
			float3 _LightPosition;

			struct Attributes
			{
				float4 positionOS : POSITION;
				float3 normalOS : NORMAL;
			};

			struct Varyings
			{
				float4 positionCS : SV_POSITION;
				float3 positionOS : TEXCOORD0;
			};

			Varyings Vert(Attributes input)
			{
				Varyings output;
				float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
				float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
				#if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
				float3 lightDirection = normalize(_LightPosition - positionWS);
				#else
				float3 lightDirection = _LightDirection;
				#endif
				float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirection));
				#if UNITY_REVERSED_Z
				positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
				#else
				positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
				#endif
				output.positionCS = positionCS;
				output.positionOS = input.positionOS.xyz;
				return output;
			}

			float4 Frag(Varyings input) : SV_Target
			{
				float edge;
				clip(DissolveMask(input.positionOS, edge));
				return 0;
			}
			ENDHLSL
		}

		Pass
		{
			Name "DepthOnly"
			Tags { "LightMode" = "DepthOnly" }
			ZWrite On
			ColorMask R
			Cull Back

			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment Frag

			struct Attributes
			{
				float4 positionOS : POSITION;
			};

			struct Varyings
			{
				float4 positionCS : SV_POSITION;
				float3 positionOS : TEXCOORD0;
			};

			Varyings Vert(Attributes input)
			{
				Varyings output;
				output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
				output.positionOS = input.positionOS.xyz;
				return output;
			}

			float4 Frag(Varyings input) : SV_Target
			{
				float edge;
				clip(DissolveMask(input.positionOS, edge));
				return input.positionCS.z;
			}
			ENDHLSL
		}
	}
}
