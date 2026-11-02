Shader "WM/SwordSwing"
{
	Properties
	{
		[Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Source blend", Float) = 5
		[Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Destination blend", Float) = 10
		[Enum(Off, 0, On, 1)] _ZWrite ("Depth write", Float) = 0
	}
	SubShader
	{
		Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
		Pass
		{
			// 정점마다 평면 색을 그대로 쓰는 로우폴리 칼날과 궤적
			Tags { "LightMode"="SRPDefaultUnlit" }
			Blend [_SrcBlend] [_DstBlend]
			ZWrite [_ZWrite]
			Cull Off
			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment Frag
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			struct Attributes { float4 positionOS : POSITION; float4 color : COLOR; };
			struct Varyings { float4 positionCS : SV_POSITION; nointerpolation float4 color : COLOR; };
			Varyings Vert(Attributes input)
			{
				Varyings output;
				output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
				output.color = input.color;
				return output;
			}
			float4 Frag(Varyings input) : SV_Target
			{
				return input.color;
			}
			ENDHLSL
		}
	}
}
