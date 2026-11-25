// 변신 연출의 단단한 물체 (막대 심, 완드, 가슴 알). 저폴리 면에 텍셀로 끊은 얼룩과 세 단 음영 (그림 기준 memo wm/design/art/style-canon.md)
// - 꼭짓점 rgb 는 바탕색, a 는 자체 발광. 텍셀 칸은 물체 공간 (_TexelDensity 칸 / 물체 길이 1)
// - 매끈한 반사와 테두리 빛 없음. 주광 방향으로 세 단, 면마다 한 번 더 갈림
// - 투명 줄 앞 큐에서 깊이를 써서 인물 그림과 앞뒤가 맞음
Shader "WM/Fx/FormSolid"
{
	Properties
	{
		_BaseColor ("Base", Color) = (1, 1, 1, 1)
		[HDR] _EmissionColor ("Emission", Color) = (0, 0, 0, 1)
		[HDR] _EdgeColor ("Dissolve edge", Color) = (2, 1.6, 1, 1)
		_TexelDensity ("Texels per unit", Vector) = (16, 48, 16, 0)
		_NoiseAmount ("Noise amount", Range(0, 1)) = 0.3
		_Dissolve ("Dissolve", Range(0, 1)) = 0
		_DissolveRange ("Dissolve height range", Vector) = (0, 1, 0, 0)
		_Heat ("Heat", Range(0, 1)) = 0
		_OutlineColor ("Outline", Color) = (0.16, 0.07, 0.04, 1)
		_Outline ("Outline width (object)", Float) = 0
	}
	SubShader
	{
		Tags { "Queue"="Transparent-60" "RenderType"="TransparentCutout" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
		Pass
		{
			Tags { "LightMode"="UniversalForward" }
			ZWrite On
			Cull Back
			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment Frag
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			CBUFFER_START(UnityPerMaterial)
				float4 _BaseColor;
				float4 _EmissionColor;
				float4 _EdgeColor;
				float4 _TexelDensity;
				float _NoiseAmount;
				float _Dissolve;
				float4 _DissolveRange;
				float _Heat;
				float4 _OutlineColor;
				float _Outline;
			CBUFFER_END
			struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 color : COLOR; };
			struct Varyings { float4 positionCS : SV_POSITION; float3 positionOS : TEXCOORD0; float3 normalWS : TEXCOORD1; float4 color : COLOR; };
			float Hash3(float3 cell)
			{
				return frac(sin(dot(cell, float3(127.1, 311.7, 74.7))) * 43758.5453);
			}
			Varyings Vert(Attributes input)
			{
				Varyings output;
				output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
				output.positionOS = input.positionOS.xyz;
				output.normalWS = TransformObjectToWorldNormal(input.normalOS);
				output.color = input.color;
				return output;
			}
			float4 Frag(Varyings input) : SV_Target
			{
				float3 texel = floor(input.positionOS * _TexelDensity.xyz + 0.001);
				float height = saturate((texel.y / _TexelDensity.y - _DissolveRange.x) / max(0.0001, _DissolveRange.y - _DissolveRange.x));
				// 위에서부터 텍셀 덩어리째 부서짐
				float value = (1.0 - height) * 0.7 + Hash3(floor(texel * 0.5)) * 0.3 - _Dissolve * 1.01;
				clip(_Dissolve <= 0.0 ? 1.0 : value);
				float burn = step(value, 0.06) * step(0.0001, _Dissolve);

				// 얼룩: 큰 얼룩 (3칸) 에 작은 칸 흔들림을 얹음
				float grain = (Hash3(floor(texel / 3.0)) * 0.65 + Hash3(texel) * 0.35 - 0.5) * _NoiseAmount;
				float3 normal = normalize(input.normalWS);
				float lit = dot(normal, normalize(GetMainLight().direction)) * 0.5 + 0.5;
				lit = saturate(lit + grain);
				float band = lit < 0.38 ? 0.5 : (lit < 0.66 ? 0.78 : 1.06);
				float3 color = _BaseColor.rgb * input.color.rgb * band;
				float glow = floor(saturate(input.color.a + grain * 0.6 * step(0.05, input.color.a)) * 4.0) / 4.0;
				color = lerp(color, _EmissionColor.rgb, glow);
				color = lerp(color, _EdgeColor.rgb, saturate(burn + _Heat));
				return float4(min(color, 30.0), 1.0);
			}
			ENDHLSL
		}
		// 짙은 윤곽: 면을 밖으로 밀어 뒷면만 그림 (밝은 날개 앞에서 완드가 갈리게)
		Pass
		{
			Tags { "LightMode"="SRPDefaultUnlit" }
			ZWrite On
			Cull Front
			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment Frag
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			CBUFFER_START(UnityPerMaterial)
				float4 _BaseColor;
				float4 _EmissionColor;
				float4 _EdgeColor;
				float4 _TexelDensity;
				float _NoiseAmount;
				float _Dissolve;
				float4 _DissolveRange;
				float _Heat;
				float4 _OutlineColor;
				float _Outline;
			CBUFFER_END
			struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
			struct Varyings { float4 positionCS : SV_POSITION; float3 positionOS : TEXCOORD0; };
			float Hash3(float3 cell)
			{
				return frac(sin(dot(cell, float3(127.1, 311.7, 74.7))) * 43758.5453);
			}
			Varyings Vert(Attributes input)
			{
				Varyings output;
				output.positionOS = input.positionOS.xyz;
				output.positionCS = TransformObjectToHClip(input.positionOS.xyz + input.normalOS * _Outline);
				return output;
			}
			float4 Frag(Varyings input) : SV_Target
			{
				clip(_Outline - 0.0001);
				float3 texel = floor(input.positionOS * _TexelDensity.xyz + 0.001);
				float height = saturate((texel.y / _TexelDensity.y - _DissolveRange.x) / max(0.0001, _DissolveRange.y - _DissolveRange.x));
				clip(_Dissolve <= 0.0 ? 1.0 : (1.0 - height) * 0.7 + Hash3(floor(texel * 0.5)) * 0.3 - _Dissolve * 1.01);
				return _OutlineColor;
			}
			ENDHLSL
		}
	}
}
