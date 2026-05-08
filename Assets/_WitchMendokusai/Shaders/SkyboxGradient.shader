// Skybox: zenith ↔ horizon gradient + sun disc + cloud (C5).
// 모동숲 같은 수채화 그라데이션 톤. SkyDirector 가 매 tick property mutate.
// (TASK-WM-054-C C1+C5; star 는 C4 후속)
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

		[Header(Cloud (C5))]
		_CloudColor ("Cloud Color", Color) = (1, 1, 1, 1)
		_CloudCoverage ("Cloud Coverage", Range(0, 1)) = 0.4
		_CloudSoftness ("Cloud Softness", Range(0.001, 0.5)) = 0.15
		_CloudHeight ("Cloud Height (수평선 기준)", Range(0, 0.8)) = 0.05
		_CloudScale ("Cloud Scale", Range(0.5, 20)) = 4
		_CloudSpeed ("Cloud Speed", Range(0, 0.5)) = 0.05
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
				float4 _CloudColor;
				float _CloudCoverage;
				float _CloudSoftness;
				float _CloudHeight;
				float _CloudScale;
				float _CloudSpeed;
			CBUFFER_END

			float hash21(float2 p)
			{
				p = frac(p * float2(123.34, 456.21));
				p += dot(p, p + 45.32);
				return frac(p.x * p.y);
			}

			float valueNoise(float2 p)
			{
				float2 i = floor(p);
				float2 f = frac(p);
				f = f * f * (3.0 - 2.0 * f);
				float a = hash21(i);
				float b = hash21(i + float2(1, 0));
				float c = hash21(i + float2(0, 1));
				float d = hash21(i + float2(1, 1));
				return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
			}

			float fbm(float2 p)
			{
				float n = valueNoise(p) * 0.5;
				n += valueNoise(p * 2.03) * 0.25;
				n += valueNoise(p * 4.01) * 0.125;
				return n;
			}

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

				// 3. cloud (수평선 위 + dir.y projection plane)
				float upMask = saturate((dir.y - _CloudHeight) / max(0.001, 1.0 - _CloudHeight));
				float2 cloudUV = dir.xz / max(0.05, abs(dir.y)) * _CloudScale;
				cloudUV.x += _Time.y * _CloudSpeed;
				float cloudNoise = fbm(cloudUV);
				float cloudThreshold = 1.0 - _CloudCoverage;
				float cloudAlpha = smoothstep(cloudThreshold, cloudThreshold + _CloudSoftness, cloudNoise) * upMask;
				skyColor = lerp(skyColor, _CloudColor.rgb, cloudAlpha * _CloudColor.a);

				return half4(skyColor, 1.0);
			}
			ENDHLSL
		}
	}
	Fallback Off
}
