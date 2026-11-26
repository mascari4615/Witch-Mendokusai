// 그린 텍스처로 만드는 이펙트 한 겹 (FxQuadBatch 가 채우는 쿼드, 파티클도 같은 셰이더)
// - 텍스처는 알파를 미리 곱한 그림 (RGB 빛, A 덮는 정도). 검은 바탕 그림을 그대로 구움
// - COLOR 는 HDR 색 곱과 불투명도. TEXCOORD0 은 uv 와 침식 (z) 과 씨앗 (w)
// - TEXCOORD1 은 덮음 (x, 0 더하기 1 덮기), 소용돌이 (y), 돌림 라디안 (z), 흐름 왜곡 (w). 파티클은 전부 0
// - 침식은 어두운 곳부터 노이즈로 깎이고 깎이는 가장자리가 달아오름
Shader "WM/Fx/Painted"
{
	Properties
	{
		_MainTex ("Painted (premultiplied)", 2D) = "black" {}
		_NoiseTex ("Noise (R soft, G wisp)", 2D) = "gray" {}
		_NoiseScale ("Erosion noise scale", Float) = 1.3
		[HDR] _EdgeColor ("Erosion edge", Color) = (2.4, 1.9, 0.7, 1)
		_EdgeWidth ("Erosion edge width", Range(0.01, 0.5)) = 0.14
		_DistortScale ("Distort noise scale", Float) = 0.8
		_FlowSpeed ("Flow speed", Float) = 0.9
		_ParticleCover ("Cover for particles", Range(0, 1)) = 0
		_Intensity ("Intensity", Float) = 1
		[Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest", Float) = 4
	}
	SubShader
	{
		Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
		Pass
		{
			Name "PAINTED"
			Tags { "LightMode"="SRPDefaultUnlit" }
			Blend One OneMinusSrcAlpha
			ZWrite Off
			ZTest [_ZTest]
			Cull Off
			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment Frag
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
			TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);
			CBUFFER_START(UnityPerMaterial)
				float4 _MainTex_ST;
				float4 _EdgeColor;
				float _NoiseScale;
				float _EdgeWidth;
				float _DistortScale;
				float _FlowSpeed;
				float _ParticleCover;
				float _Intensity;
			CBUFFER_END
			struct Attributes { float4 positionOS : POSITION; float4 color : COLOR; float4 uv : TEXCOORD0; float4 look : TEXCOORD1; };
			struct Varyings { float4 positionCS : SV_POSITION; float4 color : COLOR; float4 uv : TEXCOORD0; float4 look : TEXCOORD1; };

			Varyings Vert(Attributes input)
			{
				Varyings output;
				output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
				output.color = input.color;
				output.uv = input.uv;
				output.look = input.look;
				return output;
			}

			float4 Frag(Varyings input) : SV_Target
			{
				float2 uv = input.uv.xy;
				float dissolve = input.uv.z;
				float seed = input.uv.w;
				// 소용돌이와 돌림: 가운데로 갈수록 더 감김 (텍스처 전체를 쓰는 쿼드만)
				if (input.look.y != 0.0 || input.look.z != 0.0)
				{
					float2 fromCenter = uv - 0.5;
					float radius = length(fromCenter);
					float turn = input.look.z + input.look.y * saturate(1.0 - radius * 2.0);
					float s, c;
					sincos(turn, s, c);
					uv = 0.5 + float2(fromCenter.x * c - fromCenter.y * s, fromCenter.x * s + fromCenter.y * c);
				}
				float2 flow = float2(-_Time.y * _FlowSpeed, seed * 7.13);
				float2 bend = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, uv * _DistortScale + flow).rg - 0.5;
				uv += bend * input.look.w;
				float4 painted = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
				float light = max(painted.r, max(painted.g, painted.b));

				// 침식. 밝은 심이 가장 늦게 깎임
				float2 noiseUv = uv * _NoiseScale + float2(seed * 3.7, seed * 1.9);
				float2 noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, noiseUv).rg;
				float keep = noise.r * 0.5 + noise.g * 0.2 + light * 0.3;
				float cut = dissolve * 1.12 - 0.06;
				float left = dissolve <= 0.0 ? 1.0 : smoothstep(cut - 0.10, cut + 0.12, keep);
				float edge = dissolve <= 0.0 ? 0.0 : left * (1.0 - smoothstep(cut, cut + _EdgeWidth, keep));

				float3 color = painted.rgb * input.color.rgb * _Intensity;
				color += _EdgeColor.rgb * edge * painted.a * input.color.rgb;
				float strength = left * input.color.a * saturate((1.0 - dissolve) * 3.0);
				float cover = max(input.look.x, _ParticleCover);
				return float4(color * strength, painted.a * strength * cover);
			}
			ENDHLSL
		}
	}
}
