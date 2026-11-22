// 풀. URP Unlit 과 같은 겉모습 (기본 그림 x 기본 색) 에 주인공과 몬스터 쪽에서 눕기, 싸운 자리 눌림을 더함
// 전역 값은 GrassInteraction 이 씀. 값이 없으면 (던전 밖) 그냥 서 있는 풀
Shader "WM/Remains/InteractiveGrass"
{
	Properties
	{
		[MainTexture] _BaseMap ("Texture", 2D) = "white" {}
		[MainColor] _BaseColor ("Color", Color) = (1, 1, 1, 1)
		_Cutoff ("Alpha cutoff", Range(0, 1)) = 0.5
		[Toggle(_ALPHATEST_ON)] _AlphaClip ("Alpha clip", Float) = 0
		_BendWeight ("Bend weight", Range(0, 2)) = 1
	}
	SubShader
	{
		Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry" }

		HLSLINCLUDE
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
		#define MAX_BENDERS 16

		TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
		TEXTURE2D(_WMGrassFlatten); SAMPLER(sampler_WMGrassFlatten);
		CBUFFER_START(UnityPerMaterial)
			float4 _BaseMap_ST;
			half4 _BaseColor;
			half _Cutoff;
			half _BendWeight;
		CBUFFER_END
		float4 _WMGrassBenders[MAX_BENDERS];
		float _WMGrassBenderCount;
		float4 _WMGrassFlattenRect;
		// x 눕는 세기, y 풀 높이, z 눌림 세기, w 바닥 높이
		float4 _WMGrassParams;

		// 높을수록 많이 움직임. 뿌리는 그대로. 눌린 자리는 키가 줄어듦
		float3 Bend(float3 positionWS)
		{
			float heightAbove = max(positionWS.y - _WMGrassParams.w, 0.0);
			float heightRatio = saturate(heightAbove / max(_WMGrassParams.y, 0.01));
			float lean = heightRatio * heightRatio * _BendWeight;
			float2 sideways = 0;
			float down = 0;
			int count = (int)_WMGrassBenderCount;
			for (int i = 0; i < MAX_BENDERS; i++)
			{
				if (i >= count)
					break;
				float4 bender = _WMGrassBenders[i];
				float2 away = positionWS.xz - bender.xz;
				float distance = length(away);
				float push = saturate(1.0 - distance / max(bender.w, 0.01));
				sideways += (distance > 0.001 ? away / distance : float2(0, 0)) * push;
				down += push;
			}
			float2 uv = (positionWS.xz - _WMGrassFlattenRect.xy) / max(_WMGrassFlattenRect.zw, 0.01);
			float flattenAmount = 0;
			if (all(uv > 0.0) && all(uv < 1.0))
				flattenAmount = saturate(SAMPLE_TEXTURE2D_LOD(_WMGrassFlatten, sampler_WMGrassFlatten, uv, 0).a);
			float3 offset;
			offset.xz = sideways * _WMGrassParams.x * lean;
			// 옆으로 누운 만큼 키도 낮아짐 (길이 보존 흉내)
			offset.y = -saturate(down) * _WMGrassParams.x * lean * 0.6 - heightAbove * flattenAmount * _WMGrassParams.z;
			return positionWS + offset;
		}
		ENDHLSL

		Pass
		{
			Name "Unlit"
			Tags { "LightMode" = "UniversalForward" }
			Cull Off
			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment Frag
			#pragma multi_compile_instancing
			#pragma multi_compile_fog
			#pragma shader_feature_local _ALPHATEST_ON

			struct Attributes
			{
				float4 positionOS : POSITION;
				float2 uv : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct Varyings
			{
				float4 positionCS : SV_POSITION;
				float2 uv : TEXCOORD0;
				float fogCoord : TEXCOORD1;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			Varyings Vert(Attributes input)
			{
				Varyings output = (Varyings)0;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				float3 positionWS = Bend(TransformObjectToWorld(input.positionOS.xyz));
				output.positionCS = TransformWorldToHClip(positionWS);
				output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
				output.fogCoord = ComputeFogFactor(output.positionCS.z);
				return output;
			}

			half4 Frag(Varyings input) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID(input);
				half4 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
				#ifdef _ALPHATEST_ON
				clip(color.a - _Cutoff);
				#endif
				color.rgb = MixFog(color.rgb, input.fogCoord);
				return half4(color.rgb, 1);
			}
			ENDHLSL
		}

		Pass
		{
			Name "DepthOnly"
			Tags { "LightMode" = "DepthOnly" }
			ZWrite On
			ColorMask R
			Cull Off
			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment Frag
			#pragma multi_compile_instancing
			#pragma shader_feature_local _ALPHATEST_ON

			struct Attributes
			{
				float4 positionOS : POSITION;
				float2 uv : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct Varyings
			{
				float4 positionCS : SV_POSITION;
				float2 uv : TEXCOORD0;
			};

			Varyings Vert(Attributes input)
			{
				Varyings output;
				UNITY_SETUP_INSTANCE_ID(input);
				output.positionCS = TransformWorldToHClip(Bend(TransformObjectToWorld(input.positionOS.xyz)));
				output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
				return output;
			}

			half Frag(Varyings input) : SV_Target
			{
				#ifdef _ALPHATEST_ON
				clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a * _BaseColor.a - _Cutoff);
				#endif
				return 0;
			}
			ENDHLSL
		}
	}
}
