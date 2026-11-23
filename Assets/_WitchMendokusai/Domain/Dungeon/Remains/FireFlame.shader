// 바닥 액체 불의 불길과 연기. 세운 판 (파티클) 하나에 그리는 절차적 불
// - 잡음 두 겹을 위로 흘리고 판 가로를 비틀어 일렁임. 아래쪽 둥근 덩어리에서 잡음만큼 위로 불길이 갈라져 솟음
// - 열 값 하나로 색을 고름: 짙은 빨강, 주황, 노랑, 흰 노랑 (온도 색). 가장자리는 어둡고 반투명
// - 미리 곱한 알파 (One, OneMinusSrcAlpha): 겹쳐도 가산처럼 하얗게 타지 않고, 연기는 바탕을 어둡게 덮음
// - _Smoke 1 이면 같은 잡음으로 회색 연기 덩어리
// 파티클 꼭짓점 흐름: UV (xy), StableRandom.x (z), AgePercent (w) 가 TEXCOORD0 하나에 붙음 (GoopField.Particles)
Shader "WM/Remains/FireFlame"
{
	Properties
	{
		[HDR] _ColDark ("Dark (edge)", Color) = (0.3, 0.03, 0.0, 1)
		[HDR] _ColMid ("Mid", Color) = (1.1, 0.26, 0.03, 1)
		[HDR] _ColHot ("Hot", Color) = (1.7, 0.7, 0.12, 1)
		[HDR] _ColCore ("Core", Color) = (2.0, 1.4, 0.55, 1)
		_SmokeColor ("Smoke", Color) = (0.08, 0.07, 0.065, 1)
		_Smoke ("Smoke mode", Float) = 0
		_NoiseScale ("Noise scale (x, y)", Vector) = (3.6, 2.8, 0, 0)
		_Scroll ("Scroll speed", Float) = 2.2
		_Distort ("Sideways distortion", Float) = 0.55
		_Erode ("Noise erosion", Float) = 2.3
		_HeatGain ("Heat gain", Float) = 0.6
		_AlphaGain ("Alpha gain", Float) = 2.6
		_Center ("Body center height", Range(0, 1)) = 0.22
		_Up ("Body stretch above center", Float) = 1.25
		_Down ("Body stretch below center", Float) = 2.6
		_WidthX ("Body narrowness", Float) = 2.6
		_Falloff ("Body falloff", Float) = 1.6
		_Tip ("Noise growth toward top", Float) = 1.8
		_Opacity ("Opacity (coverage share)", Range(0, 1)) = 0.45
	}
	SubShader
	{
		Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" }
		Pass
		{
			Name "FireFlame"
			Tags { "LightMode" = "UniversalForward" }
			Blend One OneMinusSrcAlpha
			ZWrite Off
			Cull Off

			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment Frag
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

			CBUFFER_START(UnityPerMaterial)
				float4 _ColDark;
				float4 _ColMid;
				float4 _ColHot;
				float4 _ColCore;
				float4 _SmokeColor;
				float _Smoke;
				float4 _NoiseScale;
				float _Scroll;
				float _Distort;
				float _Erode;
				float _HeatGain;
				float _AlphaGain;
				float _Center;
				float _Up;
				float _Down;
				float _WidthX;
				float _Falloff;
				float _Tip;
				float _Opacity;
			CBUFFER_END

			struct Attributes
			{
				float4 positionOS : POSITION;
				float4 color : COLOR;
				float4 particle : TEXCOORD0;
			};

			struct Varyings
			{
				float4 positionCS : SV_POSITION;
				float4 color : COLOR;
				float4 particle : TEXCOORD0;
			};

			float Hash(float2 p)
			{
				return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
			}

			float ValueNoise(float2 p)
			{
				float2 cell = floor(p);
				float2 f = frac(p);
				float2 u = f * f * (3.0 - 2.0 * f);
				float a = Hash(cell);
				float b = Hash(cell + float2(1, 0));
				float c = Hash(cell + float2(0, 1));
				float d = Hash(cell + float2(1, 1));
				return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
			}

			float Fbm(float2 p)
			{
				float sum = 0.0;
				float amplitude = 0.5;
				for (int i = 0; i < 4; i++)
				{
					sum += ValueNoise(p) * amplitude;
					p = p * 2.03 + float2(17.1, 3.7);
					amplitude *= 0.5;
				}
				return sum / 0.9375;
			}

			Varyings Vert(Attributes input)
			{
				Varyings output;
				output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
				output.color = input.color;
				output.particle = input.particle;
				return output;
			}

			half4 Frag(Varyings input) : SV_Target
			{
				float2 uv = input.particle.xy;
				float seed = input.particle.z * 61.0;
				float age = input.particle.w;
				float time = _Time.y;

				// 위로 흐르는 잡음 두 겹. 판마다 다른 자리 (seed)
				float2 p = float2(uv.x * _NoiseScale.x, uv.y * _NoiseScale.y - time * _Scroll) + seed;
				float noise = Fbm(p) * 0.65 + Fbm(p * 2.1 + float2(5.3, -time * _Scroll * 0.7)) * 0.35;

				// 위로 갈수록 크게 옆으로 비틂
				float x = uv.x - 0.5 + (noise - 0.5) * _Distort * uv.y;
				float y = uv.y;
				float fade = smoothstep(0.0, 0.15, age) * (1.0 - smoothstep(0.65, 1.0, age)) * input.color.a;

				if (_Smoke > 0.5)
				{
					// 둥근 덩어리를 잡음으로 깎은 연기. 위로 갈수록 옅음
					float radius = length(float2(x * 1.6, (y - 0.5) * 1.3));
					float puff = saturate((0.55 - radius) * 3.0 - (1.0 - noise) * 0.9);
					float smokeAlpha = puff * fade * _SmokeColor.a;
					return half4(_SmokeColor.rgb * smokeAlpha, smokeAlpha);
				}

				// 아래쪽 둥근 덩어리 (가운데보다 위는 길게, 아래는 짧게). 잡음이 위로 갈수록 크게 더해져 불길이 갈라져 솟음
				float dy = y > _Center ? (y - _Center) * _Up : (y - _Center) * _Down;
				float body = 1.0 - length(float2(x * _WidthX, dy)) * _Falloff;
				float heat = body + (noise - 0.5) * _Erode * (0.3 + y * _Tip);
				heat = saturate(heat * _HeatGain) * fade;

				float3 color = lerp(_ColDark.rgb, _ColMid.rgb, saturate(heat * 3.0));
				color = lerp(color, _ColHot.rgb, saturate(heat * 3.0 - 1.0));
				color = lerp(color, _ColCore.rgb, saturate(heat * 3.0 - 2.0));
				color *= input.color.rgb;

				float alpha = saturate(heat * _AlphaGain);
				// 색은 알파만큼 더하고, 덮는 몫은 _Opacity 만큼만 (빛은 바탕 위에 더해지는 쪽이 자연스러움)
				return half4(color * alpha, alpha * _Opacity);
			}
			ENDHLSL
		}
	}
}
