// 궁극기 배경 장악. 카메라 앞을 덮는 판 하나가 카메라 Opaque Texture (불투명 배경만 담김) 를 읽어 다시 그림
// - 그리는 순서가 반투명 스프라이트 (인물, 몬스터, 큐 3000) 와 바닥 효과 판 (큐 2930~2960) 보다 먼저라, 바뀌는 것은 배경뿐이고 인물과 효과 색은 그대로 남음
// - 탈색, 보색 물들임, 어둡게, 선화 (윤곽선만 남긴 흰 반전), 두 색 (흑백 임팩트 프레임), 디더 (점 무늬 두 색), 우주 교체 (별, 은하수 띠), 사선 와이프, 섬광
// - 와이프가 켜지면 (_WipeOn 1) 우주 교체가 화면 가운데를 지나는 사선 띠 안에만 걸림. 띠 반폭 _WipePosition 이 커지면 사선이 열리듯 퍼짐. 탈색과 물들임은 화면 전체
// - 디더는 우주 교체 뒤에 걸려 별 하늘까지 점 무늬 두 색이 됨
Shader "WM/Fx/UltimateGrade"
{
	Properties
	{
		_Desaturate ("Desaturate", Range(0, 1)) = 0
		_TintColor ("Tint Color", Color) = (0.4, 0.4, 1, 1)
		_TintAmount ("Tint Amount", Range(0, 1)) = 0
		_Darken ("Darken", Range(0, 1)) = 0
		_LineArt ("Line Art", Range(0, 1)) = 0
		_LineInk ("Line Ink", Color) = (0.25, 0.2, 0.15, 1)
		_LinePaper ("Line Paper", Color) = (1, 1, 1, 1)
		_LineGain ("Line Gain", Float) = 6
		_TwoTone ("Two Tone", Range(0, 1)) = 0
		_ToneDark ("Tone Dark", Color) = (0, 0, 0, 1)
		_ToneLight ("Tone Light", Color) = (1, 1, 1, 1)
		_ToneThreshold ("Tone Threshold", Range(0, 1)) = 0.35
		_Dither ("Dither", Range(0, 1)) = 0
		_DitherDark ("Dither Dark", Color) = (0.05, 0.06, 0.16, 1)
		_DitherLight ("Dither Light", Color) = (0.75, 0.8, 1, 1)
		_DitherScale ("Dither Scale", Float) = 3
		_Space ("Space", Range(0, 1)) = 0
		_SpaceTop ("Space Top", Color) = (0.02, 0.02, 0.08, 1)
		_SpaceBottom ("Space Bottom", Color) = (0.06, 0.05, 0.18, 1)
		_StarDensity ("Star Density", Float) = 60
		_StarBrightness ("Star Brightness", Float) = 1.2
		_BandColor ("Band Color", Color) = (0.5, 0.45, 0.8, 0)
		_BandAngle ("Band Angle", Float) = 0.6
		_WipeOn ("Wipe On", Float) = 0
		_WipeAngle ("Wipe Angle", Float) = 1.0
		_WipePosition ("Wipe Position", Float) = -2
		_WipeSoft ("Wipe Soft", Float) = 0.004
		_WipeEdgeColor ("Wipe Edge Color", Color) = (1, 0.6, 0.8, 0)
		_Flash ("Flash", Range(0, 1)) = 0
		_FlashColor ("Flash Color", Color) = (1, 1, 1, 1)
	}

	SubShader
	{
		Tags { "Queue" = "Transparent-75" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

		Pass
		{
			Name "UltimateGrade"
			Tags { "LightMode" = "SRPDefaultUnlit" }
			Blend Off
			ZWrite Off
			ZTest Always
			Cull Off

			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment Frag
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

			CBUFFER_START(UnityPerMaterial)
				float _Desaturate;
				float4 _TintColor;
				float _TintAmount;
				float _Darken;
				float _LineArt;
				float4 _LineInk;
				float4 _LinePaper;
				float _LineGain;
				float _TwoTone;
				float4 _ToneDark;
				float4 _ToneLight;
				float _ToneThreshold;
				float _Dither;
				float4 _DitherDark;
				float4 _DitherLight;
				float _DitherScale;
				float _Space;
				float4 _SpaceTop;
				float4 _SpaceBottom;
				float _StarDensity;
				float _StarBrightness;
				float4 _BandColor;
				float _BandAngle;
				float _WipeOn;
				float _WipeAngle;
				float _WipePosition;
				float _WipeSoft;
				float4 _WipeEdgeColor;
				float _Flash;
				float4 _FlashColor;
			CBUFFER_END

			struct Attributes
			{
				float4 positionOS : POSITION;
			};

			struct Varyings
			{
				float4 positionCS : SV_POSITION;
				float4 screenPos : TEXCOORD0;
			};

			Varyings Vert(Attributes input)
			{
				Varyings output;
				output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
				output.screenPos = ComputeScreenPos(output.positionCS);
				return output;
			}

			float Luma(float3 color)
			{
				return dot(color, float3(0.299, 0.587, 0.114));
			}

			float HashCell(float2 cell)
			{
				return frac(sin(dot(cell, float2(127.1, 311.7))) * 43758.5453);
			}

			float Noise(float2 position)
			{
				float2 cell = floor(position);
				float2 blend = frac(position);
				blend = blend * blend * (3.0 - 2.0 * blend);
				float bottom = lerp(HashCell(cell), HashCell(cell + float2(1, 0)), blend.x);
				float top = lerp(HashCell(cell + float2(0, 1)), HashCell(cell + float2(1, 1)), blend.x);
				return lerp(bottom, top, blend.y);
			}

			float Fbm(float2 position)
			{
				float sum = 0;
				float amplitude = 0.5;
				for (int octave = 0; octave < 4; octave++)
				{
					sum += Noise(position) * amplitude;
					position *= 2.0;
					amplitude *= 0.5;
				}
				return sum;
			}

			float Stars(float2 position, float density, float seed)
			{
				float2 grid = position * density;
				float2 cell = floor(grid);
				float chance = HashCell(cell + seed);
				float2 spot = float2(HashCell(cell + seed + 1.3), HashCell(cell + seed + 2.7));
				float distance = length(frac(grid) - spot);
				float size = lerp(0.04, 0.12, HashCell(cell + seed + 4.1));
				float twinkle = 0.7 + 0.3 * sin(_Time.y * (2.0 + chance * 5.0) + chance * 40.0);
				return step(0.86, chance) * saturate(1.0 - distance / size) * twinkle;
			}

			float BayerThreshold(float2 pixel)
			{
				static const float matrix4[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
				int2 cell = int2(fmod(pixel, 4.0));
				return (matrix4[cell.y * 4 + cell.x] + 0.5) / 16.0;
			}

			half4 Frag(Varyings input) : SV_Target
			{
				float2 uv = input.screenPos.xy / input.screenPos.w;
				float aspect = _ScreenParams.x / _ScreenParams.y;
				float2 aspectUv = float2(uv.x * aspect, uv.y);
				float3 scene = SampleSceneColor(uv);
				float3 color = scene;
				float luma = Luma(scene);

				color = lerp(color, luma.xxx, _Desaturate);
				color = lerp(color, luma * _TintColor.rgb * 2.0, _TintAmount);
				color *= 1.0 - _Darken;

				if (_LineArt > 0.001)
				{
					float2 texel = 1.0 / _ScreenParams.xy * 1.5;
					float left = Luma(SampleSceneColor(uv - float2(texel.x, 0)));
					float right = Luma(SampleSceneColor(uv + float2(texel.x, 0)));
					float down = Luma(SampleSceneColor(uv - float2(0, texel.y)));
					float up = Luma(SampleSceneColor(uv + float2(0, texel.y)));
					float edge = saturate((abs(right - left) + abs(up - down)) * _LineGain);
					float3 art = lerp(_LinePaper.rgb, _LineInk.rgb, edge);
					color = lerp(color, art, _LineArt);
				}

				float tone = smoothstep(_ToneThreshold - 0.03, _ToneThreshold + 0.03, luma);
				color = lerp(color, lerp(_ToneDark.rgb, _ToneLight.rgb, tone), _TwoTone);

				float mask = 1.0;
				float3 edgeLight = 0;
				if (_WipeOn > 0.5)
				{
					float2 normal = float2(cos(_WipeAngle), sin(_WipeAngle));
					float signedDistance = abs(dot(aspectUv - float2(aspect * 0.5, 0.5), normal)) - _WipePosition;
					mask = 1.0 - smoothstep(-_WipeSoft, _WipeSoft, signedDistance);
					edgeLight = _WipeEdgeColor.rgb * _WipeEdgeColor.a * exp(-pow(signedDistance / 0.012, 2.0));
				}

				if (_Space > 0.001)
				{
					float3 sky = lerp(_SpaceBottom.rgb, _SpaceTop.rgb, uv.y);
					float2 bandAxis = float2(cos(_BandAngle), sin(_BandAngle));
					float across = dot(aspectUv - float2(aspect * 0.5, 0.5), float2(-bandAxis.y, bandAxis.x));
					float cloud = Fbm(aspectUv * 4.0 + 3.0);
					float band = exp(-pow(across / (0.16 + cloud * 0.12), 2.0)) * smoothstep(0.25, 0.75, cloud);
					sky += _BandColor.rgb * band * _BandColor.a;
					float starLight = Stars(aspectUv, _StarDensity, 0.0) + Stars(aspectUv, _StarDensity * 2.3, 17.0) * 0.6 + band * Stars(aspectUv, _StarDensity * 5.0, 31.0) * _BandColor.a;
					sky += starLight * _StarBrightness;
					color = lerp(color, sky, _Space * mask);
				}

				if (_Dither > 0.001)
				{
					float2 pixel = floor(uv * _ScreenParams.xy / max(_DitherScale, 1.0));
					float lit = step(BayerThreshold(pixel), saturate(Luma(color) * 1.6));
					color = lerp(color, lerp(_DitherDark.rgb, _DitherLight.rgb, lit), _Dither);
				}

				float3 result = color + edgeLight;
				result = lerp(result, _FlashColor.rgb, _Flash);
				return half4(result, 1);
			}
			ENDHLSL
		}
	}
}
