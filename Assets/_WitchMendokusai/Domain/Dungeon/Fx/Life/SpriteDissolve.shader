Shader "WM/SpriteDissolve"
{
	// 사망 잔상용. 스프라이트를 노이즈로 녹이고 녹는 가장자리에 밝은 띠
	// 값은 전부 MonsterLifeFxDriver 가 렌더러별 속성 블록으로 넣음. 재질의 값은 기본값일 뿐
	Properties
	{
		_MainTex ("Sprite", 2D) = "white" {}
		_Tint ("Tint", Color) = (1, 1, 1, 1)
		_Flip ("Flip (x, y)", Vector) = (1, 1, 0, 0)
		_Deform ("Width, Height, Rise", Vector) = (1, 1, 0, 0)
		_Dissolve ("Progress, Edge width, Noise scale, Vertical bias", Vector) = (0, 0.1, 6, 0)
		[HDR] _EdgeColor ("Edge color (a: strength)", Color) = (1, 1, 1, 1)
		_FlashColor ("Flash color (a: amount)", Color) = (1, 1, 1, 0)
		_BoundsY ("Bottom, Height, Seed", Vector) = (0, 1, 0, 0)
		_BillboardMode ("Billboard Mode", Range(0, 2)) = 0
	}
	SubShader
	{
		Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" "DisableBatching"="True" }
		Pass
		{
			Tags { "LightMode"="SRPDefaultUnlit" }
			Blend SrcAlpha OneMinusSrcAlpha
			ZWrite Off
			Cull Off
			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment Frag
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

			TEXTURE2D(_MainTex);
			SAMPLER(sampler_MainTex);

			CBUFFER_START(UnityPerMaterial)
				float4 _MainTex_ST;
				float4 _Tint;
				float4 _Flip;
				float4 _Deform;
				float4 _Dissolve;
				float4 _EdgeColor;
				float4 _FlashColor;
				float4 _BoundsY;
				float _BillboardMode;
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
				float2 shapePosition : TEXCOORD1;
			};

			float Hash21(float2 cell)
			{
				float2 folded = frac(cell * float2(123.34, 456.21));
				folded += dot(folded, folded + 45.32);
				return frac(folded.x * folded.y);
			}

			float ValueNoise(float2 position)
			{
				float2 cell = floor(position);
				float2 local = frac(position);
				float2 blend = local * local * (3.0 - 2.0 * local);
				float bottomLeft = Hash21(cell);
				float bottomRight = Hash21(cell + float2(1.0, 0.0));
				float topLeft = Hash21(cell + float2(0.0, 1.0));
				float topRight = Hash21(cell + float2(1.0, 1.0));
				return lerp(lerp(bottomLeft, bottomRight, blend.x), lerp(topLeft, topRight, blend.x), blend.y);
			}

			Varyings Vert(Attributes input)
			{
				Varyings output;

				// 무늬와 높이는 변형 전 자리 기준. 찌그러져도 무늬가 몸에 붙어 있음
				output.shapePosition = input.positionOS.xy;

				// 스프라이트 원점 (발밑) 기준으로 뒤집기와 찌그러짐
				float3 local = input.positionOS.xyz;
				local.x *= _Flip.x * _Deform.x;
				local.y *= _Flip.y * _Deform.y;

				float3 axisX = float3(UNITY_MATRIX_M._m00, UNITY_MATRIX_M._m10, UNITY_MATRIX_M._m20);
				float3 axisY = float3(UNITY_MATRIX_M._m01, UNITY_MATRIX_M._m11, UNITY_MATRIX_M._m21);
				float3 axisZ = float3(UNITY_MATRIX_M._m02, UNITY_MATRIX_M._m12, UNITY_MATRIX_M._m22);
				float3 scale = max(float3(length(axisX), length(axisY), length(axisZ)), 0.00001);
				float3 pivotWS = float3(UNITY_MATRIX_M._m03, UNITY_MATRIX_M._m13, UNITY_MATRIX_M._m23);

				// 본체 셰이더 (BillboardShader) 와 같은 세 방식. 0 트랜스폼 회전, 1 카메라 위치 기준, 2 카메라 회전 기준
				float3 right = axisX / scale.x;
				float3 up = axisY / scale.y;
				float3 forward = axisZ / scale.z;
				if (_BillboardMode > 1.5)
				{
					right = UNITY_MATRIX_V[0].xyz;
					up = UNITY_MATRIX_V[1].xyz;
					forward = UNITY_MATRIX_V[2].xyz;
				}
				else if (_BillboardMode > 0.5)
				{
					forward = normalize(pivotWS - _WorldSpaceCameraPos);
					right = normalize(cross(float3(0.0, 1.0, 0.0), forward));
					up = cross(forward, right);
				}

				float3 scaled = local * scale;
				float3 positionWS = pivotWS + right * scaled.x + up * scaled.y + forward * scaled.z;
				positionWS.y += _Deform.z;

				output.positionCS = TransformWorldToHClip(positionWS);
				output.uv = input.uv;
				return output;
			}

			float4 Frag(Varyings input) : SV_Target
			{
				float4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * _Tint;

				float2 noisePosition = input.shapePosition * _Dissolve.z + _BoundsY.z;
				float noise = ValueNoise(noisePosition) * 0.65 + ValueNoise(noisePosition * 2.03 + 17.0) * 0.35;

				// 세로 치우침. 양수면 위부터, 음수면 아래부터 녹음
				float height01 = saturate((input.shapePosition.y - _BoundsY.x) / max(_BoundsY.y, 0.0001));
				float directional = _Dissolve.w > 0.0 ? 1.0 - height01 : height01;
				float field = lerp(noise, directional * 0.8 + noise * 0.2, saturate(abs(_Dissolve.w)));

				// remain 이 0 아래면 녹은 자리, 0 ~ edgeWidth 가 밝은 띠
				float edgeWidth = max(_Dissolve.y, 0.0001);
				float remain = field - _Dissolve.x * (1.0 + edgeWidth) + edgeWidth;
				float coverage = saturate(remain / max(fwidth(remain), 0.0001));
				float edge = 1.0 - smoothstep(0.0, edgeWidth, remain);

				color.rgb = lerp(color.rgb, _FlashColor.rgb, saturate(_FlashColor.a));
				color.rgb = lerp(color.rgb, _EdgeColor.rgb, edge * saturate(_EdgeColor.a));
				color.a *= coverage;
				return color;
			}
			ENDHLSL
		}
	}
}
