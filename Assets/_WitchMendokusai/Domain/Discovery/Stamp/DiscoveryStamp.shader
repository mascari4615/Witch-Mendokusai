// 완성 도장 한 장을 렌더 텍스처에 굽는 셰이더 (MonsterStampBaker). 금판 위에 몬스터 그림을 금 양각으로 얹음
// 그림의 색은 버리고 밝기만 금색 네 단계로 옮김. 그래서 어두운 몬스터도 금으로 읽힘
Shader "WM/UI/DiscoveryStamp"
{
	Properties
	{
		_MainTex ("Monster Sprite", 2D) = "black" {}
		_Plate ("Gold Plate", 2D) = "white" {}
		_SpriteRect ("Sprite UV Rect (x, y, w, h)", Vector) = (0, 0, 1, 1)
		_SpriteSize ("Sprite Size On Plate (w, h)", Vector) = (0.5, 0.5, 0, 0)
		_Fit ("Longest Side On Plate", Range(0.2, 0.8)) = 0.55
		_LumLow ("Brightness Mapped To Darkest Gold", Range(0, 1)) = 0.12
		_LumHigh ("Brightness Mapped To Lightest Gold", Range(0, 1)) = 0.92
		_Gold0 ("Gold 0 Dark", Color) = (0.54, 0.32, 0.04, 1)
		_Gold1 ("Gold 1", Color) = (0.84, 0.58, 0.13, 1)
		_Gold2 ("Gold 2", Color) = (0.96, 0.78, 0.31, 1)
		_Gold3 ("Gold 3 Light", Color) = (1, 0.93, 0.63, 1)
		_Shadow ("Shadow", Color) = (0.43, 0.25, 0.02, 1)
		_Light ("Edge Light", Color) = (1, 0.94, 0.69, 1)
		_Offset ("Edge Offset On Plate", Range(0, 0.03)) = 0.01
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
			#pragma vertex vert_img
			#pragma fragment frag
			#include "UnityCG.cginc"

			sampler2D _MainTex;
			sampler2D _Plate;
			float4 _SpriteRect;
			float4 _SpriteSize;
			float _LumLow;
			float _LumHigh;
			float4 _Gold0;
			float4 _Gold1;
			float4 _Gold2;
			float4 _Gold3;
			float4 _Shadow;
			float4 _Light;
			float _Offset;

			float4 SampleSprite(float2 uv)
			{
				float2 local = (uv - 0.5) / _SpriteSize.xy + 0.5;
				float4 color = tex2Dlod(_MainTex, float4(_SpriteRect.xy + local * _SpriteRect.zw, 0, 0));
				float2 inside = step(0, local) * step(local, 1);
				color.a *= inside.x * inside.y;
				return color;
			}

			float4 frag(v2f_img input) : SV_Target
			{
				float4 plate = tex2D(_Plate, input.uv);
				float3 color = plate.rgb;

				float shadowFar = SampleSprite(input.uv + float2(-2, 2) * _Offset).a;
				float shadowNear = SampleSprite(input.uv + float2(-1, 1) * _Offset).a;
				float light = SampleSprite(input.uv + float2(1, -1) * _Offset).a;
				color = lerp(color, _Shadow.rgb, saturate(max(shadowFar * 0.6, shadowNear)));
				color = lerp(color, _Light.rgb, light);

				float4 sprite = SampleSprite(input.uv);
				// 밝기는 눈에 보이는 값 (감마) 으로 셈
				float lum = pow(saturate(dot(sprite.rgb, float3(0.2126, 0.7152, 0.0722))), 1.0 / 2.2);
				float t = saturate((lum - _LumLow) / max(0.001, _LumHigh - _LumLow)) * 3.0;
				float3 gold = lerp(_Gold0.rgb, _Gold1.rgb, saturate(t));
				gold = lerp(gold, _Gold2.rgb, saturate(t - 1.0));
				gold = lerp(gold, _Gold3.rgb, saturate(t - 2.0));
				color = lerp(color, gold, sprite.a);

				return float4(color, plate.a);
			}
			ENDCG
		}
	}
}
