// 궁극기 연출용 범용 빛 판. 입자, 바닥 판, 세운 판, 화면 위 이미지가 같이 씀
// - 텍스처 x 색 x 꼭짓점 색. 합성 방식은 재질의 _SrcBlend, _DstBlend (더하기 One, 알파 OneMinusSrcAlpha)
// - _Dither: 0 보다 크면 화면 화소 격자 (4x4 순서 디더) 로 알파를 점 무늬로 끊음. 점으로 서서히 나타나고 녹는 물체용. _DitherScale 은 점 한 칸의 화소 수
// - _ClipLine: 판 UV 위의 직선 (xy 법선, z 거리, w 켜짐). 직선 바깥쪽 화소를 버림. 달을 두 쪽으로 가르는 데 씀
// - 값은 UltimateFx 가 코드로 재질을 만들어 넣음 (프로젝트 재질 에셋 없음)
Shader "WM/Fx/UltimateSprite"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
		[HDR] _Color ("Color", Color) = (1, 1, 1, 1)
		[Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 5
		[Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 1
		[Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest", Float) = 4
		_Dither ("Dither", Range(0, 1)) = 0
		_DitherScale ("Dither Scale", Float) = 3
		_ClipLine ("Clip Line", Vector) = (1, 0, 0, 0)
	}

	SubShader
	{
		Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

		Pass
		{
			Name "UltimateSprite"
			Tags { "LightMode" = "SRPDefaultUnlit" }
			Blend [_SrcBlend] [_DstBlend]
			ZWrite Off
			ZTest [_ZTest]
			Cull Off

			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment Frag
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

			TEXTURE2D(_MainTex);
			SAMPLER(sampler_MainTex);
			CBUFFER_START(UnityPerMaterial)
				float4 _MainTex_ST;
				float4 _Color;
				float _Dither;
				float _DitherScale;
				float4 _ClipLine;
			CBUFFER_END

			struct Attributes
			{
				float4 positionOS : POSITION;
				float4 color : COLOR;
				float2 uv : TEXCOORD0;
			};

			struct Varyings
			{
				float4 positionCS : SV_POSITION;
				float4 color : COLOR;
				float2 uv : TEXCOORD0;
				float4 screenPos : TEXCOORD1;
			};

			Varyings Vert(Attributes input)
			{
				Varyings output;
				output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
				output.color = input.color;
				output.uv = TRANSFORM_TEX(input.uv, _MainTex);
				output.screenPos = ComputeScreenPos(output.positionCS);
				return output;
			}

			float BayerThreshold(float2 pixel)
			{
				static const float matrix4[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
				int2 cell = int2(fmod(pixel, 4.0));
				return (matrix4[cell.y * 4 + cell.x] + 0.5) / 16.0;
			}

			half4 Frag(Varyings input) : SV_Target
			{
				half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
				half4 color = tex * _Color * input.color;
				if (_ClipLine.w > 0.5)
				{
					float side = dot(input.uv - 0.5, _ClipLine.xy) - _ClipLine.z;
					clip(side);
				}
				if (_Dither > 0.001)
				{
					float2 pixel = floor(input.screenPos.xy / input.screenPos.w * _ScreenParams.xy / max(_DitherScale, 1.0));
					float keep = step(BayerThreshold(pixel), 1.0 - _Dither);
					color *= keep;
				}
				return color;
			}
			ENDHLSL
		}
	}
}
