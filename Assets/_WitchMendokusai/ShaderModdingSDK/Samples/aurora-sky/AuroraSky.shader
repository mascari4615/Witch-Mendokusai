// AuroraSky — WM-058 P2-D 샘플 셰이더팩.
// 모더 SDK 의 standard uniform contract 시연:
//   _WMSkyZenith / _WMSkyHorizon (Color) → base sky 그라데이션 mix
//   _WMSkyStarAlpha (float) → 밤 강도 (낮 = 0, 밤 = 1) 로 오로라 alpha modulate
// SkyDirector (TASK-WM-054-C C6) 가 매 프레임 Shader.SetGlobal* 로 노출.
// 모더는 자기 톤·강도·움직임 자유 — Properties 인스펙터에서 tweak.
Shader "WM/Sample/AuroraSky"
{
	Properties
	{
		_AuroraColor ("Aurora Color", Color) = (0.3, 1.0, 0.6, 1.0)
		_AuroraIntensity ("Aurora Intensity", Range(0, 5)) = 2.0
		_AuroraHeight ("Aurora Height (0=horizon, 1=zenith)", Range(0, 1)) = 0.55
		_AuroraThickness ("Aurora Thickness", Range(0.01, 0.5)) = 0.18
		_AuroraWaveAmount ("Aurora Wave Amount", Range(0, 0.3)) = 0.08
		_AuroraWaveSpeed ("Aurora Wave Speed", Range(0, 5)) = 1.0
		_AuroraWaveFrequency ("Aurora Wave Frequency", Range(1, 20)) = 8.0
	}

	SubShader
	{
		Tags
		{
			"Queue" = "Background"
			"RenderType" = "Background"
			"PreviewType" = "Skybox"
			"RenderPipeline" = "UniversalPipeline"
		}
		Cull Off
		ZWrite Off

		Pass
		{
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

			struct Attributes
			{
				float4 positionOS : POSITION;
			};

			struct Varyings
			{
				float4 positionHCS : SV_POSITION;
				float3 viewDir : TEXCOORD0;
			};

			// SkyDirector C6 standard uniforms (Shader.SetGlobal* 노출).
			// CBUFFER 밖 — global uniform 은 UnityPerMaterial 안에 박지 않는다.
			float4 _WMSkyZenith;
			float4 _WMSkyHorizon;
			float _WMSkyStarAlpha;
			float _WMNormalizedTime;

			CBUFFER_START(UnityPerMaterial)
				float4 _AuroraColor;
				float _AuroraIntensity;
				float _AuroraHeight;
				float _AuroraThickness;
				float _AuroraWaveAmount;
				float _AuroraWaveSpeed;
				float _AuroraWaveFrequency;
			CBUFFER_END

			Varyings vert(Attributes IN)
			{
				Varyings OUT;
				OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
				OUT.viewDir = normalize(IN.positionOS.xyz);
				return OUT;
			}

			half4 frag(Varyings IN) : SV_Target
			{
				float3 dir = normalize(IN.viewDir);

				// 1. base sky — SkyDirector zenith ↔ horizon (base+overlay 합성의 base).
				float verticalFactor = saturate(dir.y);
				float3 baseSky = lerp(_WMSkyHorizon.rgb, _WMSkyZenith.rgb, verticalFactor);

				// 2. aurora ribbon — 수직 _AuroraHeight 근처 띠 + sin wave 좌우 흐름 + Gaussian falloff.
				float wave = sin(dir.x * _AuroraWaveFrequency + _Time.y * _AuroraWaveSpeed) * _AuroraWaveAmount;
				float ribbonY = _AuroraHeight + wave;
				float ribbonDist = abs(verticalFactor - ribbonY);
				float ribbonShape = exp(-pow(ribbonDist / max(0.001, _AuroraThickness), 2.0));

				// 3. _WMSkyStarAlpha 받아 밤일 때만 진해짐 — 모더 효과가 *시간대 자동 반응* 시연.
				float intensity = _AuroraIntensity * _WMSkyStarAlpha;

				float3 aurora = _AuroraColor.rgb * ribbonShape * intensity;

				return half4(baseSky + aurora, 1.0);
			}
			ENDHLSL
		}
	}
	Fallback Off
}
