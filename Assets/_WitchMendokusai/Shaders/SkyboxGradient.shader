// Skybox: zenith ↔ horizon gradient + sun disc.
// 모동숲 같은 수채화 그라데이션 톤. SkyDirector 가 매 tick property mutate.
// (TASK-WM-054-C C1; star/cloud 는 C4/C5 후속)
Shader "WM/SkyboxGradient"
{
	Properties
	{
		_ZenithColor ("Zenith", Color) = (0.10, 0.10, 0.30, 1)
		_HorizonColor ("Horizon", Color) = (1.00, 0.70, 0.50, 1)
		_SunDiscColor ("Sun Disc", Color) = (1.00, 0.95, 0.85, 1)
		_SunDirection ("Sun Direction (xyz, normalized)", Vector) = (0, 1, 0, 0)
		_SunSize ("Sun Size", Range(0, 0.5)) = 0.04
		_SunSoftness ("Sun Softness", Range(0, 0.5)) = 0.05
		_HorizonExponent ("Horizon Exponent", Range(0.5, 8)) = 2.5
		_StarAlpha ("Star Alpha (C4)", Range(0, 1)) = 0
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

			CBUFFER_START(UnityPerMaterial)
				float4 _ZenithColor;
				float4 _HorizonColor;
				float4 _SunDiscColor;
				float4 _SunDirection;
				float _SunSize;
				float _SunSoftness;
				float _HorizonExponent;
				float _StarAlpha;
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

				// 1. zenith ↔ horizon gradient
				float horizonFactor = pow(saturate(1.0 - max(dir.y, 0.0)), _HorizonExponent);
				float3 skyColor = lerp(_ZenithColor.rgb, _HorizonColor.rgb, horizonFactor);

				// 2. sun disc
				float3 sunDir = normalize(_SunDirection.xyz);
				float sunDot = dot(dir, sunDir);
				float innerEdge = 1.0 - _SunSize;
				float outerEdge = innerEdge - _SunSoftness;
				float sunMask = smoothstep(outerEdge, innerEdge, sunDot);
				skyColor = lerp(skyColor, _SunDiscColor.rgb, sunMask * _SunDiscColor.a);

				return half4(skyColor, 1.0);
			}
			ENDHLSL
		}
	}
	Fallback Off
}
