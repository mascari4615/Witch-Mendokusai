// 충격파 굴절 링. 바닥에 눕힌 사각판 (UV 0~1, 중심 0.5) 위의 고리 모양 띠 안에서 화면을 중심 바깥쪽으로 밀어 그림
// - 화면 색은 _CameraOpaqueTexture (카메라의 Opaque Texture 를 스크립트가 켬). 불투명 물체만 담기므로 반투명 스프라이트는 굴절되지 않음
// - _Ring: 고리 반지름 (UV 중심에서 가장자리 0.5 기준 0~1 비율), _Width: 띠 폭 (같은 비율)
// - 굴절 방향은 화면에서 본 중심 (_ScreenCenter, 0~1 뷰포트) 에서 바깥쪽
// - 고리 앞쪽은 밝은 선, 뒤쪽은 어두운 골. 색 번짐 (색수차) 은 R, B 를 어긋나게 읽음
Shader "WM/Fx/UltimateShockwave"
{
	Properties
	{
		_Ring ("Ring", Range(0, 1)) = 0.5
		_Width ("Width", Range(0.005, 0.5)) = 0.12
		_Strength ("Distortion", Float) = 0.04
		_Chroma ("Chromatic", Float) = 0.4
		_Fade ("Fade", Range(0, 1)) = 1
		[HDR] _RimColor ("Rim Color", Color) = (1, 0.8, 0.5, 1)
		_RimStrength ("Rim Strength", Float) = 0.5
		_ScreenCenter ("Screen Center", Vector) = (0.5, 0.5, 0, 0)
		_Aspect ("Aspect", Float) = 1.7778
	}

	SubShader
	{
		Tags { "Queue" = "Transparent+10" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

		Pass
		{
			Name "UltimateShockwave"
			Tags { "LightMode" = "SRPDefaultUnlit" }
			Blend SrcAlpha OneMinusSrcAlpha
			ZWrite Off
			ZTest Always
			Cull Off

			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment Frag
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

			CBUFFER_START(UnityPerMaterial)
				float _Ring;
				float _Width;
				float _Strength;
				float _Chroma;
				float _Fade;
				float4 _RimColor;
				float _RimStrength;
				float4 _ScreenCenter;
				float _Aspect;
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
				float4 screenPos : TEXCOORD1;
			};

			Varyings Vert(Attributes input)
			{
				Varyings output;
				output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
				output.uv = input.uv;
				output.screenPos = ComputeScreenPos(output.positionCS);
				return output;
			}

			half4 Frag(Varyings input) : SV_Target
			{
				float2 local = (input.uv - 0.5) * 2.0;
				float dist = length(local);
				// 띠 안의 위치: -1 (안쪽) ~ 1 (바깥쪽)
				float t = (dist - _Ring) / max(_Width, 0.0001);
				float inBand = saturate(1.0 - abs(t));
				float band = inBand * inBand * (3.0 - 2.0 * inBand);
				// 앞쪽 (바깥) 은 밀고 뒤쪽 (안) 은 당기는 부호가 있는 프로필
				float profile = band * sign(t) * -1.0;
				profile = lerp(band * 0.35, profile, 0.65);

				float2 uv = input.screenPos.xy / input.screenPos.w;
				float2 toPixel = uv - _ScreenCenter.xy;
				toPixel.x *= _Aspect;
				float2 dir = normalize(toPixel + 0.00001);
				dir.x /= _Aspect;
				float2 offset = dir * profile * _Strength * _Fade;

				half3 colorG = SampleSceneColor(uv + offset);
				half3 colorR = SampleSceneColor(uv + offset * (1.0 + _Chroma));
				half3 colorB = SampleSceneColor(uv + offset * (1.0 - _Chroma));
				half3 color = half3(colorR.r, colorG.g, colorB.b);

				float rim = pow(band, 3.0) * saturate(t * 0.5 + 0.6);
				color += _RimColor.rgb * rim * _RimStrength * _Fade;
				float alpha = saturate(band * 1.4) * _Fade;
				return half4(color, alpha);
			}
			ENDHLSL
		}
	}
}
