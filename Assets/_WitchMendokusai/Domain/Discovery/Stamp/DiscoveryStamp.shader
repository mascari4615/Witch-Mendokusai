// 완성 도장 한 장을 렌더 텍스처에 굽는 셰이더 (MonsterStampBaker). 금판 위에 몬스터 그림을 금 양각으로 얹음
// 그림의 색은 버리고 밝기만 금색 네 단계로 옮김. 종마다 밝은 정도가 달라 그 그림의 밝기 평균과 퍼짐을 먼저 재서 그 폭을 펴 씀
Shader "WM/UI/DiscoveryStamp"
{
	Properties
	{
		_MainTex ("Monster Sprite", 2D) = "black" {}
		_Plate ("Gold Plate", 2D) = "white" {}
		_SpriteRect ("Sprite UV Rect (x, y, w, h)", Vector) = (0, 0, 1, 1)
		_SpriteSize ("Sprite Size On Plate (w, h)", Vector) = (0.5, 0.5, 0, 0)
		_Fit ("Longest Side On Plate", Range(0.2, 0.8)) = 0.55
		_Spread ("Brightness Range In Standard Deviations", Range(0.5, 3)) = 1.7
		_MinSpread ("Smallest Standard Deviation", Range(0.01, 0.3)) = 0.06
		_Gold0 ("Gold 0 Dark", Color) = (0.54, 0.32, 0.04, 1)
		_Gold1 ("Gold 1", Color) = (0.84, 0.58, 0.13, 1)
		_Gold2 ("Gold 2", Color) = (0.96, 0.78, 0.31, 1)
		_Gold3 ("Gold 3 Light", Color) = (1, 0.93, 0.63, 1)
		_Shadow ("Shadow", Color) = (0.43, 0.25, 0.02, 1)
		_Light ("Edge Light", Color) = (1, 0.94, 0.69, 1)
		_LightDirection ("Light Direction On Plate (xy)", Vector) = (-1, 1, 0, 0)
		_Offset ("Edge Offset On Plate", Range(0, 0.03)) = 0.01
		_FarShadowScale ("Far Shadow Distance In Edge Offsets", Range(1, 4)) = 2
		_FarShadowStrength ("Far Shadow Strength", Range(0, 1)) = 0.6
	}
	SubShader
	{
		Cull Off
		ZWrite Off
		ZTest Always
		Blend Off
		Pass
		{
			CGPROGRAM
			#pragma target 3.0
			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc"

			sampler2D _MainTex;
			sampler2D _Plate;
			float4 _SpriteRect;
			float4 _SpriteSize;
			float _Spread;
			float _MinSpread;
			float4 _Gold0;
			float4 _Gold1;
			float4 _Gold2;
			float4 _Gold3;
			float4 _Shadow;
			float4 _Light;
			float4 _LightDirection;
			float _Offset;
			float _FarShadowScale;
			float _FarShadowStrength;

			float4 SampleSprite(float2 uv)
			{
				float2 local = (uv - 0.5) / _SpriteSize.xy + 0.5;
				float4 color = tex2Dlod(_MainTex, float4(_SpriteRect.xy + local * _SpriteRect.zw, 0, 0));
				float2 inside = step(0, local) * step(local, 1);
				color.a *= inside.x * inside.y;
				return color;
			}

			// 밝기 (Rec. 709) 는 눈에 보이는 값 (감마) 으로
			float BrightnessOf(float3 color)
			{
				return LinearToGammaSpaceExact(saturate(dot(color, float3(0.2126, 0.7152, 0.0722))));
			}

			#define SAMPLE_COUNT 24

			struct Varyings
			{
				float4 position : SV_POSITION;
				float2 uv : TEXCOORD0;
				nointerpolation float2 brightnessRange : TEXCOORD1;
			};

			// 정점마다 한 번만 잼 (굽는 사각형의 꼭짓점 몇 개)
			Varyings vert(appdata_img input)
			{
				Varyings output;
				output.position = UnityObjectToClipPos(input.vertex);
				output.uv = input.texcoord;
				float3 sums = 0;
				for (int row = 0; row < SAMPLE_COUNT; row++)
				{
					for (int column = 0; column < SAMPLE_COUNT; column++)
					{
						float2 local = (float2(column, row) + 0.5) / SAMPLE_COUNT;
						float4 color = tex2Dlod(_MainTex, float4(_SpriteRect.xy + local * _SpriteRect.zw, 0, 0));
						float brightness = BrightnessOf(color.rgb);
						sums += float3(1, brightness, brightness * brightness) * step(0.5, color.a);
					}
				}
				float count = max(1, sums.x);
				float mean = sums.y / count;
				float deviation = max(_MinSpread, sqrt(max(0, sums.z / count - mean * mean)));
				output.brightnessRange = float2(mean - deviation * _Spread, mean + deviation * _Spread);
				return output;
			}

			float4 frag(Varyings input) : SV_Target
			{
				float4 plate = tex2D(_Plate, input.uv);
				float3 color = plate.rgb;

				float2 toLight = _LightDirection.xy * _Offset;
				float shadowFar = SampleSprite(input.uv + toLight * _FarShadowScale).a;
				float shadowNear = SampleSprite(input.uv + toLight).a;
				float light = SampleSprite(input.uv - toLight).a;
				color = lerp(color, _Shadow.rgb, saturate(max(shadowFar * _FarShadowStrength, shadowNear)));
				color = lerp(color, _Light.rgb, light);

				float4 sprite = SampleSprite(input.uv);
				float brightness = BrightnessOf(sprite.rgb);
				float goldStep = saturate((brightness - input.brightnessRange.x) / (input.brightnessRange.y - input.brightnessRange.x)) * 3.0;
				float3 gold = lerp(_Gold0.rgb, _Gold1.rgb, saturate(goldStep));
				gold = lerp(gold, _Gold2.rgb, saturate(goldStep - 1.0));
				gold = lerp(gold, _Gold3.rgb, saturate(goldStep - 2.0));
				color = lerp(color, gold, sprite.a);

				return float4(color, plate.a);
			}
			ENDCG
		}
	}
}
