// 바닥 예고 판. 바닥에 눕힌 사각판 (XZ, UV 0~1) 에 원 또는 직사각 띠를 거리 함수로 그림
// - 원: 중심에서 바깥으로 차오름. 띠: UV 의 v 가 0 인 쪽 (시작점) 에서 1 쪽으로 차오름
// - 길이는 전부 월드 단위. 판 크기와 모양 반크기는 스크립트가 넘김 (판은 모양보다 바깥 장식만큼 큼)
// - 겹 순서 (아래에서 위): 바탕과 빗금, 차오른 면, 앞선과 그 빛, 이중 가장자리 고리, 바깥 눈금, 바깥 빛, 끝 충격파, 끝 번쩍임
// - 나타날 때는 가장자리가 한 바퀴 그려지며 등장 (_Appear), 끝나면 충격파 고리가 바깥으로 퍼짐 (_Shock)
// - 밝은 부분은 더하기로 얹어 풀밭 위에서도 빛나 보임 (곱한 알파 출력)
// - 값은 GroundTelegraph.cs 가 MaterialPropertyBlock 으로 매 프레임 넣음
Shader "WM/Fx/GroundTelegraph"
{
	Properties
	{
		[Enum(Circle, 0, Line, 1)] _Shape ("Shape", Float) = 0
		_BaseColor ("Base Color", Color) = (0.55, 0.08, 0.1, 0.28)
		_FillColor ("Fill Color", Color) = (0.95, 0.24, 0.2, 0.5)
		_FrontColor ("Fill Front Color", Color) = (1, 0.82, 0.62, 1)
		_EdgeColor ("Edge Color", Color) = (1, 0.36, 0.3, 1)
		_FlashColor ("Flash Color", Color) = (1, 0.96, 0.88, 1)
		_EdgeWidth ("Edge Width (world)", Float) = 0.07
		_InnerRingInset ("Inner Ring Inset (world)", Float) = 0.16
		_InnerRingWidth ("Inner Ring Width (world)", Float) = 0.02
		_FrontWidth ("Fill Front Width (world)", Float) = 0.035
		_FrontGlowWidth ("Fill Front Glow Width (world)", Float) = 0.45
		_GlowWidth ("Outer Glow Width (world)", Float) = 0.35
		_GlowStrength ("Outer Glow Strength", Range(0, 2)) = 0.6
		_CornerRadius ("Line Corner Radius (world)", Float) = 0.1
		_HatchScale ("Hatch Lines Per World Unit", Float) = 2.4
		_HatchStrength ("Hatch Strength", Range(0, 1)) = 0.22
		_HatchSpeed ("Hatch Scroll Speed", Float) = 0.35
		_TickCount ("Outer Tick Count (circle) or Per Unit (line)", Float) = 36
		_TickLength ("Outer Tick Length (world)", Float) = 0.12
		_TickGap ("Outer Tick Gap From Edge (world)", Float) = 0.07
		_TickSpeed ("Outer Tick Rotate Speed", Float) = 0.06
		_Progress ("Progress", Range(0, 1)) = 0.5
		_Appear ("Appear (set by script)", Range(0, 1)) = 1
		_PulseAmount ("Edge Pulse Amount", Range(0, 1)) = 0.25
		_PulseSpeed ("Edge Pulse Speed", Float) = 9
		_Flash ("Flash (set by script)", Range(0, 1)) = 0
		_FlashStrength ("Flash Strength", Range(0, 1)) = 0.85
		_Shock ("Shockwave (set by script)", Range(0, 1)) = 0
		_ShockDistance ("Shockwave Travel (world)", Float) = 0.9
		_ShockWidth ("Shockwave Width (world)", Float) = 0.12
		_AddStrength ("Additive Strength", Range(0, 2)) = 0.7
		_Alpha ("Master Alpha", Range(0, 1)) = 1
		_PlateSize ("Plate Size (world x, z)", Vector) = (2, 2, 0, 0)
		_ShapeHalfSize ("Shape Half Size (world x, z)", Vector) = (0.8, 0.8, 0, 0)
	}

	SubShader
	{
		// 유닛 스프라이트보다 먼저 그려서 유닛이 예고 위에 오게 함
		Tags { "Queue" = "Transparent-50" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

		Pass
		{
			Name "GroundTelegraph"
			Tags { "LightMode" = "SRPDefaultUnlit" }

			// 곱한 알파. rgb 에 더하기 성분을 얹어 밝은 선이 빛나게
			Blend One OneMinusSrcAlpha
			ZWrite Off
			ZTest LEqual
			// 바닥과 같은 높이에서 깜빡이지 않게 깊이를 카메라 쪽으로 당김 (스크립트의 띄움 높이와 같이 씀)
			Offset -1, -1
			Cull Off

			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment Frag

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

			CBUFFER_START(UnityPerMaterial)
				float _Shape;
				half4 _BaseColor;
				half4 _FillColor;
				half4 _FrontColor;
				half4 _EdgeColor;
				half4 _FlashColor;
				float _EdgeWidth;
				float _InnerRingInset;
				float _InnerRingWidth;
				float _FrontWidth;
				float _FrontGlowWidth;
				float _GlowWidth;
				half _GlowStrength;
				float _CornerRadius;
				float _HatchScale;
				half _HatchStrength;
				float _HatchSpeed;
				float _TickCount;
				float _TickLength;
				float _TickGap;
				float _TickSpeed;
				float _Progress;
				half _Appear;
				half _PulseAmount;
				float _PulseSpeed;
				half _Flash;
				half _FlashStrength;
				half _Shock;
				float _ShockDistance;
				float _ShockWidth;
				half _AddStrength;
				half _Alpha;
				float4 _PlateSize;
				float4 _ShapeHalfSize;
			CBUFFER_END

			struct Attributes
			{
				float4 positionOS : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct Varyings
			{
				float4 positionCS : SV_POSITION;
				float2 uv : TEXCOORD0;
			};

			Varyings Vert(Attributes input)
			{
				Varyings output;
				output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
				output.uv = input.uv;
				return output;
			}

			// 곱하지 않은 알파끼리 위에 얹기
			half4 Over(half4 under, half3 overColor, half overAlpha)
			{
				half outAlpha = overAlpha + under.a * (1.0 - overAlpha);
				half3 outColor = (overColor * overAlpha + under.rgb * under.a * (1.0 - overAlpha)) / max(outAlpha, 1e-4);
				return half4(outColor, outAlpha);
			}

			// 거리 d 를 중심으로 한 굵기 width 의 선 (안티에일리어싱 포함)
			half Band(float d, float halfWidth, float aa)
			{
				return 1.0 - smoothstep(halfWidth - aa, halfWidth + aa, abs(d));
			}

			half4 Frag(Varyings input) : SV_Target
			{
				// 판 중심 기준 월드 단위 좌표. y 가 차오르는 축 (띠)
				float2 position = (input.uv - 0.5) * _PlateSize.xy;
				float2 halfSize = _ShapeHalfSize.xy;
				bool isCircle = _Shape < 0.5;

				// shapeDistance: 모양 가장자리까지 (안쪽 음수). fillDistance: 차오름 앞선까지 (차오른 쪽 음수)
				// sweep: 나타날 때 가장자리가 그려지는 순서 0~1. along: 둘레를 따라 잰 길이 (눈금용)
				float shapeDistance;
				float fillDistance;
				float sweep;
				float along;
				if (isCircle)
				{
					float radial = length(position);
					float turn = atan2(position.x, position.y) / TWO_PI + 0.5;
					shapeDistance = radial - halfSize.x;
					fillDistance = radial - _Progress * halfSize.x;
					sweep = turn;
					along = turn * _TickCount + _Time.y * _TickSpeed * _TickCount;
				}
				else
				{
					float corner = min(_CornerRadius, min(halfSize.x, halfSize.y));
					float2 toCorner = abs(position) - (halfSize - corner);
					shapeDistance = length(max(toCorner, 0.0)) + min(max(toCorner.x, toCorner.y), 0.0) - corner;
					fillDistance = (position.y + halfSize.y) - _Progress * halfSize.y * 2.0;
					sweep = saturate((position.y + halfSize.y) / max(halfSize.y * 2.0, 1e-4));
					along = position.y * _TickCount - _Time.y * _TickSpeed * _TickCount * 4.0;
				}

				float aa = max(fwidth(shapeDistance), 1e-5);
				float fillAa = max(fwidth(fillDistance), 1e-5);

				half inside = 1.0 - smoothstep(-aa, aa, shapeDistance);
				half filled = inside * (1.0 - smoothstep(-fillAa, fillAa, fillDistance));
				half shown = step(sweep, _Appear * 1.02);

				// 바탕: 가장자리로 갈수록 진함. 그 위에 흐르는 빗금 (안 찬 곳에서 더 보임)
				float depth = saturate(-shapeDistance / max(min(halfSize.x, halfSize.y), 1e-4));
				half baseAlpha = _BaseColor.a * lerp(1.0, 0.45, depth);
				float hatchCoord = isCircle
					? (position.x + position.y) * _HatchScale + _Time.y * _HatchSpeed
					: (position.y - abs(position.x)) * _HatchScale - _Time.y * _HatchSpeed * 6.0;
				float hatchWave = abs(frac(hatchCoord) - 0.5) * 2.0;
				float hatchAa = max(fwidth(hatchCoord) * 2.0, 1e-4);
				half hatch = smoothstep(0.5 - hatchAa, 0.5 + hatchAa, hatchWave);
				half4 color = half4(_BaseColor.rgb, inside * shown * (baseAlpha + hatch * _HatchStrength * lerp(1.0, 0.4, filled)));

				// 차오른 면: 앞선 쪽이 밝음
				half fillShade = lerp(0.75, 1.0, saturate(1.0 + fillDistance / max(_FrontGlowWidth * 2.0, 1e-4)));
				color = Over(color, _FillColor.rgb * fillShade, _FillColor.a * filled);

				// 앞선과 그 뒤로 번지는 빛 (진행도 0 근처에서는 숨김)
				half frontOn = saturate(_Progress * 20.0) * inside;
				half front = Band(fillDistance, _FrontWidth, fillAa) * frontOn;
				half frontGlow = saturate(1.0 + fillDistance / max(_FrontGlowWidth, 1e-4)) * filled * frontOn;
				frontGlow *= frontGlow;

				// 가장자리 이중 고리와 맥동
				half pulseWave = 0.5 + 0.5 * sin(_Time.y * _PulseSpeed);
				half pulse = 1.0 - _PulseAmount * pulseWave;
				half edge = Band(shapeDistance + _EdgeWidth * 0.5, _EdgeWidth * 0.5, aa) * shown;
				half innerRing = Band(shapeDistance + _InnerRingInset, _InnerRingWidth * 0.5, aa) * shown * 0.7;

				// 바깥 눈금: 가장자리 밖에 짧은 선들이 둘레를 따라 흐름
				float tickCenter = _TickGap + _TickLength * 0.5;
				half tickBand = Band(shapeDistance - tickCenter, _TickLength * 0.5, aa);
				float tickWave = abs(frac(along) - 0.5) * 2.0;
				float tickAa = max(fwidth(along) * 2.0, 1e-4);
				half ticks = tickBand * smoothstep(0.55 - tickAa, 0.55 + tickAa, tickWave) * shown;

				// 바깥 빛
				half glowFalloff = saturate(1.0 - shapeDistance / max(_GlowWidth, 1e-5));
				half glow = (1.0 - inside) * glowFalloff * glowFalloff * _GlowStrength * shown;

				color = Over(color, _FrontColor.rgb, _FrontColor.a * frontGlow * 0.55);
				color = Over(color, _EdgeColor.rgb, _EdgeColor.a * innerRing);
				color = Over(color, _EdgeColor.rgb, _EdgeColor.a * edge * pulse);
				color = Over(color, _EdgeColor.rgb, _EdgeColor.a * ticks * 0.85);
				color = Over(color, _EdgeColor.rgb, _EdgeColor.a * saturate(glow) * pulse);
				color = Over(color, _FrontColor.rgb, _FrontColor.a * front);

				// 끝: 충격파 고리가 가장자리에서 바깥으로 퍼지며 옅어짐
				float shockAt = _Shock * _ShockDistance;
				half shockLife = saturate(_Shock * 30.0) * (1.0 - _Shock);
				half shock = Band(shapeDistance - shockAt, _ShockWidth * (1.0 - _Shock * 0.6), aa) * shockLife;
				color = Over(color, _FlashColor.rgb, shock);

				// 끝 번쩍임: 모양 안쪽 전체를 번쩍임 색으로 덮음
				half flash = _Flash * _FlashStrength;
				color = Over(color, _FlashColor.rgb, flash * inside);

				// 더하기 성분: 밝은 선만 빛남
				half additive = (front + frontGlow * 0.5 + edge * pulse * 0.6 + shock + flash * inside * 0.5) * _AddStrength;

				color.a *= _Alpha;
				half3 premultiplied = color.rgb * color.a + _FrontColor.rgb * additive * _Alpha;
				return half4(premultiplied, color.a);
			}
			ENDHLSL
		}
	}
}
