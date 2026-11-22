// 바닥 액체. 칸 텍스처를 부드럽게 읽고 잡음으로 가장자리를 물방울처럼, 불은 일렁이는 빛, 감전은 깜빡이는 선
Shader "WM/Remains/Goop"
{
	Properties
	{
		_GoopTex ("Goop", 2D) = "black" {}
		_HeatTex ("Heat", 2D) = "black" {}
		[HDR] _FireColor ("Fire", Color) = (3, 1.2, 0.3, 1)
		[HDR] _ShockColor ("Shock", Color) = (0.8, 1.6, 3, 1)
		_TexelCount ("Texels", Float) = 96
		_Lift ("Lift above ground", Float) = 0.02
		_EdgeNoise ("Edge noise", Range(0, 1)) = 0.35
		_Sheen ("Sheen", Range(0, 1)) = 0.25
	}
	SubShader
	{
		Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent-390" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
		Pass
		{
			Name "Goop"
			Tags { "LightMode" = "UniversalForward" }
			Blend SrcAlpha OneMinusSrcAlpha
			ZWrite Off
			Cull Off

			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment Frag
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

			TEXTURE2D(_GoopTex); SAMPLER(sampler_GoopTex);
			TEXTURE2D(_HeatTex); SAMPLER(sampler_HeatTex);
			CBUFFER_START(UnityPerMaterial)
				float4 _FireColor;
				float4 _ShockColor;
				float _TexelCount;
				float _Lift;
				float _EdgeNoise;
				float _Sheen;
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
				float3 positionWS : TEXCOORD1;
			};

			float Hash(float2 p)
			{
				return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
			}

			float ValueNoise(float2 p)
			{
				float2 i = floor(p);
				float2 f = frac(p);
				float2 u = f * f * (3.0 - 2.0 * f);
				return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), u.x), lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), u.x), u.y);
			}

			Varyings Vert(Attributes input)
			{
				Varyings output;
				float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
				positionWS.y += _Lift;
				output.positionWS = positionWS;
				output.positionCS = TransformWorldToHClip(positionWS);
				output.uv = input.uv;
				return output;
			}

			half4 Frag(Varyings input) : SV_Target
			{
				half4 goop = SAMPLE_TEXTURE2D(_GoopTex, sampler_GoopTex, input.uv);
				half4 heat = SAMPLE_TEXTURE2D(_HeatTex, sampler_HeatTex, input.uv);
				float2 world = input.positionWS.xz;
				float noise = ValueNoise(world * 2.3) * 0.6 + ValueNoise(world * 6.1) * 0.4;
				// 칸 경계 대신 둥근 웅덩이: 양을 잡음으로 흔든 뒤 문턱
				float shaped = goop.a * (1.0 + (noise - 0.5) * 2.0 * _EdgeNoise);
				float coverage = smoothstep(0.18, 0.32, shaped);
				Light mainLight = GetMainLight();
				half3 light = mainLight.color * saturate(mainLight.direction.y) + SampleSH(half3(0, 1, 0));
				half3 color = goop.rgb * light;
				// 젖은 윤기: 잡음 밝은 점
				color += _Sheen * smoothstep(0.78, 0.95, ValueNoise(world * 9.0 + _Time.y * 0.2)) * light;

				float flicker = ValueNoise(world * 3.0 + float2(0, -_Time.y * 4.0));
				half3 fire = _FireColor.rgb * heat.r * (0.55 + 0.9 * flicker);
				float arc = step(0.92, frac(ValueNoise(world * 5.0 + _Time.y * 30.0) * 7.0));
				half3 shock = _ShockColor.rgb * heat.g * (0.25 + arc);
				float alpha = max(coverage * goop.a * 1.4, saturate(heat.r * 1.2 + heat.g * arc));
				return half4(color * (1.0 - heat.r * 0.6) + fire + shock, saturate(alpha));
			}
			ENDHLSL
		}
	}
}
