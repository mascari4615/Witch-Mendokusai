// 선택 칸 위 이동 빛. 시간 정지 중에도 돌도록 실제 시간 전역값 사용
Shader "WM/UI/Sheen"
{
	Properties
	{
		[PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
		_Color ("Tint", Color) = (1,1,1,1)
		_SheenWidth ("Width", Float) = 0.12
		_SheenSpeed ("Speed", Float) = 0.6
		_SheenAngle ("Angle", Float) = 0.35
		_StencilComp ("Stencil Comparison", Float) = 8
		_Stencil ("Stencil ID", Float) = 0
		_StencilOp ("Stencil Operation", Float) = 0
		_StencilWriteMask ("Stencil Write Mask", Float) = 255
		_StencilReadMask ("Stencil Read Mask", Float) = 255
		_ColorMask ("Color Mask", Float) = 15
	}
	SubShader
	{
		Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
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
		Blend SrcAlpha One
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
			float _SheenWidth;
			float _SheenSpeed;
			float _SheenAngle;
			float _WMUIUnscaledTime;

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
				float cycle = frac(_WMUIUnscaledTime * _SheenSpeed) * 2.0 - 0.5;
				float distanceToBand = abs(input.uv.x + input.uv.y * _SheenAngle - cycle);
				float band = saturate(1.0 - distanceToBand / _SheenWidth);
				fixed4 result = input.color;
				result.a *= band * band;
				return result;
			}
			ENDCG
		}
	}
}
