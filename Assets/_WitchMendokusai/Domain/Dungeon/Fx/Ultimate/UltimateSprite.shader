// 궁극기 연출용 범용 빛 판. 입자, 바닥 판, 화면 위 이미지가 같이 씀
// - 텍스처 x 색 x 꼭짓점 색. 합성 방식은 재질의 _SrcBlend, _DstBlend (더하기 One, 알파 OneMinusSrcAlpha)
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
			};

			Varyings Vert(Attributes input)
			{
				Varyings output;
				output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
				output.color = input.color;
				output.uv = TRANSFORM_TEX(input.uv, _MainTex);
				return output;
			}

			half4 Frag(Varyings input) : SV_Target
			{
				half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
				return tex * _Color * input.color;
			}
			ENDHLSL
		}
	}
}
