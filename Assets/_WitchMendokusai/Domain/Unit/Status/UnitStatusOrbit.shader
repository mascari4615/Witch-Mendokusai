Shader "WM/UnitStatusOrbit"
{
	// 상태이상 머리 위 3D 궤도 (기절 별과 고리). 불투명, 색은 _Color, 면 방향으로 밝기 차이
	Properties
	{
		_Color ("Color", Color) = (1, 1, 1, 1)
		_ShadeDirection ("Shade direction (world)", Vector) = (0.3, 0.9, -0.4, 0)
		_ShadeMin ("Shade min", Range(0, 1)) = 0.55
		_RimColor ("Rim color", Color) = (1, 1, 1, 1)
		_RimPower ("Rim power", Range(0.5, 8)) = 3
		_RimStrength ("Rim strength", Range(0, 1)) = 0.35
	}
	SubShader
	{
		Tags { "Queue"="Geometry" "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
		Pass
		{
			Tags { "LightMode"="SRPDefaultUnlit" }
			ZWrite On
			Cull Back
			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment Frag
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			CBUFFER_START(UnityPerMaterial)
				float4 _Color;
				float4 _ShadeDirection;
				float _ShadeMin;
				float4 _RimColor;
				float _RimPower;
				float _RimStrength;
			CBUFFER_END
			struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
			struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 viewWS : TEXCOORD1; };
			Varyings Vert(Attributes input)
			{
				Varyings output;
				float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
				output.positionCS = TransformWorldToHClip(positionWS);
				output.normalWS = TransformObjectToWorldNormal(input.normalOS);
				output.viewWS = GetWorldSpaceViewDir(positionWS);
				return output;
			}
			float4 Frag(Varyings input) : SV_Target
			{
				float3 normal = normalize(input.normalWS);
				float shade = lerp(_ShadeMin, 1.0, saturate(dot(normal, normalize(_ShadeDirection.xyz)) * 0.5 + 0.5));
				float rim = pow(1.0 - saturate(dot(normal, normalize(input.viewWS))), _RimPower) * _RimStrength;
				float3 color = _Color.rgb * shade + _RimColor.rgb * rim;
				return float4(color, 1.0);
			}
			ENDHLSL
		}
	}
}
