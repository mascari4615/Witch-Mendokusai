// 전체 메뉴 가운데 코어 모델. 빛 없이 그리는 미리보기 무대용
// - 면마다 평평한 명암 (화면 미분으로 면 법선), 단계 명암
// - 텍스처에서 보라가 강한 부분과 아주 밝은 부분을 빛나는 곳으로 보고 _Pulse 로 밝기를 오르내림
// - 가장자리 빛 (rim)
Shader "WM/UI/MenuCore"
{
	Properties
	{
		_MainTex ("Albedo", 2D) = "white" {}
		_Tint ("Tint", Color) = (1, 1, 1, 1)
		_LightDirection ("Light Direction (view space)", Vector) = (-0.45, 0.75, -0.5, 0)
		_Ambient ("Ambient", Range(0, 1)) = 0.5
		_ShadeSteps ("Shade Steps", Range(1, 8)) = 3
		_RimColor ("Rim Color", Color) = (0.72, 0.45, 1, 1)
		_RimPower ("Rim Power", Range(0.5, 8)) = 2.6
		_RimStrength ("Rim Strength", Range(0, 3)) = 0.7
		_GlowColor ("Glow Color", Color) = (0.74, 0.5, 1, 1)
		_GlowStrength ("Glow Strength", Range(0, 3)) = 0.55
		_GlowPulse ("Glow Pulse Amount", Range(0, 1)) = 0.6
		_Pulse ("Pulse (set by script)", Range(0, 1)) = 0.5
	}

	SubShader
	{
		Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

		Pass
		{
			Name "MenuCore"
			Tags { "LightMode" = "SRPDefaultUnlit" }

			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment Frag

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

			TEXTURE2D(_MainTex);
			SAMPLER(sampler_MainTex);

			CBUFFER_START(UnityPerMaterial)
				float4 _MainTex_ST;
				half4 _Tint;
				float4 _LightDirection;
				half _Ambient;
				half _ShadeSteps;
				half4 _RimColor;
				half _RimPower;
				half _RimStrength;
				half4 _GlowColor;
				half _GlowStrength;
				half _GlowPulse;
				half _Pulse;
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
				float3 positionVS : TEXCOORD1;
			};

			Varyings Vert(Attributes input)
			{
				Varyings output;
				float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
				output.positionVS = TransformWorldToView(positionWS);
				output.positionCS = TransformWViewToHClip(output.positionVS);
				output.uv = TRANSFORM_TEX(input.uv, _MainTex);
				return output;
			}

			half4 Frag(Varyings input) : SV_Target
			{
				half4 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * _Tint;

				// 면 법선 (보는 쪽 기준). 정점 법선을 쓰지 않아 면이 평평하게 보임
				float3 normalVS = normalize(cross(ddy(input.positionVS), ddx(input.positionVS)));
				if (normalVS.z < 0)
					normalVS = -normalVS;

				float3 lightVS = normalize(_LightDirection.xyz * float3(1, 1, -1));
				half lambert = saturate(dot(normalVS, lightVS));
				half stepped = floor(lambert * _ShadeSteps + 0.5) / _ShadeSteps;
				half shade = lerp(_Ambient, 1.0, stepped);

				// 보라가 강하거나 아주 밝은 텍셀
				half purple = saturate((albedo.b - albedo.g) * 2.2);
				half bright = saturate((min(albedo.r, min(albedo.g, albedo.b)) - 0.62) * 3.0);
				half glowMask = saturate(purple * 0.6 + bright);
				half pulse = lerp(1.0 - _GlowPulse, 1.0, _Pulse);

				float3 viewVS = normalize(-input.positionVS);
				half rim = pow(1.0 - saturate(dot(normalVS, viewVS)), _RimPower) * _RimStrength;

				half3 color = albedo.rgb * shade;
				color += _GlowColor.rgb * glowMask * _GlowStrength * pulse;
				color += _RimColor.rgb * rim;
				return half4(color, 1);
			}
			ENDHLSL
		}
	}
}
