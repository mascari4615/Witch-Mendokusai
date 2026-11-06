Shader "WM/SurvivalWeaponFx"
{
	Properties
	{
		_Color ("Color", Color) = (1, 1, 1, 1)
		[Enum(Field, 0, Beam, 1, Flat, 2)] _Shape ("Shape", Float) = 0
		_FillAlpha ("Fill alpha", Range(0, 1)) = 0.08
		_RingAlpha ("Ring or core alpha", Range(0, 1)) = 0.45
		_RingWidth ("Ring width", Range(0.01, 0.5)) = 0.05
		_PulseSpeed ("Pulse speed", Float) = 0
		_PulseAmount ("Pulse amount", Range(0, 1)) = 0
		_Alpha ("Alpha", Range(0, 1)) = 1
	}
	SubShader
	{
		Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
		Pass
		{
			// 바닥 고리, 빛줄기, 궤적. 적과 플레이어를 가리지 않는 낮은 알파
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
				float _Shape;
				float _FillAlpha;
				float _RingAlpha;
				float _RingWidth;
				float _PulseSpeed;
				float _PulseAmount;
				float _Alpha;
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
				float alpha;
				float3 color = _Color.rgb;
				if (_Shape < 0.5)
				{
					float radius = length(input.uv);
					clip(1.0 - radius);
					float pulse = 1.0 + sin(_Time.y * _PulseSpeed) * _PulseAmount;
					alpha = step(1.0 - _RingWidth, radius) > 0.5 ? _RingAlpha : _FillAlpha * pulse;
				}
				else if (_Shape < 1.5)
				{
					float side = 1.0 - abs(input.uv.x);
					float tip = saturate((1.0 - abs(input.uv.y)) * 6.0);
					alpha = saturate(side * 2.0) * tip * _RingAlpha + step(0.7, side) * tip * _FillAlpha;
				}
				else
				{
					color *= input.color.rgb;
					alpha = input.color.a * _RingAlpha;
				}
				return float4(color, alpha * _Color.a * _Alpha);
			}
			ENDHLSL
		}
	}
}
