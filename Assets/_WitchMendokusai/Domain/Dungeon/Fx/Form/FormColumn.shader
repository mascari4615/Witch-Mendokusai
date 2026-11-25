// 변신 연출의 빛기둥. 원통 겉면이 아니라 부피: 시선이 세로 기둥 속을 지나는 길이로 밝기를 구해 텍셀과 네 단으로 끊음
// - 상자 메시 안에서 그림 (뒷면만). 물체 원점이 기둥 바닥 가운데, 물체 크기는 지름과 높이
// - 깊이는 시선이 축에 가장 가까운 점으로 비교 (앞 물체에 가려짐). 바닥 아래는 없음, 위는 단으로 옅어짐
Shader "WM/Fx/FormColumn"
{
	Properties
	{
		[HDR] _CoreColor ("Core color", Color) = (2.0, 1.75, 1.15, 1)
		[HDR] _BodyColor ("Body color", Color) = (1.35, 0.78, 0.16, 1)
		[HDR] _TipColor ("Dark color", Color) = (0.75, 0.3, 0.06, 1)
		_Texel ("Texels per unit", Float) = 24
		_Alpha ("Alpha", Range(0, 1)) = 1
		_Flow ("Flow speed", Float) = 7
		_Steps ("Steps per second", Float) = 15
		_Cover ("Cover", Range(0, 1)) = 0
	}
	SubShader
	{
		Tags { "Queue"="Transparent-44" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
		Pass
		{
			Tags { "LightMode"="UniversalForward" }
			Blend One OneMinusSrcAlpha
			ZWrite Off
			ZTest Always
			Cull Front
			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment Frag
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
			CBUFFER_START(UnityPerMaterial)
				float4 _CoreColor;
				float4 _BodyColor;
				float4 _TipColor;
				float _Texel;
				float _Alpha;
				float _Flow;
				float _Steps;
				float _Cover;
			CBUFFER_END
			struct Attributes { float4 positionOS : POSITION; };
			struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float4 screen : TEXCOORD1; };
			float Hash2(float2 cell)
			{
				return frac(sin(dot(cell, float2(127.1, 311.7))) * 43758.5453);
			}
			float Noise2(float2 position)
			{
				float2 cell = floor(position);
				float2 fraction = frac(position);
				fraction = fraction * fraction * (3.0 - 2.0 * fraction);
				float low = lerp(Hash2(cell), Hash2(cell + float2(1, 0)), fraction.x);
				float high = lerp(Hash2(cell + float2(0, 1)), Hash2(cell + float2(1, 1)), fraction.x);
				return lerp(low, high, fraction.y);
			}
			Varyings Vert(Attributes input)
			{
				Varyings output;
				VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
				output.positionCS = position.positionCS;
				output.positionWS = position.positionWS;
				output.screen = ComputeScreenPos(position.positionCS);
				return output;
			}
			float4 Frag(Varyings input) : SV_Target
			{
				float3 axis = float3(UNITY_MATRIX_M[0][3], UNITY_MATRIX_M[1][3], UNITY_MATRIX_M[2][3]);
				float radius = 0.5 * length(float3(UNITY_MATRIX_M[0][0], UNITY_MATRIX_M[1][0], UNITY_MATRIX_M[2][0]));
				float heightMax = length(float3(UNITY_MATRIX_M[0][1], UNITY_MATRIX_M[1][1], UNITY_MATRIX_M[2][1]));
				float3 origin = GetCameraPositionWS();
				float3 direction = normalize(input.positionWS - origin);
				float2 toAxis = origin.xz - axis.xz;
				float flat = max(dot(direction.xz, direction.xz), 0.0001);
				float travel = -dot(toAxis, direction.xz) / flat;
				float3 nearest = origin + direction * travel;
				float lateral = dot(nearest.xz - axis.xz, normalize(float2(-direction.z, direction.x)));
				float height = nearest.y - axis.y;

				// 텍셀로 끊은 좌표
				lateral = (floor(lateral * _Texel) + 0.5) / _Texel;
				height = (floor(height * _Texel) + 0.5) / _Texel;
				clip(radius - abs(lateral));
				clip(height);
				clip(heightMax - height);

				float depth = LinearEyeDepth(SampleSceneDepth(input.screen.xy / input.screen.w), _ZBufferParams);
				float nearestDepth = -TransformWorldToView(nearest).z;
				clip(depth - nearestDepth);

				// 속을 지나는 길이 (0..1). 가늘고 긴 빛줄기 여러 가닥으로 나누고 가닥마다 길이와 밝기가 다름
				float through = sqrt(saturate(1.0 - (lateral * lateral) / (radius * radius)));
				float clock = floor(_Time.y * _Steps) / max(_Steps, 0.001);
				float strand = Noise2(float2(lateral / radius * 7.0 + 11.0, height * 0.12 + clock * _Flow * 0.35));
				float fine = Noise2(float2(lateral / radius * 17.0, height * 0.28 + clock * _Flow));
				float lifted = 1.0 - smoothstep(heightMax * 0.35, heightMax * 0.95, height);
				float shade = (through * 0.62 + (strand - 0.5) * 1.1 * (1.25 - through) + (fine - 0.5) * 0.45) * (0.45 + 0.55 * lifted);
				// 밑동은 가닥마다 다르게 끝남 (바닥에 자른 선이 안 보이게)
				clip(height - Noise2(float2(lateral / radius * 9.0, 3.7)) * 0.22 * (1.0 - through * 0.7));
				float band = floor(saturate(shade) * 4.0 + 0.35);
				clip(band - 0.5);
				float3 color = band < 1.5 ? _BodyColor.rgb : (band < 2.5 ? lerp(_BodyColor.rgb, _CoreColor.rgb, 0.45) : _CoreColor.rgb);
				float alpha = (band < 1.5 ? 0.22 : (band < 2.5 ? 0.7 : 1.25)) * _Alpha;
				return float4(color * alpha, alpha * _Cover);
			}
			ENDHLSL
		}
	}
}
