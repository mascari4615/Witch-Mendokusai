// 바닥 자국 캔버스 손질. 0 밀기 (바깥은 지움), 1 옅게 (알파 빼기), 2 도장 (GL 사각형)
Shader "Hidden/WM/Remains/StainUtility"
{
	Properties
	{
		_MainTex ("Canvas", 2D) = "black" {}
		_StampTex ("Stamp", 2D) = "white" {}
	}
	SubShader
	{
		Tags { "RenderPipeline" = "UniversalPipeline" }
		ZWrite Off ZTest Always Cull Off

		HLSLINCLUDE
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

		TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
		TEXTURE2D(_StampTex); SAMPLER(sampler_StampTex);
		float4 _Scroll;
		float _Fade;

		struct Attributes
		{
			float4 positionOS : POSITION;
			float2 uv : TEXCOORD0;
			float4 color : COLOR;
		};

		struct Varyings
		{
			float4 positionCS : SV_POSITION;
			float2 uv : TEXCOORD0;
			float4 color : COLOR;
		};

		Varyings Vert(Attributes input)
		{
			Varyings output;
			output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
			output.uv = input.uv;
			output.color = input.color;
			return output;
		}
		ENDHLSL

		Pass
		{
			Name "Scroll"
			Blend Off
			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment Frag
			half4 Frag(Varyings input) : SV_Target
			{
				float2 uv = input.uv + _Scroll.xy;
				if (any(uv < 0.0) || any(uv > 1.0))
					return half4(0, 0, 0, 0);
				return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
			}
			ENDHLSL
		}

		Pass
		{
			Name "Fade"
			Blend Off
			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment Frag
			half4 Frag(Varyings input) : SV_Target
			{
				half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
				color.a = saturate(color.a - _Fade);
				return color;
			}
			ENDHLSL
		}

		Pass
		{
			Name "Stamp"
			// 색은 알파로 섞고, 알파는 더함 (겹친 얼룩이 진해짐)
			Blend SrcAlpha OneMinusSrcAlpha, One One
			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment Frag
			half4 Frag(Varyings input) : SV_Target
			{
				half mask = SAMPLE_TEXTURE2D(_StampTex, sampler_StampTex, input.uv).a;
				return half4(input.color.rgb, input.color.a * mask);
			}
			ENDHLSL
		}
	}
}
