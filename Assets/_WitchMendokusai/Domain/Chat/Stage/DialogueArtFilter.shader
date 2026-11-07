// 대화 일러 전용 UI Toolkit 필터. 빛 스침, 윤곽 빛, 숨쉬기, 감정 색, 듣는 쪽 채도 감소
Shader "WM/UI/DialogueArtFilter"
{
	Properties
	{
		_MainTex ("Source", 2D) = "black" {}
		_SweepPhase ("Sweep Phase", Float) = -1
		_Rim ("Rim", Float) = 0
		_Light ("Light", Float) = 0
		_EffectColor ("Effect Color", Color) = (1,1,1,0)
		_SweepWidth ("Sweep Width", Float) = 0.14
		_SweepAngle ("Sweep Angle", Float) = 0.35
		_SweepStrength ("Sweep Strength", Float) = 0.6
		_RimRadius ("Rim Radius", Float) = 3
	}
	SubShader
	{
		Tags { "RenderType"="Transparent" }
		Cull Off
		ZWrite Off
		ZTest Always
		Blend Off
		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc"
			#include "UnityUIEFilter.cginc"

			struct v2f
			{
				float4 pos : SV_POSITION;
				float2 uv : TEXCOORD0;
				nointerpolation float4 uvRect : TEXCOORD1;
			};

			sampler2D _MainTex;
			float _SweepPhase;
			float _Rim;
			float _Light;
			float4 _EffectColor;
			float _SweepWidth;
			float _SweepAngle;
			float _SweepStrength;
			float _RimRadius;

			v2f vert(FilterVertexInput input)
			{
				v2f output;
				output.pos = UnityObjectToClipPos(input.vertex);
				output.uv = input.uv;
				output.uvRect = GetFilterUVRect(GetFilterRectIndex(input));
				return output;
			}

			// 필터 사각형 밖 샘플 차단
			float SampleAlpha(float2 uv, float4 rect)
			{
				float2 clamped = clamp(uv, rect.xy, rect.xy + rect.zw);
				return tex2Dlod(_MainTex, float4(clamped, 0, 0)).a;
			}

			float4 frag(v2f input) : SV_Target
			{
				float4 rect = input.uvRect;
				float4 color = tex2D(_MainTex, input.uv);
				float2 local = (input.uv - rect.xy) / rect.zw;

				// 숨쉬기(양수)와 듣는 쪽 채도 감소(음수)
				if (_Light < 0.0)
				{
					float luminance = dot(color.rgb, float3(0.299, 0.587, 0.114));
					color.rgb = lerp(color.rgb, luminance.xxx, saturate(-_Light));
				}
				else
				{
					color.rgb *= 1.0 + _Light;
				}

				// 감정 색
				color.rgb += _EffectColor.rgb * _EffectColor.a * color.a;

				// 윤곽 빛
				float2 pixel = float2(abs(ddx(input.uv.x)), abs(ddy(input.uv.y))) * _RimRadius;
				float around = 0.0;
				for (int x = -1; x <= 1; x++)
				{
					for (int y = -1; y <= 1; y++)
					{
						around += SampleAlpha(input.uv + pixel * float2(x, y), rect);
					}
				}
				around /= 9.0;
				float inner = saturate(color.a - around);
				float outer = saturate(around - color.a);
				color.rgb += _EffectColor.rgb * (inner + outer) * _Rim;
				color.a = saturate(color.a + outer * _Rim);

				// 빛 스침. 불투명 부분만
				if (_SweepPhase >= 0.0)
				{
					float start = -_SweepWidth;
					float end = 1.0 + _SweepAngle + _SweepWidth;
					float center = lerp(start, end, saturate(_SweepPhase));
					float band = saturate(1.0 - abs(local.x + local.y * _SweepAngle - center) / _SweepWidth);
					color.rgb += band * band * _SweepStrength * color.a;
				}

				return color;
			}
			ENDCG
		}
	}
}
