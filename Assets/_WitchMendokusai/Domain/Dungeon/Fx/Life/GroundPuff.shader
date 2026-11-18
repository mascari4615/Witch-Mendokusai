Shader "WM/GroundPuff"
{
	// 등장 때 발밑에 깔리는 짧은 먼지 고리. 바닥에 누운 판 하나
	// 값은 MonsterLifeFxDriver 가 렌더러별 속성 블록으로 넣음
	Properties
	{
		_Color ("Color", Color) = (1, 1, 1, 1)
		_Shape ("Inner ratio, Softness", Vector) = (0, 0.25, 0, 0)
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

			CBUFFER_START(UnityPerMaterial)
				float4 _Color;
				float4 _Shape;
			CBUFFER_END

			struct Attributes
			{
				float4 positionOS : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct Varyings
			{
				float4 positionCS : SV_POSITION;
				float2 uv : TEXCOORD0;
			};

			Varyings Vert(Attributes input)
			{
				Varyings output;
				output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
				output.uv = input.uv * 2.0 - 1.0;
				return output;
			}

			float4 Frag(Varyings input) : SV_Target
			{
				float radius = length(input.uv);
				float softness = max(_Shape.y, 0.0001);
				float inner = smoothstep(_Shape.x - softness, _Shape.x, radius);
				float outer = 1.0 - smoothstep(1.0 - softness, 1.0, radius);
				return float4(_Color.rgb, _Color.a * inner * outer);
			}
			ENDHLSL
		}
	}
}
