// 변신 연출의 빛 메시 (깃, 막대 둘레, 휘감는 띠, 바늘, 고리). 캐릭터 그림과 같은 밀도의 텍셀로 끊어 그린 네 단 색 (그림 기준 memo wm/design/art/style-canon.md: 매끈한 풀 3D 금지, 저폴리에 텍스처)
// - uv.x 는 뿌리 0 에서 끝 1, uv.y 는 가로 0..1 (가운데 0.5). _Texels 는 그 길이와 폭의 텍셀 수 (0 이면 안 끊음)
// - 색은 어둠, 몸, 밝음, 심 네 단. 단 사이는 낮은 잡음으로만 흔듦 (색이 크게 다른 점이 흩어지지 않게)
// - _Reveal 로 뿌리부터 드러나고 _Dissolve 로 끝부터 텍셀 덩어리째 부서짐. 흐름은 초당 _Steps 번만 바뀜
Shader "WM/Fx/FormEnergy"
{
	Properties
	{
		[HDR] _CoreColor ("Core color", Color) = (2.0, 1.75, 1.15, 1)
		[HDR] _BodyColor ("Body color", Color) = (1.35, 0.78, 0.16, 1)
		[HDR] _TipColor ("Dark color", Color) = (0.75, 0.3, 0.06, 1)
		[Enum(Strip, 0, Feather, 1, Glow, 2)] _Shape ("Shape", Float) = 0
		_Texels ("Texels (along, across)", Vector) = (48, 8, 0, 0)
		_Alpha ("Alpha", Range(0, 1)) = 1
		_RootFade ("Root fade length", Range(0, 1)) = 0
		_Reveal ("Reveal", Range(0, 1)) = 1
		_Dissolve ("Dissolve", Range(0, 1)) = 0
		_DissolveFromTip ("Dissolve from tip", Float) = 1
		_NoiseScale ("Noise scale (along, across)", Vector) = (9, 3, 0, 0)
		_NoiseAmount ("Noise amount", Range(0, 1)) = 0.3
		_RimAmount ("Feather outline", Range(0, 1)) = 1
		_Shade ("Shade offset", Range(-1, 1)) = 0
		_Flow ("Flow speed", Float) = 1.5
		_Steps ("Steps per second", Float) = 12
		_Seed ("Seed", Float) = 0
		_Intensity ("Intensity", Range(0, 6)) = 1
		_Flash ("Birth flash", Range(0, 1)) = 0
		_Cover ("Cover (0 adds, 1 covers)", Range(0, 1)) = 1
	}
	SubShader
	{
		Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
		Pass
		{
			Tags { "LightMode"="SRPDefaultUnlit" }
			Blend One OneMinusSrcAlpha
			ZWrite Off
			Cull Off
			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment Frag
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			CBUFFER_START(UnityPerMaterial)
				float4 _CoreColor;
				float4 _BodyColor;
				float4 _TipColor;
				float _Shape;
				float4 _Texels;
				float _Alpha;
				float _RootFade;
				float _Reveal;
				float _Dissolve;
				float _DissolveFromTip;
				float4 _NoiseScale;
				float _NoiseAmount;
				float _RimAmount;
				float _Shade;
				float _Flow;
				float _Steps;
				float _Seed;
				float _Intensity;
				float _Flash;
				float _Cover;
			CBUFFER_END
			struct Attributes { float4 positionOS : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
			struct Varyings { float4 positionCS : SV_POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
			float Hash2(float2 cell)
			{
				return frac(sin(dot(cell, float2(127.1, 311.7))) * 43758.5453);
			}
			float Noise2(float2 position)
			{
				float2 cell = floor(position);
				float2 fraction = frac(position);
				fraction = fraction * fraction * (3.0 - 2.0 * fraction);
				float low = lerp(Hash2(cell), Hash2(cell + float2(1, 0)), fraction.x);
				float high = lerp(Hash2(cell + float2(0, 1)), Hash2(cell + float2(1, 1)), fraction.x);
				return lerp(low, high, fraction.y);
			}
			Varyings Vert(Attributes input)
			{
				Varyings output;
				output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
				output.color = input.color;
				output.uv = input.uv;
				return output;
			}
			float4 Frag(Varyings input) : SV_Target
			{
				float2 uv = input.uv;
				if (_Texels.x > 0.5)
					uv = (floor(uv * _Texels.xy) + 0.5) / _Texels.xy;
				float along = uv.x;
				float side = abs(uv.y * 2.0 - 1.0);
				float below = uv.y * 2.0 - 1.0;

				// 깃 윤곽은 텍셀 단위로 자름 (계단 윤곽)
				float mask = 1.0;
				float shade;
				float clock = floor(_Time.y * _Steps) / max(_Steps, 0.001);
				float2 noiseUv = float2(along * _NoiseScale.x, uv.y * _NoiseScale.y) + _Seed * 7.31;
				float noise = Noise2(noiseUv);
				float flowNoise = Noise2(noiseUv * float2(1.4, 1.0) - float2(clock * _Flow, 0.0));
				if (_Shape > 0.5 && _Shape < 1.5)
				{
					float width = pow(saturate(sin(3.14159 * pow(saturate(along), 0.72))), 0.75);
					// 아랫깃은 넓고 깃가지가 갈라짐
					float notch = step(0.74, frac(along * 7.0 + _Seed)) * step(0.0, -below) * 0.22;
					mask = step(side, max(width - notch, 0.0));
					float inner = side / max(width, 0.001);
					// 크게 나눈 색면: 뿌리 밝음, 끝으로 갈수록 짙음. 위쪽 절반이 한 단 밝음
					shade = 0.82 - along * 0.62 + step(0.0, below) * 0.14 + (noise - 0.5) * _NoiseAmount * 0.5;
					// 깃대 한 줄
					shade += step(side * _Texels.y, 1.1) * 0.3;
					// 깃끼리 갈리는 짙은 테두리 한 텍셀
					float rim = step((width - notch) * _Texels.y - 2.0, side * _Texels.y) + step(_Texels.x - 2.0, along * _Texels.x);
					shade = rim > 0.5 ? lerp(shade - 0.26, 0.05, _RimAmount) : shade;
				}
				else
				{
					shade = 1.0 - side * 0.85 + (flowNoise - 0.5) * _NoiseAmount;
				}

				float reveal = _Reveal >= 1.0 ? 1.0 : step(along, _Reveal);
				// 부서짐은 텍셀 덩어리 (2x2) 째
				float2 chunk = floor(uv * _Texels.xy / 3.0);
				float order = _DissolveFromTip > 0.5 ? 1.0 - along : along;
				float value = order * 0.62 + (noise * 0.75 + Hash2(chunk + _Seed) * 0.25) * 0.38 - _Dissolve * 1.02;
				float remain = _Dissolve <= 0.0 ? 1.0 : step(0.0, value);
				float burn = step(value, 0.07) * step(0.0001, _Dissolve);

				shade = saturate(shade + _Shade + burn * 0.35 + _Flash);
				float band = min(floor(shade * 4.0), 3.0);
				float3 color = band < 0.5 ? _TipColor.rgb : (band < 1.5 ? _BodyColor.rgb : (band < 2.5 ? lerp(_BodyColor.rgb, _CoreColor.rgb, 0.5) : _CoreColor.rgb));
				color = lerp(color, _CoreColor.rgb, _Flash);
				// 빛 둘레는 바깥 단일수록 옅음
				float bandAlpha = _Shape > 1.5 ? (band < 0.5 ? 0.0 : (band < 1.5 ? 0.35 : (band < 2.5 ? 0.7 : 1.0))) : 1.0;
				float root = _RootFade <= 0.0 ? 1.0 : step(_RootFade * (0.3 + noise * 0.7), along);
				float alpha = mask * bandAlpha * reveal * remain * root * _Alpha * input.color.a;
				return float4(min(color * input.color.rgb * alpha * _Intensity, 30.0), alpha * _Cover);
			}
			ENDHLSL
		}
	}
}
