// 궁극기 3D 연출의 빛 기둥 (원기둥 메시). 하늘에서 지면까지 잇는 체적 느낌
// - 가산 합성, 깊이 쓰기 없음. 정면 (법선이 카메라 쪽) 일수록 밝고 테두리로 옅어짐. 가운데는 흰 심
// - 물체 공간 각도와 높이로 3D 잡음 줄무늬가 아래로 흐름
// - 높이 양 끝 (원기둥 뚜껑) 은 투명. 아래는 바닥과 맞닿는 곳을 깊이 텍스처로 녹임
// - 출력 상한 40: 반정밀도 넘침이 빛 번짐에 번지는 것 방지 (2026-11-20 첫 촬영 사건)
Shader "WM/Fx/UltimateBeamVolume"
{
	Properties
	{
		[HDR] _Color ("Color", Color) = (3, 2.1, 0.7, 1)
		[HDR] _CoreColor ("Core Color", Color) = (4, 3.6, 2.6, 1)
		_Intensity ("Intensity", Float) = 1
		_EdgePower ("Edge Power", Range(0.5, 8)) = 1.6
		_CorePower ("Core Power", Range(1, 16)) = 6
		_StreakScale ("Streak Scale (angle, height)", Vector) = (5, 3, 0, 0)
		_StreakSpeed ("Streak Speed", Float) = 6
		_StreakAmount ("Streak Amount", Range(0, 1)) = 0.5
		_SoftDistance ("Soft Distance", Float) = 0.5
	}

	SubShader
	{
		Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

		Pass
		{
			Name "ForwardUnlit"
			Tags { "LightMode" = "UniversalForward" }
			Blend One One
			ZWrite Off
			Cull Back

			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment Frag
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

			CBUFFER_START(UnityPerMaterial)
				float4 _Color;
				float4 _CoreColor;
				float _Intensity;
				float _EdgePower;
				float _CorePower;
				float4 _StreakScale;
				float _StreakSpeed;
				float _StreakAmount;
				float _SoftDistance;
			CBUFFER_END

			struct Attributes
			{
				float4 positionOS : POSITION;
				float3 normalOS : NORMAL;
			};

			struct Varyings
			{
				float4 positionCS : SV_POSITION;
				float3 positionOS : TEXCOORD0;
				float3 normalWS : TEXCOORD1;
				float3 positionWS : TEXCOORD2;
				float4 screenPosition : TEXCOORD3;
			};

			float Hash3(float3 cell)
			{
				cell = frac(cell * 0.3183099 + 0.1);
				cell *= 17.0;
				return frac(cell.x * cell.y * cell.z * (cell.x + cell.y + cell.z));
			}

			float ValueNoise(float3 position)
			{
				float3 cell = floor(position);
				float3 fraction = frac(position);
				fraction = fraction * fraction * (3.0 - 2.0 * fraction);
				float bottom = lerp(lerp(Hash3(cell), Hash3(cell + float3(1, 0, 0)), fraction.x), lerp(Hash3(cell + float3(0, 1, 0)), Hash3(cell + float3(1, 1, 0)), fraction.x), fraction.y);
				float top = lerp(lerp(Hash3(cell + float3(0, 0, 1)), Hash3(cell + float3(1, 0, 1)), fraction.x), lerp(Hash3(cell + float3(0, 1, 1)), Hash3(cell + float3(1, 1, 1)), fraction.x), fraction.y);
				return lerp(bottom, top, fraction.z);
			}

			Varyings Vert(Attributes input)
			{
				Varyings output;
				VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
				output.positionCS = position.positionCS;
				output.positionWS = position.positionWS;
				output.positionOS = input.positionOS.xyz;
				output.normalWS = TransformObjectToWorldNormal(input.normalOS);
				output.screenPosition = ComputeScreenPos(position.positionCS);
				return output;
			}

			float4 Frag(Varyings input) : SV_Target
			{
				float3 viewDirection = normalize(GetWorldSpaceViewDir(input.positionWS));
				float3 normal = normalize(input.normalWS);
				// 원기둥 옆면만. 뚜껑 (법선이 위아래) 은 버림
				float side = 1.0 - abs(normal.y);
				float facing = saturate(dot(normal, viewDirection));
				float body = pow(facing, _EdgePower) * side;
				float core = pow(facing, _CorePower) * side;

				float angle = atan2(input.positionOS.z, input.positionOS.x);
				float height = input.positionOS.y;
				float noise = ValueNoise(float3(angle * _StreakScale.x, height * _StreakScale.y + _Time.y * _StreakSpeed, 0.5));
				float streak = lerp(1.0, 0.4 + noise * 1.2, _StreakAmount);

				// 아래 끝 짧게, 위 끝은 길게 옅어짐 (화면 위로 넘어가 끝이 안 보이게)
				float vertical = smoothstep(-1.0, -0.97, height) * smoothstep(1.0, 0.3, height);
				float3 color = lerp(_Color.rgb * streak, _CoreColor.rgb, saturate(core)) * body * vertical * _Intensity;

				if (_SoftDistance > 0.0)
				{
					float2 uv = input.screenPosition.xy / input.screenPosition.w;
					float sceneDepth = LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
					float beamDepth = LinearEyeDepth(input.positionCS.z, _ZBufferParams);
					color *= saturate((sceneDepth - beamDepth) / _SoftDistance);
				}
				return float4(min(color, 40.0), 1.0);
			}
			ENDHLSL
		}
	}
}
