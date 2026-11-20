Shader "WM/MapObjectMarker"
{
	SubShader
	{
		Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
		Pass
		{
			// 맵 오브젝트 바닥 선 표식. 정점 색 그대로, 모양 자르기 없음
			Tags { "LightMode"="SRPDefaultUnlit" }
			Blend SrcAlpha OneMinusSrcAlpha
			ZWrite Off
			Cull Off
			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment Frag
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			struct Attributes { float4 positionOS : POSITION; float4 color : COLOR; };
			struct Varyings { float4 positionCS : SV_POSITION; float4 color : COLOR; };
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
