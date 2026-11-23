Shader "WM/FxGlowParticle"
{
	// 입자용 빛 (덮어 칠하는 합성, 밝은 풀밭에서도 보이게). 그림 없이 uv 로 모양을 그림: 0 부드러운 원 (섬광), 1 마름모 (늘린 빌보드에서 파편 줄기), 2 띠 (궤적 렌더러, 가로만 옅어짐)
	// 색과 알파는 입자 정점 색 (수명 중 색 곡선)
	Properties
	{
		[Enum(Glow, 0, Diamond, 1, Ribbon, 2)] _Shape ("Shape", Float) = 1
		_Falloff ("Falloff power", Range(0.3, 6)) = 1.6
		_Intensity ("Intensity", Range(0, 6)) = 2
	}
	SubShader
	{
		Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
		Pass
		{
			Tags { "LightMode"="SRPDefaultUnlit" }
			Blend One OneMinusSrcAlpha
			ZWrite Off
			Cull Off
			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment Frag
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			CBUFFER_START(UnityPerMaterial)
				float _Shape;
				float _Falloff;
				float _Intensity;
			CBUFFER_END
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
				float distance = _Shape < 0.5 ? length(input.uv) : (_Shape < 1.5 ? abs(input.uv.x) + abs(input.uv.y) : abs(input.uv.y));
				float shape = pow(saturate(1.0 - distance), _Falloff);
				float alpha = shape * input.color.a;
				return float4(input.color.rgb * alpha * _Intensity, alpha);
			}
			ENDHLSL
		}
	}
}
