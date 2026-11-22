Shader "WM/MasteryGem"
{
	// 숙련 조각 보석. 단색 발광 대신 면마다 세 단 색 (어두운 아래, 중간, 밝은 위), 가장자리 빛, 면을 훑는 반짝 띠
	// 면 음영은 메시 정점 색 r (CrystalClusterMesh), 숨쉬기는 _Pulse (MasteryShardObject 가 MaterialPropertyBlock 으로)
	Properties
	{
		_DeepColor ("Deep (그늘 면)", Color) = (0.8, 0.38, 0.05, 1)
		_MidColor ("Mid", Color) = (1, 0.76, 0.16, 1)
		_BrightColor ("Bright (빛 받는 면)", Color) = (1, 0.97, 0.72, 1)
		_BandLow ("Deep 와 Mid 경계", Range(0, 1)) = 0.42
		_BandHigh ("Mid 와 Bright 경계", Range(0, 1)) = 0.72
		_BandSoftness ("경계 부드러움", Range(0.001, 0.3)) = 0.04
		_RimColor ("Rim (가장자리 빛, a 세기)", Color) = (1, 1, 0.9, 0.8)
		_RimPower ("Rim power", Range(0.5, 8)) = 3
		_GlintColor ("Glint (훑는 띠, a 세기)", Color) = (1, 1, 1, 0.9)
		_GlintWidth ("Glint width", Range(0.01, 0.5)) = 0.07
		_GlintPeriod ("Glint period (초)", Float) = 2.4
		_Glow ("Glow (HDR 배율)", Float) = 1.25
		_Pulse ("Pulse (코드가 씀)", Float) = 0
	}
	SubShader
	{
		Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }
		Pass
		{
			Tags { "LightMode"="UniversalForward" }
			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment Frag
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			CBUFFER_START(UnityPerMaterial)
				float4 _DeepColor;
				float4 _MidColor;
				float4 _BrightColor;
				float _BandLow;
				float _BandHigh;
				float _BandSoftness;
				float4 _RimColor;
				float _RimPower;
				float4 _GlintColor;
				float _GlintWidth;
				float _GlintPeriod;
				float _Glow;
				float _Pulse;
			CBUFFER_END
			struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 color : COLOR; };
			struct Varyings
			{
				float4 positionCS : SV_POSITION;
				float3 normalWS : TEXCOORD0;
				float3 positionWS : TEXCOORD1;
				float3 positionOS : TEXCOORD2;
				float shade : TEXCOORD3;
			};
			Varyings Vert(Attributes input)
			{
				Varyings output;
				output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
				output.positionCS = TransformWorldToHClip(output.positionWS);
				output.normalWS = TransformObjectToWorldNormal(input.normalOS);
				output.positionOS = input.positionOS.xyz;
				output.shade = input.color.r;
				return output;
			}
			float4 Frag(Varyings input) : SV_Target
			{
				float3 normal = normalize(input.normalWS);
				float3 view = normalize(GetWorldSpaceViewDir(input.positionWS));
				Light mainLight = GetMainLight();
				// 빛 방향과 보는 방향을 섞어 돌 때 면 색이 바뀌게
				float facing = saturate(dot(normal, mainLight.direction) * 0.35 + dot(normal, view) * 0.45 + 0.35) * input.shade;
				float3 color = lerp(_DeepColor.rgb, _MidColor.rgb, smoothstep(_BandLow - _BandSoftness, _BandLow + _BandSoftness, facing));
				color = lerp(color, _BrightColor.rgb, smoothstep(_BandHigh - _BandSoftness, _BandHigh + _BandSoftness, facing));
				float rim = pow(1.0 - saturate(dot(normal, view)), _RimPower);
				color = lerp(color, _RimColor.rgb, rim * _RimColor.a);
				// 대각선 띠가 아래에서 위로 훑음
				float sweep = frac(_Time.y / _GlintPeriod) * 3.0 - 1.5;
				float along = input.positionOS.y * 1.6 + input.positionOS.x * 0.8;
				float glint = 1.0 - smoothstep(0.0, _GlintWidth, abs(along - sweep));
				color += _GlintColor.rgb * glint * _GlintColor.a;
				return float4(color * _Glow * (1.0 + _Pulse), 1.0);
			}
			ENDHLSL
		}
	}
}
