// 형태 카드의 빛. 그림 없이 흐르는 연기 같은 빛 (그림 칸), 둘레의 가는 빛줄, 고른 뒤의 세로 빛. 시간 정지 중에도 돌게 실제 시간 전역값
// _Mode 0 그림 칸 (아래가 어둠으로 녹음), 1 테두리 (판은 카드보다 _Margin 만큼 큼), 2 세로 빛
Shader "WM/UI/FormCardLight"
{
	Properties
	{
		[PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
		_Color ("Tint", Color) = (1,1,1,1)
		_Dark ("Dark", Color) = (0.1,0.075,0.03,1)
		_Mid ("Mid", Color) = (0.52,0.38,0.08,1)
		_Main ("Main", Color) = (0.98,0.82,0.32,1)
		_Bright ("Bright", Color) = (1,0.98,0.88,1)
		_Size ("Size (pixels)", Vector) = (140, 208, 0, 0)
		_Margin ("Border margin (pixels)", Float) = 60
		_Line ("Border width (pixels)", Float) = 2.5
		_Mode ("Mode", Float) = 0
		_Flow ("Flow speed", Float) = 1
		_StencilComp ("Stencil Comparison", Float) = 8
		_Stencil ("Stencil ID", Float) = 0
		_StencilOp ("Stencil Operation", Float) = 0
		_StencilWriteMask ("Stencil Write Mask", Float) = 255
		_StencilReadMask ("Stencil Read Mask", Float) = 255
		_ColorMask ("Color Mask", Float) = 15
	}
	SubShader
	{
		Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }
		Stencil
		{
			Ref [_Stencil]
			Comp [_StencilComp]
			Pass [_StencilOp]
			ReadMask [_StencilReadMask]
			WriteMask [_StencilWriteMask]
		}
		Cull Off
		Lighting Off
		ZWrite Off
		ZTest [unity_GUIZTestMode]
		Blend SrcAlpha OneMinusSrcAlpha
		ColorMask [_ColorMask]
		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc"

			struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
			struct v2f { float4 pos : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };

			fixed4 _Color;
			fixed4 _Dark;
			fixed4 _Mid;
			fixed4 _Main;
			fixed4 _Bright;
			float4 _Size;
			float _Margin;
			float _Line;
			float _Mode;
			float _Flow;
			float _WMUIUnscaledTime;

			float hash(float2 cell) { return frac(sin(dot(cell, float2(127.1, 311.7))) * 43758.5453); }
			float noise(float2 position)
			{
				float2 cell = floor(position);
				float2 part = frac(position);
				part = part * part * (3.0 - 2.0 * part);
				return lerp(lerp(hash(cell), hash(cell + float2(1, 0)), part.x), lerp(hash(cell + float2(0, 1)), hash(cell + float2(1, 1)), part.x), part.y);
			}
			float fbm(float2 position) { return noise(position) * 0.55 + noise(position * 2.1 + 7.3) * 0.3 + noise(position * 4.3 + 3.1) * 0.15; }

			v2f vert(appdata input)
			{
				v2f output;
				output.pos = UnityObjectToClipPos(input.vertex);
				output.uv = input.uv;
				output.color = input.color * _Color;
				return output;
			}

			// 위로 흐르는 세로 결의 빛
			float smoke(float2 uv, float aspect, float clock)
			{
				float2 position = float2(uv.x * aspect, uv.y);
				float warp = fbm(position * float2(2.2, 1.3) + float2(0.0, -clock * 0.22));
				float body = fbm(position * float2(3.0, 1.5) + float2(warp * 1.3, warp * 0.7 - clock * 0.38));
				float fine = noise(position * float2(7.0, 2.6) + float2(body * 1.1, -clock * 0.7));
				return saturate((body * 0.66 + warp * 0.38 + fine * 0.10 - 0.3) * 1.9);
			}
			float3 shade(float value)
			{
				float3 color = lerp(_Dark.rgb, _Mid.rgb, saturate(value / 0.4));
				color = lerp(color, _Main.rgb, saturate((value - 0.4) / 0.35));
				return lerp(color, _Bright.rgb, saturate((value - 0.75) / 0.25));
			}

			fixed4 frag(v2f input) : SV_Target
			{
				float2 uv = input.uv;
				float clock = _WMUIUnscaledTime * _Flow;
				float aspect = _Size.x / max(_Size.y, 1.0);
				fixed4 result = fixed4(0, 0, 0, 0);
				if (_Mode < 0.5)
				{
					float value = smoke(uv, aspect, clock * 0.7);
					float across = (uv.x - 0.5) / 0.26;
					value = saturate(value * (0.3 + 0.75 * saturate(uv.y * 1.2)) + 0.12 * exp(-across * across));
					result.rgb = shade(value * 0.88);
					result.a = smoothstep(0.0, 0.5, uv.y);
				}
				else if (_Mode < 1.5)
				{
					float2 fromCenter = (uv - 0.5) * _Size.xy;
					float2 outside = abs(fromCenter) - (_Size.xy * 0.5 - _Margin);
					float away = abs(length(max(outside, 0.0)) + min(max(outside.x, outside.y), 0.0));
					// 둘레를 도는 빛 두 줄
					float turn = atan2(fromCenter.y / _Size.y, fromCenter.x / _Size.x) / 6.2832;
					float run = (frac((turn - clock * 0.11) * 2.0) - 0.5) / 0.14;
					run = exp(-run * run);
					float sharp = saturate(_Line + 0.5 - away);
					float glow = (exp(-away / 10.0) * 0.3 + exp(-away / 30.0) * 0.16) * (0.5 + run);
					result.rgb = lerp(_Main.rgb, _Bright.rgb, saturate(sharp * (0.35 + run)));
					result.a = saturate(max(sharp * (0.75 + 0.25 * run), glow));
				}
				else
				{
					float side = abs(uv.x * 2.0 - 1.0);
					float rays = 0.7 + 0.5 * noise(float2(uv.x * 34.0, uv.y * 0.7 - clock * 0.9));
					float body = exp(-side * side * 9.0) * rays;
					result.rgb = lerp(_Main.rgb, _Bright.rgb, saturate(exp(-side * side * 60.0) + body * 0.4));
					result.a = saturate(body) * smoothstep(0.0, 0.1, uv.y) * (1.0 - smoothstep(0.55, 1.0, uv.y));
				}
				return result * input.color;
			}
			ENDCG
		}
	}
}
