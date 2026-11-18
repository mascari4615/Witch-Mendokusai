Shader "WM/UI/LiquidBar"
{
	// 체력 바 채움을 액체처럼 그리는 uGUI 셰이더. 왼쪽에서 오른쪽으로 차는 가로 바 전용
	// 움직임 값은 UIBarLiquid 가 꼭짓점 자료로 넣음
	// - TEXCOORD1: 바 안 위치 x, y (0 ~ 1), 채움 비율, 출렁임 (픽셀)
	// - TEXCOORD2: 물결 높이 (픽셀), 끝 물결 개수, 물결 속도 (초당 바퀴), 윗면 물결 개수
	// 색과 두께만 재질 속성
	Properties
	{
		[PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
		_Color ("Tint", Color) = (1, 1, 1, 1)

		_SurfaceColor ("Surface Highlight Color", Color) = (1, 1, 1, 0.35)
		_SurfaceWidth ("Top Surface Width (px)", Float) = 3
		_EdgeWidth ("Edge Highlight Width (px)", Float) = 3
		_EndFade ("Edge Wave Fade Near Empty And Full (fill ratio)", Float) = 0.05

		_StencilComp ("Stencil Comparison", Float) = 8
		_Stencil ("Stencil ID", Float) = 0
		_StencilOp ("Stencil Operation", Float) = 0
		_StencilWriteMask ("Stencil Write Mask", Float) = 255
		_StencilReadMask ("Stencil Read Mask", Float) = 255
		_ColorMask ("Color Mask", Float) = 15

		[Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
	}

	SubShader
	{
		Tags
		{
			"Queue"="Transparent"
			"IgnoreProjector"="True"
			"RenderType"="Transparent"
			"PreviewType"="Plane"
			"CanUseSpriteAtlas"="True"
		}

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
		Blend One OneMinusSrcAlpha
		ColorMask [_ColorMask]

		Pass
		{
			Name "Default"
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 3.0

			#include "UnityCG.cginc"
			#include "UnityUI.cginc"

			#pragma multi_compile_local _ UNITY_UI_CLIP_RECT
			#pragma multi_compile_local _ UNITY_UI_ALPHACLIP

			#define WM_TAU 6.2831853

			struct appdata_t
			{
				float4 vertex : POSITION;
				float4 color : COLOR;
				float2 texcoord : TEXCOORD0;
				float4 barData : TEXCOORD1;
				float4 waveData : TEXCOORD2;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct v2f
			{
				float4 vertex : SV_POSITION;
				fixed4 color : COLOR;
				float2 texcoord : TEXCOORD0;
				float4 worldPosition : TEXCOORD1;
				float4 barData : TEXCOORD2;
				float4 waveData : TEXCOORD3;
				UNITY_VERTEX_OUTPUT_STEREO
			};

			sampler2D _MainTex;
			fixed4 _Color;
			fixed4 _TextureSampleAdd;
			float4 _ClipRect;
			float4 _MainTex_ST;
			fixed4 _SurfaceColor;
			float _SurfaceWidth;
			float _EdgeWidth;
			float _EndFade;

			v2f vert(appdata_t input)
			{
				v2f output;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
				output.worldPosition = input.vertex;
				output.vertex = UnityObjectToClipPos(output.worldPosition);
				output.texcoord = TRANSFORM_TEX(input.texcoord, _MainTex);
				output.color = input.color * _Color;
				output.barData = input.barData;
				output.waveData = input.waveData;
				return output;
			}

			fixed4 frag(v2f input) : SV_Target
			{
				float2 position = input.barData.xy;
				float fill = input.barData.z;
				float sloshPixels = input.barData.w;
				float waveHeight = input.waveData.x;
				float edgeWaveCount = input.waveData.y;
				float waveSpeed = input.waveData.z;
				float surfaceWaveCount = input.waveData.w;

				// 화면 한 픽셀이 바 안 좌표로 얼마인지. 물결 높이를 픽셀로 받기 위함
				float pixelX = max(fwidth(position.x), 0.00001);
				float pixelY = max(fwidth(position.y), 0.00001);
				float phase = _Time.y * waveSpeed;

				// 거의 빈 바와 거의 찬 바에서는 끝 물결과 기울기 끔. 안 끄면 체력 0 에 물결 조각, 가득 찬 바 오른쪽 끝에 홈이 남음
				float endFadeRatio = max(_EndFade, 0.00001);
				float endFade = saturate(fill / endFadeRatio) * saturate((1.0 - fill) / endFadeRatio);

				// 오른쪽 끝 (채움 경계): 세로 물결 + 출렁임 기울기
				float edgeWave = sin((position.y * edgeWaveCount + phase) * WM_TAU) * waveHeight;
				float edgeTilt = (position.y - 0.5) * 2.0 * sloshPixels;
				float edgeX = fill + (edgeWave + edgeTilt) * pixelX * endFade;
				float insidePixels = (edgeX - position.x) / pixelX;
				float fillAlpha = saturate(insidePixels + 0.5);

				// 윗면: 가로로 흐르는 잔물결 띠. 가득 찬 바에서도 보이는 평소 물결
				float surfaceLine = 1.0 - (_SurfaceWidth + sin((position.x * surfaceWaveCount - phase) * WM_TAU) * waveHeight) * pixelY;
				float surfaceHighlight = saturate((position.y - surfaceLine) / pixelY + 0.5);

				// 끝 경계 바로 안쪽 밝은 띠
				float edgeHighlight = saturate(1.0 - insidePixels / max(_EdgeWidth, 0.00001)) * endFade;

				half4 color = (tex2D(_MainTex, input.texcoord) + _TextureSampleAdd) * input.color;
				float highlight = max(surfaceHighlight, edgeHighlight) * _SurfaceColor.a;
				color.rgb = lerp(color.rgb, _SurfaceColor.rgb, highlight);
				color.a *= fillAlpha;

				#ifdef UNITY_UI_CLIP_RECT
				color.a *= UnityGet2DClipping(input.worldPosition.xy, _ClipRect);
				#endif

				// 바에 Mask 가 붙어 있어 빈 부분은 버림. 안 버리면 가림판이 바 전체를 덮음
				clip(color.a - 0.001);

				color.rgb *= color.a;
				return color;
			}
			ENDCG
		}
	}
}
