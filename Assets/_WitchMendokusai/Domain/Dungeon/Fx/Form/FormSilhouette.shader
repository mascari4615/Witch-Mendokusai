// 스프라이트 윤곽을 한 색으로. 변신 실루엣 (NinjaFormFx)
Shader "WM/Fx/FormSilhouette"
{
	Properties
	{
		_MainTex ("Sprite", 2D) = "white" {}
		_Color ("Color", Color) = (0, 0, 0, 1)
	}
	SubShader
	{
		Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
		Pass
		{
			Tags { "LightMode"="SRPDefaultUnlit" }
			Blend SrcAlpha OneMinusSrcAlpha
			ZWrite Off
			Cull Off
			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment Frag
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
			CBUFFER_START(UnityPerMaterial)
				float4 _MainTex_ST;
				float4 _Color;
			CBUFFER_END
			struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
			struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

			Varyings Vert(Attributes input)
			{
				Varyings output;
				output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
				output.uv = input.uv;
				return output;
			}

			float4 Frag(Varyings input) : SV_Target
			{
				float alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a;
				return float4(_Color.rgb, _Color.a * alpha);
			}
			ENDHLSL
		}
	}
}
