// 바닥 위 얇은 판에 자국 캔버스를 그림. 해와 환경광을 받아 밤에 빛나지 않음
Shader "WM/Remains/GroundStain"
{
	Properties
	{
		_StainCanvas ("Canvas", 2D) = "black" {}
		_Opacity ("Opacity", Range(0, 1)) = 0.85
		_Lift ("Lift above ground", Float) = 0.015
	}
	SubShader
	{
		Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent-400" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
		Pass
		{
			Name "GroundStain"
			Tags { "LightMode" = "UniversalForward" }
			Blend SrcAlpha OneMinusSrcAlpha
			ZWrite Off
			Cull Off

			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment Frag
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

			TEXTURE2D(_StainCanvas); SAMPLER(sampler_StainCanvas);
			CBUFFER_START(UnityPerMaterial)
				float _Opacity;
				float _Lift;
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
				float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
				positionWS.y += _Lift;
				output.positionCS = TransformWorldToHClip(positionWS);
				output.uv = input.uv;
				return output;
			}

			half4 Frag(Varyings input) : SV_Target
			{
				half4 stain = SAMPLE_TEXTURE2D(_StainCanvas, sampler_StainCanvas, input.uv);
				// 가장자리 2% 는 옅게 (캔버스 끝이 칼선으로 안 보이게)
				float2 edge = min(input.uv, 1.0 - input.uv);
				half border = saturate(min(edge.x, edge.y) / 0.02);
				Light mainLight = GetMainLight();
				half3 light = mainLight.color * saturate(mainLight.direction.y) + SampleSH(half3(0, 1, 0));
				return half4(stain.rgb * light, saturate(stain.a) * _Opacity * border);
			}
			ENDHLSL
		}
	}
}
