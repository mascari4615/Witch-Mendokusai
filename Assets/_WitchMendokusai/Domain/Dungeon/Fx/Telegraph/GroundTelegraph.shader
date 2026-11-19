// 바닥 예고 판 (2차). 바닥에 눕힌 사각판 (XZ, UV 0~1) 에 원 또는 직사각 띠를 거리 함수로 그림
// - 원: 중심에서 바깥으로 차오름. 띠: UV 의 v 가 0 인 쪽 (시작점) 에서 1 쪽으로 차오름
// - 길이는 전부 월드 단위. 판 크기와 모양 반크기는 스크립트가 넘김 (판은 모양보다 바깥 빛과 충격파만큼 큼)
// - 칠한 원판이 아니라 빛 선 위주. 겹 순서 (아래에서 위)
//   1. 얇은 막: 안쪽 전체, 알파 아주 낮음
//   2. 차오른 막: 막보다 조금 짙고 앞선 쪽이 밝음
//   3. 무늬: 원은 마법진 (안쪽 고리, 룬 획, 별 다각형과 그 고리, 천천히 회전), 띠는 앞으로 흐르는 화살 무늬
//   4. 가장자리: 색 띠 + 거의 흰 심 + 바깥으로 부드러운 번짐 (번짐은 더하기 위주)
//   5. 차오르는 앞선의 밝은 선
//   6. 가장자리 위 반짝이는 점
//   7. 발동: 가장자리 번쩍임, 바깥으로 퍼지는 충격파, 안쪽 옅은 번쩍임
// - 출력은 곱한 알파. 빛 성분은 알파 없이 rgb 에 더함 (HDR 카메라면 1 넘는 값이 번짐)
// - 값은 GroundTelegraph.cs 와 파생 클래스가 MaterialPropertyBlock 으로 매 프레임 넣음
Shader "WM/Fx/GroundTelegraph"
{
	Properties
	{
		[Enum(Circle, 0, Line, 1)] _Shape ("Shape", Float) = 0
		_BaseColor ("Membrane Color", Color) = (1, 0.35, 0.22, 0.1)
		_FillColor ("Fill Color", Color) = (1, 0.32, 0.18, 0.22)
		_FrontColor ("Fill Front Color", Color) = (1, 0.85, 0.62, 1)
		_EdgeColor ("Edge Color", Color) = (1, 0.38, 0.2, 0.85)
		_CoreColor ("Edge Core Color", Color) = (1, 0.93, 0.82, 1)
		_PatternColor ("Pattern Color", Color) = (1, 0.55, 0.35, 0.22)
		_SparkColor ("Spark Color", Color) = (1, 0.85, 0.6, 1)
		_FlashColor ("Flash Color", Color) = (1, 0.9, 0.75, 1)
		_MembraneCenter ("Membrane Center Ratio", Range(0, 1)) = 0.55
		_FillDepthShade ("Fill Depth Shade", Range(0, 1)) = 0.65
		_EdgeWidth ("Edge Width (world)", Float) = 0.06
		_CoreWidth ("Edge Core Width (world)", Float) = 0.022
		_CoreIntensity ("Core Intensity (HDR)", Float) = 1.6
		_GlowWidth ("Glow Width (world)", Float) = 0.3
		_GlowStrength ("Glow Strength", Float) = 0.8
		_GlowInner ("Inner Glow Ratio", Range(0, 1)) = 0.35
		_GlowOpacity ("Glow Opacity", Range(0, 1)) = 0.2
		_FrontWidth ("Fill Front Half Width (world)", Float) = 0.018
		_FrontGlowWidth ("Fill Front Glow Width (world)", Float) = 0.5
		_FrontGlowAdd ("Fill Front Glow Add", Float) = 0.35
		_PulseAmount ("Edge Pulse Amount", Range(0, 1)) = 0.2
		_PulseSpeed ("Edge Pulse Speed", Float) = 7
		_PatternFilledBoost ("Pattern Boost In Fill", Float) = 1.6
		_PatternGlow ("Pattern Glow Add", Float) = 0.25
		_InnerRingInset ("Magic Ring Inset (world)", Float) = 0.32
		_InnerRingWidth ("Magic Ring Width (world)", Float) = 0.016
		_PatternLineWidth ("Pattern Line Width (world)", Float) = 0.016
		_RuneCount ("Rune Count", Float) = 32
		_RuneLength ("Rune Length (world)", Float) = 0.16
		_RuneLengthMin ("Rune Length Min Ratio", Range(0, 1)) = 0.4
		_RuneCrossChance ("Rune Cross Chance", Range(0, 1)) = 0.35
		_RuneCrossWidth ("Rune Cross Width (world)", Float) = 0.07
		_StarPoints ("Star Points (3~8)", Float) = 6
		_StarStep ("Star Step", Float) = 2
		_StarRadius ("Star Radius Ratio", Range(0, 1)) = 0.72
		_PatternSpeed ("Pattern Rotate Speed (rad/s)", Float) = 0.12
		_ChevronSpacing ("Chevron Spacing (world)", Float) = 0.7
		_ChevronSlope ("Chevron Slope", Float) = 0.6
		_ChevronWidth ("Chevron Width (world)", Float) = 0.05
		_ChevronSpeed ("Chevron Speed", Float) = 1.2
		_ChevronStrength ("Chevron Strength", Range(0, 1)) = 0.6
		_ChevronMargin ("Chevron Margin (world)", Float) = 0.18
		[ToggleUI] _InwardArrows ("Circle Inward Arrows (pull)", Float) = 0
		_ArrowSectors ("Inward Arrow Sectors", Float) = 8
		_ArrowArmWidth ("Inward Arrow Arm Width (sector ratio)", Range(0, 0.5)) = 0.36
		_ArrowInnerClear ("Inward Arrow Inner Clear Radius (world)", Float) = 1
		_ArrowArmSoftness ("Inward Arrow Arm Edge Softness (sector ratio)", Range(0, 0.2)) = 0.05
		_LineEndEdge ("Line End Edge Ratio", Range(0, 1)) = 0.45
		_CornerRadius ("Line Corner Radius (world)", Float) = 0.1
		_SparkSpacing ("Spark Spacing (world)", Float) = 0.55
		_SparkSize ("Spark Size (world)", Float) = 0.035
		_SparkJitter ("Spark Jitter (world)", Float) = 0.04
		_SparkSpeed ("Spark Flow Speed", Float) = 0.6
		_SparkTwinkleSpeed ("Spark Twinkle Speed", Float) = 5
		_SparkStrength ("Spark Strength", Float) = 1.4
		_Progress ("Progress", Range(0, 1)) = 0.5
		_Appear ("Appear (set by script)", Range(0, 1)) = 1
		_Flash ("Flash (set by script)", Range(0, 1)) = 0
		_FlashStrength ("Inner Flash Strength", Range(0, 1)) = 0.3
		_FlashEdgeBoost ("Flash Edge Boost", Float) = 2.5
		_Shock ("Shockwave (set by script)", Range(0, 1)) = 0
		_ShockDistance ("Shockwave Travel (world)", Float) = 0.9
		_ShockWidth ("Shockwave Half Width (world)", Float) = 0.06
		_AddStrength ("Additive Strength", Float) = 1
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

			// 곱한 알파. rgb 에 빛 성분을 얹어 밝은 선이 빛나게
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
				float4 _BaseColor;
				float4 _FillColor;
				float4 _FrontColor;
				float4 _EdgeColor;
				float4 _CoreColor;
				float4 _PatternColor;
				float4 _SparkColor;
				float4 _FlashColor;
				float _MembraneCenter;
				float _FillDepthShade;
				float _EdgeWidth;
				float _CoreWidth;
				float _CoreIntensity;
				float _GlowWidth;
				float _GlowStrength;
				float _GlowInner;
				float _GlowOpacity;
				float _FrontWidth;
				float _FrontGlowWidth;
				float _FrontGlowAdd;
				float _PulseAmount;
				float _PulseSpeed;
				float _PatternFilledBoost;
				float _PatternGlow;
				float _InnerRingInset;
				float _InnerRingWidth;
				float _PatternLineWidth;
				float _RuneCount;
				float _RuneLength;
				float _RuneLengthMin;
				float _RuneCrossChance;
				float _RuneCrossWidth;
				float _StarPoints;
				float _StarStep;
				float _StarRadius;
				float _PatternSpeed;
				float _ChevronSpacing;
				float _ChevronSlope;
				float _ChevronWidth;
				float _ChevronSpeed;
				float _ChevronStrength;
				float _ChevronMargin;
				float _InwardArrows;
				float _ArrowSectors;
				float _ArrowArmWidth;
				float _ArrowInnerClear;
				float _ArrowArmSoftness;
				float _LineEndEdge;
				float _CornerRadius;
				float _SparkSpacing;
				float _SparkSize;
				float _SparkJitter;
				float _SparkSpeed;
				float _SparkTwinkleSpeed;
				float _SparkStrength;
				float _Progress;
				float _Appear;
				float _Flash;
				float _FlashStrength;
				float _FlashEdgeBoost;
				float _Shock;
				float _ShockDistance;
				float _ShockWidth;
				float _AddStrength;
				float _Alpha;
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
			float4 Over(float4 under, float3 overColor, float overAlpha)
			{
				float clampedAlpha = saturate(overAlpha);
				float outAlpha = clampedAlpha + under.a * (1.0 - clampedAlpha);
				float3 outColor = (overColor * clampedAlpha + under.rgb * under.a * (1.0 - clampedAlpha)) / max(outAlpha, 1e-4);
				return float4(outColor, outAlpha);
			}

			// 거리 0 을 중심으로 한 반굵기 halfWidth 의 선 (안티에일리어싱 포함)
			float Band(float distanceValue, float halfWidth, float aa)
			{
				return 1.0 - smoothstep(halfWidth - aa, halfWidth + aa, abs(distanceValue));
			}

			// 0~1 의사 난수
			float Hash11(float value)
			{
				return frac(sin(value * 127.1 + 311.7) * 43758.5453);
			}

			// 점에서 선분 a-b 까지
			float SegmentDistance(float2 position, float2 pointA, float2 pointB)
			{
				float2 toPosition = position - pointA;
				float2 segment = pointB - pointA;
				float along = saturate(dot(toPosition, segment) / max(dot(segment, segment), 1e-6));
				return length(toPosition - segment * along);
			}

			// 마법진 무늬: 안쪽 고리, 고리와 가장자리 사이 룬 획, 별 다각형과 그 고리. 0~1
			float MagicCircle(float2 position, float radial, float turn, float radius, float aa)
			{
				float rotation = _Time.y * _PatternSpeed;
				float lineHalf = _PatternLineWidth * 0.5;
				float ringRadius = max(radius - _InnerRingInset, 0.0);
				float ring = Band(radial - ringRadius, _InnerRingWidth * 0.5, aa);

				// 룬 획: 고리 바깥에서 가장자리 쪽으로 뻗는 짧은 방사 획. 일부는 가로 획이 붙음
				float runeCount = max(floor(_RuneCount), 1.0);
				float runeCoord = frac(turn + rotation / TWO_PI) * runeCount;
				float runeCell = floor(runeCoord);
				float runeArc = (frac(runeCoord) - 0.5) * TWO_PI * radial / runeCount;
				float runeLength = _RuneLength * lerp(_RuneLengthMin, 1.0, Hash11(runeCell));
				float runeStart = ringRadius + _InnerRingWidth;
				float runeRadial = Band(radial - (runeStart + runeLength * 0.5), runeLength * 0.5, aa);
				float rune = Band(runeArc, lineHalf, aa) * runeRadial;
				float crossOn = step(1.0 - _RuneCrossChance, Hash11(runeCell + 17.0));
				float crossAt = runeStart + runeLength * lerp(0.35, 0.85, Hash11(runeCell + 31.0));
				float runeCross = Band(radial - crossAt, lineHalf, aa) * Band(runeArc, _RuneCrossWidth * 0.5, aa) * crossOn;

				// 별 다각형: 고리 안쪽 원 위의 꼭짓점을 StarStep 칸씩 건너 이음. 고리와 반대로 돎
				float starRadius = ringRadius * _StarRadius;
				float starRotation = -rotation;
				float points = clamp(round(_StarPoints), 3.0, 8.0);
				float stepCount = clamp(round(_StarStep), 1.0, points - 1.0);
				float starDistance = 1e5;
				[loop]
				for (int i = 0; i < 8; i++)
				{
					float index = (float)i;
					if (index >= points)
						break;
					float indexB = index + stepCount;
					indexB = indexB >= points ? indexB - points : indexB;
					float angleA = starRotation + TWO_PI * index / points;
					float angleB = starRotation + TWO_PI * indexB / points;
					float2 pointA = float2(sin(angleA), cos(angleA)) * starRadius;
					float2 pointB = float2(sin(angleB), cos(angleB)) * starRadius;
					starDistance = min(starDistance, SegmentDistance(position, pointA, pointB));
				}
				float star = Band(starDistance, lineHalf, aa);
				float starRing = Band(radial - starRadius, lineHalf, aa);

				return max(max(ring, max(rune, runeCross)), max(star, starRing));
			}

			// 화살 무늬: 진행 방향 (+y) 으로 꺾인 V 줄이 앞으로 흐름. 0~1
			float Chevrons(float2 position, float shapeDistance, float aa)
			{
				float spacing = max(_ChevronSpacing, 1e-4);
				float chevronCoord = (position.y + abs(position.x) * _ChevronSlope) / spacing - _Time.y * _ChevronSpeed;
				float stripe = (frac(chevronCoord) - 0.5) * spacing / sqrt(1.0 + _ChevronSlope * _ChevronSlope);
				float chevronMask = 1.0 - smoothstep(-_ChevronMargin - aa, -_ChevronMargin + aa, shapeDistance);
				return Band(stripe, _ChevronWidth * 0.5, aa) * chevronMask * _ChevronStrength;
			}

			// 안쪽 화살 무늬 (끌어당김 원): 부채 칸마다 중심을 향한 V 줄이 안쪽으로 흐름. 화살 값은 띠 화살과 같은 _Chevron 값. 0~1
			float InwardArrows(float radial, float turn, float shapeDistance, float aa)
			{
				float sectors = max(floor(_ArrowSectors), 1.0);
				float sectorCoord = frac(turn * sectors) - 0.5;
				float arcOffset = sectorCoord * TWO_PI * radial / sectors;
				float spacing = max(_ChevronSpacing, 1e-4);
				float arrowCoord = (-radial + abs(arcOffset) * _ChevronSlope) / spacing - _Time.y * _ChevronSpeed;
				float stripe = (frac(arrowCoord) - 0.5) * spacing / sqrt(1.0 + _ChevronSlope * _ChevronSlope);
				float edgeMask = 1.0 - smoothstep(-_ChevronMargin - aa, -_ChevronMargin + aa, shapeDistance);
				float innerMask = smoothstep(_ArrowInnerClear, _ArrowInnerClear + spacing, radial);
				float armMask = 1.0 - smoothstep(_ArrowArmWidth - _ArrowArmSoftness, _ArrowArmWidth + _ArrowArmSoftness, abs(sectorCoord));
				return Band(stripe, _ChevronWidth * 0.5, aa) * edgeMask * innerMask * armMask * _ChevronStrength;
			}

			float4 Frag(Varyings input) : SV_Target
			{
				// 판 중심 기준 월드 단위 좌표. y 가 차오르는 축 (띠)
				float2 position = (input.uv - 0.5) * _PlateSize.xy;
				float2 halfSize = _ShapeHalfSize.xy;
				bool isCircle = _Shape < 0.5;
				float radial = length(position);
				// 정중앙 atan2(0, 0) 을 피하려고 y 에 아주 작은 값을 더함
				float turn = atan2(position.x, position.y + 1e-6) / TWO_PI + 0.5;

				// shapeDistance: 모양 가장자리까지 (안쪽 음수). fillDistance: 차오름 앞선까지 (차오른 쪽 음수)
				// sweep: 나타날 때 가장자리가 그려지는 순서 0~1. along: 가장자리를 따라 잰 길이 (반짝이 점)
				// sideWeight: 가장자리 선 세기. 띠는 긴 변 1, 짧은 끝 변은 낮춤
				float shapeDistance;
				float fillDistance;
				float sweep;
				float along;
				float alongLength;
				float sideWeight;
				if (isCircle)
				{
					shapeDistance = radial - halfSize.x;
					fillDistance = radial - _Progress * halfSize.x;
					sweep = turn;
					alongLength = TWO_PI * halfSize.x;
					along = turn * alongLength;
					sideWeight = 1.0;
				}
				else
				{
					float corner = min(_CornerRadius, min(halfSize.x, halfSize.y));
					float2 toCorner = abs(position) - (halfSize - corner);
					shapeDistance = length(max(toCorner, 0.0)) + min(max(toCorner.x, toCorner.y), 0.0) - corner;
					fillDistance = (position.y + halfSize.y) - _Progress * halfSize.y * 2.0;
					sweep = saturate((position.y + halfSize.y) / max(halfSize.y * 2.0, 1e-4));
					alongLength = 0.0;
					along = position.y;
					float sideBlend = saturate((toCorner.x - toCorner.y) / max(corner, 1e-3) * 0.5 + 0.5);
					sideWeight = lerp(_LineEndEdge, 1.0, sideBlend);
				}

				// 모든 선의 안티에일리어싱 폭: 화면 한 칸의 월드 길이
				float aa = max(fwidth(shapeDistance), 1e-5);
				float appearFade = saturate(_Appear);
				float shown = step(sweep, _Appear * 1.02);

				float inside = 1.0 - smoothstep(-aa, aa, shapeDistance);
				float filled = inside * (1.0 - smoothstep(-aa, aa, fillDistance));

				// 1. 얇은 막: 가운데는 더 옅음
				float depth = saturate(-shapeDistance / max(min(halfSize.x, halfSize.y), 1e-4));
				float membraneAlpha = _BaseColor.a * lerp(1.0, _MembraneCenter, depth) * inside * appearFade;
				float4 color = float4(_BaseColor.rgb, membraneAlpha);

				// 2. 차오른 막: 앞선 쪽이 짙음
				float frontNear = saturate(1.0 + fillDistance / max(_FrontGlowWidth, 1e-4));
				color = Over(color, _FillColor.rgb, _FillColor.a * filled * lerp(_FillDepthShade, 1.0, frontNear));

				// 3. 무늬
				float pattern = isCircle
					? (_InwardArrows > 0.5 ? InwardArrows(radial, turn, shapeDistance, aa) : MagicCircle(position, radial, turn, halfSize.x, aa))
					: Chevrons(position, shapeDistance, aa);
				pattern *= inside * appearFade * lerp(1.0, _PatternFilledBoost, filled);
				color = Over(color, _PatternColor.rgb, _PatternColor.a * pattern);

				// 4. 가장자리: 선 중심은 가장자리 바로 안쪽. 번짐은 바깥이 강함
				float pulse = 1.0 - _PulseAmount * (0.5 + 0.5 * sin(_Time.y * _PulseSpeed));
				float edgeBoost = 1.0 + _Flash * _FlashEdgeBoost;
				float edgeCenter = shapeDistance + _EdgeWidth * 0.5;
				float edgeVisible = shown * sideWeight;
				float edgeBody = Band(edgeCenter, _EdgeWidth * 0.5, aa) * edgeVisible;
				float core = Band(edgeCenter, _CoreWidth * 0.5, aa) * edgeVisible;
				float glowFalloff = saturate(1.0 - abs(edgeCenter) / max(_GlowWidth, 1e-5));
				float glowSide = edgeCenter > 0.0 ? 1.0 : _GlowInner;
				float glow = glowFalloff * glowFalloff * glowSide * _GlowStrength * edgeVisible * pulse;
				color = Over(color, _EdgeColor.rgb, _GlowOpacity * saturate(glow));
				color = Over(color, _EdgeColor.rgb, _EdgeColor.a * edgeBody * pulse);
				color = Over(color, _CoreColor.rgb, _CoreColor.a * core);

				// 5. 차오르는 앞선 (진행도 0 근처에서는 숨김)
				float frontOn = saturate(_Progress * 20.0) * inside;
				float front = Band(fillDistance, _FrontWidth, aa) * frontOn;
				float frontGlow = frontNear * frontNear * filled * frontOn;
				color = Over(color, _FrontColor.rgb, _FrontColor.a * front);

				// 6. 반짝이는 점: 가장자리를 따라 칸마다 하나, 흐르며 깜빡임. 원은 칸 수를 둘레에 맞춰 이음매를 없앰
				float sparkSpacing = max(_SparkSpacing, 1e-4);
				float sparkCellCount = 1.0;
				if (isCircle)
				{
					sparkCellCount = max(round(alongLength / sparkSpacing), 1.0);
					sparkSpacing = alongLength / sparkCellCount;
				}
				float sparkCoord = along / max(sparkSpacing, 1e-4) + _Time.y * _SparkSpeed;
				float sparkCell = floor(sparkCoord);
				float sparkKey = isCircle ? sparkCell - sparkCellCount * floor(sparkCell / sparkCellCount) : sparkCell + (position.x > 0.0 ? 57.0 : 0.0);
				float sparkHash = Hash11(sparkKey);
				float sparkOffset = (frac(sparkCoord) - 0.5 - (sparkHash - 0.5) * 0.5) * sparkSpacing;
				float sparkRadial = edgeCenter - (Hash11(sparkKey + 91.0) - 0.5) * _SparkJitter;
				float sparkDistanceSquared = sparkOffset * sparkOffset + sparkRadial * sparkRadial;
				// 깜빡임: 사인의 양수 쪽만 네제곱해 짧게 반짝임
				float twinkleWave = saturate(sin(_Time.y * _SparkTwinkleSpeed + sparkHash * TWO_PI));
				twinkleWave *= twinkleWave;
				float twinkle = twinkleWave * twinkleWave;
				float spark = exp(-sparkDistanceSquared / max(_SparkSize * _SparkSize, 1e-6)) * twinkle * _SparkStrength * edgeVisible;

				// 7. 발동: 충격파 고리가 가장자리에서 바깥으로 퍼지며 옅어짐, 안쪽 옅은 번쩍임
				float shockAt = _Shock * _ShockDistance;
				float shockLife = saturate(_Shock * 30.0) * (1.0 - _Shock);
				float shockHalf = _ShockWidth * (1.0 - _Shock * 0.6);
				float shock = Band(shapeDistance - shockAt, shockHalf, aa) * shockLife;
				float shockGlowFalloff = saturate(1.0 - abs(shapeDistance - shockAt) / max(shockHalf * 4.0, 1e-5));
				float shockGlow = shockGlowFalloff * shockGlowFalloff * shockLife;
				float flash = _Flash * _FlashStrength * inside;
				color = Over(color, _FlashColor.rgb, shock);
				color = Over(color, _FlashColor.rgb, flash);

				// 빛 성분 (알파 없이 더함)
				float3 light = _EdgeColor.rgb * (glow + edgeBody * pulse * 0.5) * edgeBoost;
				light += _CoreColor.rgb * core * _CoreIntensity * edgeBoost;
				light += _FrontColor.rgb * (front * _CoreIntensity + frontGlow * _FrontGlowAdd);
				light += _PatternColor.rgb * pattern * _PatternGlow;
				light += _SparkColor.rgb * spark;
				light += _FlashColor.rgb * ((shock + shockGlow * 0.5) * _CoreIntensity + flash * 0.5);
				light *= _AddStrength;

				color.a *= _Alpha;
				float3 premultiplied = color.rgb * color.a + light * _Alpha;
				return float4(premultiplied, color.a);
			}
			ENDHLSL
		}
	}
}
