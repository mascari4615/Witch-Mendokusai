Shader "WM/SurvivalFeelParticle"
{
	Properties
	{
		[Toggle] _Ring ("Ring shape", Float) = 0
		_RingWidth ("Ring width", Range(0.02, 0.5)) = 0.16
	}
	SubShader
	{
		Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
		Pass
		{
			// 칼날 효과와 같은 각진 단색. 마름모 조각과 고리
			Tags { "LightMode"="SRPDefaultUnlit" }
			Blend SrcAlpha OneMinusSrcAlpha
			ZWrite Off
			Cull Off
			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment Frag
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			float _Ring;
			float _RingWidth;
			struct Attributes { float4 positionOS : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
			struct Varyings { float4 positionCS : SV_POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
			Varyings Vert(Attributes input)
			{
				Varyings output;
				output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
				output.color = input.color;
				output.uv = input.uv * 2.0 - 1.0;
				return output;
			}
			float4 Frag(Varyings input) : SV_Target
			{
				float inside;
				if (_Ring > 0.5)
				{
					float radius = length(input.uv);
					inside = step(radius, 1.0) * step(1.0 - _RingWidth, radius);
				}
				else
				{
					inside = step(abs(input.uv.x) + abs(input.uv.y), 1.0);
				}
				clip(inside - 0.5);
				return input.color;
			}
			ENDHLSL
		}
	}
}
