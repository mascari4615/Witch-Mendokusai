// CartoonWater — WM-058 P2-D Water 샘플 셰이더팩.
// 모더 SDK Water slot 시연 — flat 카툰 톤 + sin wave 파동 + 가장자리 흰 거품 (foam).
// SkyDirector C6 standard uniform 도 일부 받아 시간대 색감 살짝 반영 (옵션):
//   _WMSkyHorizon (Color) → reflectionTint mix (낮 = 옅은 청록, 황혼 = 분홍빛 surface)
// WaterRenderer 마커 attach 된 MeshRenderer 의 sharedMaterial 교체 — base+overlay 모델.
Shader "WM/Sample/CartoonWater"
{
	Properties
	{
		_DeepColor ("Deep Color", Color) = (0.05, 0.30, 0.45, 1.0)
		_ShallowColor ("Shallow Color", Color) = (0.40, 0.85, 0.95, 1.0)
		_DepthBlend ("Depth Blend (0=flat, 1=full gradient)", Range(0, 1)) = 0.6
		_FoamColor ("Foam Color", Color) = (1.0, 1.0, 1.0, 1.0)
		_FoamIntensity ("Foam Intensity", Range(0, 2)) = 1.0
		_FoamThreshold ("Foam Threshold", Range(0, 1)) = 0.65
		_FoamSoftness ("Foam Softness", Range(0.001, 0.3)) = 0.08
		_WaveAmount ("Wave Amount", Range(0, 0.3)) = 0.06
		_WaveSpeed ("Wave Speed", Range(0, 5)) = 1.2
		_WaveFrequency ("Wave Frequency", Range(1, 30)) = 12.0
		_SkyTintAmount ("Sky Tint Amount (0=무시, 1=황혼 분홍 강함)", Range(0, 1)) = 0.25
	}

	SubShader
	{
		Tags
		{
			"Queue" = "Geometry"
			"RenderType" = "Opaque"
			"RenderPipeline" = "UniversalPipeline"
		}

		Pass
		{
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

			struct Attributes
			{
				float4 positionOS : POSITION;
				float3 normalOS : NORMAL;
				float2 uv : TEXCOORD0;
			};

			struct Varyings
			{
				float4 positionHCS : SV_POSITION;
				float3 worldNormal : TEXCOORD0;
				float2 uv : TEXCOORD1;
			};

			// SkyDirector C6 standard uniforms (옵션 사용).
			float4 _WMSkyHorizon;

			CBUFFER_START(UnityPerMaterial)
				float4 _DeepColor;
				float4 _ShallowColor;
				float _DepthBlend;
				float4 _FoamColor;
				float _FoamIntensity;
				float _FoamThreshold;
				float _FoamSoftness;
				float _WaveAmount;
				float _WaveSpeed;
				float _WaveFrequency;
				float _SkyTintAmount;
			CBUFFER_END

			Varyings vert(Attributes IN)
			{
				Varyings OUT;
				OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
				OUT.worldNormal = TransformObjectToWorldNormal(IN.normalOS);
				OUT.uv = IN.uv;
				return OUT;
			}

			half4 frag(Varyings IN) : SV_Target
			{
				// 1. base — depth gradient (얕은색 ↔ 깊은색).
				float depthFactor = saturate(IN.uv.y * _DepthBlend);
				float3 baseColor = lerp(_ShallowColor.rgb, _DeepColor.rgb, depthFactor);

				// 2. cross sin wave — 두 축 sin 합성으로 자연 물결 패턴.
				float waveX = sin(IN.uv.x * _WaveFrequency + _Time.y * _WaveSpeed) * _WaveAmount;
				float waveY = sin(IN.uv.y * _WaveFrequency * 0.7 + _Time.y * _WaveSpeed * 1.3) * _WaveAmount;
				float waveCombined = waveX + waveY;

				// 3. foam — wave 값이 threshold 넘으면 흰 거품. soft edge.
				float foamMask = smoothstep(_FoamThreshold, _FoamThreshold + _FoamSoftness, waveCombined + 0.5);
				float3 surface = lerp(baseColor, _FoamColor.rgb, foamMask * _FoamIntensity * _FoamColor.a);

				// 4. SkyDirector horizon 색 살짝 mix — 시간대 자동 반영 (옵션).
				surface = lerp(surface, _WMSkyHorizon.rgb, _SkyTintAmount * 0.3);

				return half4(surface, 1.0);
			}
			ENDHLSL
		}
	}
	Fallback Off
}
