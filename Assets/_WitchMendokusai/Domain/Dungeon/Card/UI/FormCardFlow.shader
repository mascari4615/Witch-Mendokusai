// 형태 카드의 흐르는 빛. 칸으로 끊은 네 단 색, 흐름은 초당 _Steps 번만 바뀜 (변신 연출과 같은 결). 시간 정지 중에도 돌게 실제 시간 전역값
// _Cover 1 이면 전부 덮고 0 으로 가며 칸 덩어리째 걷힘
Shader "WM/UI/FormCardFlow"
{
	Properties
	{
		[PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
		_Color ("Tint", Color) = (1,1,1,1)
		_Main ("Main", Color) = (1,0.82,0.36,1)
		_Bright ("Bright", Color) = (1,0.97,0.8,1)
		_Dark ("Dark", Color) = (0.36,0.2,0.05,1)
		_Cells ("Cells", Vector) = (86, 125, 0, 0)
		_Cover ("Cover", Range(0,1)) = 1
		_Pillar ("Pillar", Float) = 0
		_Flow ("Flow", Float) = 0.5
		_Steps ("Steps", Float) = 12
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
			fixed4 _Main;
			fixed4 _Bright;
			fixed4 _Dark;
			float4 _Cells;
			float _Cover;
			float _Pillar;
			float _Flow;
			float _Steps;
			float _WMUIUnscaledTime;

			float hash(float2 cell) { return frac(sin(dot(cell, float2(127.1, 311.7))) * 43758.5453); }
			float noise(float2 position)
			{
				float2 cell = floor(position);
				float2 part = frac(position);
				part = part * part * (3.0 - 2.0 * part);
				return lerp(lerp(hash(cell), hash(cell + float2(1, 0)), part.x), lerp(hash(cell + float2(0, 1)), hash(cell + float2(1, 1)), part.x), part.y);
			}

			v2f vert(appdata input)
			{
				v2f output;
				output.pos = UnityObjectToClipPos(input.vertex);
				output.uv = input.uv;
				output.color = input.color * _Color;
				return output;
			}

			fixed4 frag(v2f input) : SV_Target
			{
				float2 cell = floor(input.uv * _Cells.xy);
				float2 uv = (cell + 0.5) / _Cells.xy;
				float clock = floor(_WMUIUnscaledTime * _Steps) / _Steps;
				// 비스듬히 위로 흐르는 굵은 띠
				float2 fromCenter = (uv - 0.5) * float2(_Cells.x / max(_Cells.y, 1.0), 1.0);
				float radius = length(fromCenter) * 1.7;
				float ray = sin(atan2(fromCenter.y, fromCenter.x) * 14.0 + clock * _Flow * 1.6) * 0.5 + 0.5;
				// 가운데가 가장 밝고 빛살이 바깥으로 갈수록 갈림
				float shade = saturate(1.02 - radius * 0.62 + (ray - 0.5) * 0.4 * saturate(radius * 1.6) + (noise(uv * 9.0) - 0.5) * 0.1);
				if (_Pillar > 0.5)
				{
					// 빛기둥: 가운데가 밝고 가장자리는 칸 계단으로 잘림
					float side = abs(uv.x * 2.0 - 1.0);
					clip(1.0 - side - shade * 0.35);
					shade = saturate(1.15 - side * 0.9 + (shade - 0.5) * 0.3);
				}
				float band = floor(saturate(shade) * 4.0);
				fixed4 result;
				result.rgb = band < 1.0 ? _Dark.rgb : (band < 2.0 ? lerp(_Dark.rgb, _Main.rgb, 0.6) : (band < 3.0 ? _Main.rgb : _Bright.rgb));
				float order = noise(uv * 5.0 + 3.0) * 0.6 + hash(floor(cell / 3.0)) * 0.4;
				float left = _Cover * 1.02 - order - 0.001;
				clip(left);
				// 걷히는 가장자리는 밝게
				result.rgb = lerp(result.rgb, _Bright.rgb, step(left, 0.06) * step(_Cover, 0.999));
				result.a = 1.0;
				return result * input.color;
			}
			ENDCG
		}
	}
}
