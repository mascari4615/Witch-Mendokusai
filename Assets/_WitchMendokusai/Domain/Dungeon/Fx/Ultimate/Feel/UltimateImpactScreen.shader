// 궁극기 임팩트 화면 왜곡. 렌더러 기능 (UltimateImpactScreenFeature) 이 후처리 뒤 카메라 색 전체에 한 번 그림
// - 굴절 고리: 표적 화면 위치 (_WMImpactCenter) 에서 퍼지는 고리 띠 안의 화소를 바깥으로 밀어 읽음
// - 방사 흐림: 표적 쪽으로 여러 번 읽어 평균. 가운데는 또렷, 바깥일수록 길게
// - 임팩트 프레임: 1 흑백 (대비 올림), 2 반전, 3 실루엣 (밝은 곳만 흰색, 나머지 거의 검정)
// - 값은 전역 셰이더 값 (UltimateImpactScreen 이 매 프레임 넣음)
Shader "WM/Fx/UltimateImpactScreen"
{
	SubShader
	{
		Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
		ZWrite Off
		ZTest Always
		Cull Off
		Blend Off

		Pass
		{
			Name "UltimateImpactScreen"

			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment Frag
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

			float4 _WMImpactCenter;   // xy 뷰포트, z 화면 가로세로비
			float4 _WMImpactRadial;   // x 세기, y 표본 거리 상한
			float4 _WMImpactRipple;   // x 반지름 (화면 높이), y 세기, z 띠 폭
			float4 _WMImpactFrame;    // x 종류, y 세기

			#define RADIAL_SAMPLES 12

			half3 SampleSource(float2 uv)
			{
				return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, saturate(uv)).rgb;
			}

			half4 Frag(Varyings input) : SV_Target
			{
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
				float2 uv = input.texcoord;
				float2 center = _WMImpactCenter.xy;
				float aspect = max(0.1, _WMImpactCenter.z);

				float2 toPixel = uv - center;
				toPixel.x *= aspect;
				float radius = length(toPixel);
				float2 direction = toPixel / max(radius, 0.00001);
				direction.x /= aspect;

				// 굴절 고리
				float bandPosition = (radius - _WMImpactRipple.x) / max(_WMImpactRipple.z, 0.0001);
				float inBand = saturate(1.0 - abs(bandPosition));
				inBand = inBand * inBand * (3.0 - 2.0 * inBand);
				uv += direction * inBand * _WMImpactRipple.y;

				// 방사 흐림
				half3 color = 0;
				float reach = saturate(_WMImpactRadial.x) * _WMImpactRadial.y * saturate(radius * 1.6);
				[unroll]
				for (int i = 0; i < RADIAL_SAMPLES; i++)
				{
					float along = i / (float)(RADIAL_SAMPLES - 1);
					color += SampleSource(uv - (uv - center) * along * reach);
				}
				color /= RADIAL_SAMPLES;

				// 임팩트 프레임
				float kind = _WMImpactFrame.x;
				float strength = saturate(_WMImpactFrame.y);
				if (kind > 0.5 && strength > 0.0)
				{
					float luminance = dot(color, half3(0.2126, 0.7152, 0.0722));
					half3 frame;
					if (kind < 1.5)
						frame = saturate((luminance - 0.45) * 1.9 + 0.45).xxx;
					else if (kind < 2.5)
						frame = saturate(1.0 - color);
					else
						frame = lerp(half3(0.03, 0.02, 0.05), half3(1.0, 0.98, 0.94), smoothstep(0.5, 0.62, luminance));
					color = lerp(color, frame, strength);
				}
				return half4(color, 1.0);
			}
			ENDHLSL
		}
	}
}
