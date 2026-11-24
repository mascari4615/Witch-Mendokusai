// 변신 연출의 빛 메시 (깃, 빛 막대 둘레, 휘감는 띠, 발밑 불꽃, 머리 위 고리). 덮어 칠하는 합성이라 밝은 풀밭에서도 보임 (WM/ArcSlash 와 같은 결)
// - uv.x 는 뿌리 0 에서 끝 1, uv.y 는 가로 0..1 (가운데 0.5)
// - _Reveal 로 뿌리부터 드러나고 _Dissolve 로 끝부터 (_DissolveFromTip 1) 부서짐. 경계에 밝은 띠
// - _Dash 가 있으면 길이 방향으로 점선이 되며 벌어짐 (고리 부서짐)
Shader "WM/Fx/FormEnergy"
{
	Properties
	{
		[HDR] _CoreColor ("Core color", Color) = (2.6, 2.3, 1.6, 1)
		[HDR] _BodyColor ("Body color", Color) = (1.7, 1.05, 0.25, 1)
		[HDR] _TipColor ("Tip color", Color) = (1.5, 0.6, 0.1, 1)
		_Alpha ("Alpha", Range(0, 1)) = 1
		_BodyAlpha ("Body alpha", Range(0, 1)) = 0.5
		_CoreWidth ("Core width", Range(0.01, 1)) = 0.22
		_EdgeWidth ("Edge width", Range(0.01, 1)) = 0.2
		_EdgeAmount ("Edge amount", Range(0, 2)) = 0.8
		_Softness ("Outer softness", Range(0, 1)) = 0
		_RootFade ("Root fade length", Range(0, 1)) = 0
		_Reveal ("Reveal", Range(0, 1)) = 1
		_Dissolve ("Dissolve", Range(0, 1)) = 0
		_DissolveFromTip ("Dissolve from tip", Float) = 1
		_Dash ("Dash", Range(0, 1)) = 0
		_DashCount ("Dash count", Float) = 24
		_NoiseScale ("Noise scale (along, across)", Vector) = (7, 2.5, 0, 0)
		_Flow ("Flow speed", Float) = 1.5
		_FlowAmount ("Flow amount", Range(0, 1)) = 0.35
		_Seed ("Seed", Float) = 0
		_Intensity ("Intensity", Range(0, 6)) = 1
		_Flash ("Birth flash", Range(0, 1)) = 0
		_Cover ("Cover (0 adds, 1 covers)", Range(0, 1)) = 0.8
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
				float _Alpha;
				float _BodyAlpha;
				float _CoreWidth;
				float _EdgeWidth;
				float _EdgeAmount;
				float _Softness;
				float _RootFade;
				float _Reveal;
				float _Dissolve;
				float _DissolveFromTip;
				float _Dash;
				float _DashCount;
				float4 _NoiseScale;
				float _Flow;
				float _FlowAmount;
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
				float along = input.uv.x;
				float side = abs(input.uv.y * 2.0 - 1.0);
				float2 noiseUv = float2(along * _NoiseScale.x, input.uv.y * _NoiseScale.y) + _Seed * 7.31;
				float noise = Noise2(noiseUv);

				// 드러남 (뿌리부터)
				float reveal = _Reveal >= 1.0 ? 1.0 : 1.0 - smoothstep(_Reveal - 0.02, _Reveal + 0.001, along);

				// 부서짐. 길이 0.62 + 잡음 0.38
				float order = _DissolveFromTip > 0.5 ? 1.0 - along : along;
				float value = order * 0.62 + noise * 0.38 - _Dissolve * 1.02;
				float remain = _Dissolve <= 0.0 ? 1.0 : smoothstep(0.0, 0.03, value);
				float burn = (1.0 - smoothstep(0.0, 0.1, value)) * step(0.0001, _Dissolve);

				float flow = lerp(1.0, 0.45 + 1.1 * Noise2(noiseUv * float2(1.6, 1.0) - float2(_Time.y * _Flow, 0.0)), _FlowAmount);
				float core = 1.0 - smoothstep(0.0, _CoreWidth, side);
				float edge = smoothstep(1.0 - _EdgeWidth, 1.0, side) * _EdgeAmount;
				float outer = lerp(1.0, 1.0 - smoothstep(0.0, 1.0, side), _Softness);
				float dash = _Dash <= 0.0 ? 1.0 : smoothstep(_Dash - 0.06, _Dash, abs(frac(along * _DashCount) * 2.0 - 1.0));

				float strength = saturate((_BodyAlpha * flow + core + edge) * outer + burn * 0.8 + _Flash);
				float root = _RootFade <= 0.0 ? 1.0 : smoothstep(0.0, _RootFade, along);
				float alpha = strength * root * reveal * remain * dash * _Alpha * input.color.a;
				float3 color = lerp(_BodyColor.rgb, _TipColor.rgb, smoothstep(0.35, 1.0, along));
				color = lerp(color, _CoreColor.rgb, saturate(core + edge * 0.7 + burn + _Flash));
				color *= input.color.rgb;
				return float4(min(color * alpha * _Intensity, 30.0), alpha * _Cover);
			}
			ENDHLSL
		}
	}
}
