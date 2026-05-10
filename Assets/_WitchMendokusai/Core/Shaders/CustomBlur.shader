Shader "WM/CustomBlur"
{
	HLSLINCLUDE
		#pragma editor_sync_compilation
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
		#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
		#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderVariablesFunctions.hlsl"

		SAMPLER(sampler_BlitTexture);

		#define SAMPLE_BASEMAP(uv) half4(SAMPLE_TEXTURE2D_LOD(_BlitTexture, sampler_LinearClamp, UnityStereoTransformScreenSpaceTex(uv), _BlitMipLevel))

		// Blur params (set by RenderPass via MaterialPropertyBlock)
		half4 _BlurParams;  // x=intensity, y=iteration scale, z=unused, w=unused

		#define INTENSITY _BlurParams.x
		#define ITERATION_SCALE _BlurParams.y

		// Pass 0 — Kawase (Bunkasha Games GDC 2003): 4 corner sample + average.
		half4 KawaseBlurFilter(half2 texCoord, half2 pixelSize, half iteration)
		{
			half2 halfPixelSize = pixelSize * half(0.5);
			half2 dUV = (pixelSize * half2(iteration, iteration)) + halfPixelSize;

			half4 cOut;
			cOut  = SAMPLE_BASEMAP(half2(texCoord.x - dUV.x, texCoord.y + dUV.y));
			cOut += SAMPLE_BASEMAP(half2(texCoord.x + dUV.x, texCoord.y + dUV.y));
			cOut += SAMPLE_BASEMAP(half2(texCoord.x + dUV.x, texCoord.y - dUV.y));
			cOut += SAMPLE_BASEMAP(half2(texCoord.x - dUV.x, texCoord.y - dUV.y));
			cOut *= half(0.25);
			return cOut;
		}

		half4 FragKawase(Varyings input) : SV_Target
		{
			UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
			half2 uv = input.texcoord;
			half2 texelSize = _BlitTexture_TexelSize.xy;
			return KawaseBlurFilter(uv, texelSize * INTENSITY, ITERATION_SCALE);
		}

		// Pass 1 — Dual Kawase DownSample (Marius Bjorge, ARM GDC 2015)
		// 중심 1 sample (weight×4) + 4 offset sample (weight×1) / 8
		half4 FragDualKawaseDown(Varyings input) : SV_Target
		{
			UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
			half2 uv = input.texcoord;
			half2 ts = _BlitTexture_TexelSize.xy * INTENSITY;

			half4 sum = SAMPLE_BASEMAP(uv) * 4.0h;
			sum += SAMPLE_BASEMAP(uv + half2(-ts.x,  ts.y));
			sum += SAMPLE_BASEMAP(uv + half2( ts.x,  ts.y));
			sum += SAMPLE_BASEMAP(uv + half2( ts.x, -ts.y));
			sum += SAMPLE_BASEMAP(uv + half2(-ts.x, -ts.y));
			return sum * (1.0h / 8.0h);
		}

		// Pass 2 — Dual Kawase UpSample
		// 8방향 샘플, 대각=weight×2 / 12
		half4 FragDualKawaseUp(Varyings input) : SV_Target
		{
			UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
			half2 uv = input.texcoord;
			half2 ts = _BlitTexture_TexelSize.xy * INTENSITY;

			half4 sum = half4(0, 0, 0, 0);
			// cardinal (weight 1)
			sum += SAMPLE_BASEMAP(uv + half2(-2.0h * ts.x,  0));
			sum += SAMPLE_BASEMAP(uv + half2( 0,             2.0h * ts.y));
			sum += SAMPLE_BASEMAP(uv + half2( 2.0h * ts.x,  0));
			sum += SAMPLE_BASEMAP(uv + half2( 0,            -2.0h * ts.y));
			// diagonal (weight 2)
			sum += SAMPLE_BASEMAP(uv + half2(-ts.x,  ts.y)) * 2.0h;
			sum += SAMPLE_BASEMAP(uv + half2( ts.x,  ts.y)) * 2.0h;
			sum += SAMPLE_BASEMAP(uv + half2( ts.x, -ts.y)) * 2.0h;
			sum += SAMPLE_BASEMAP(uv + half2(-ts.x, -ts.y)) * 2.0h;
			return sum * (1.0h / 12.0h);
		}
	ENDHLSL

	SubShader
	{
		Tags
		{
			"RenderType" = "Opaque"
			"RenderPipeline" = "UniversalPipeline"
		}
		Cull Off
		ZWrite Off
		ZTest Always

		// Pass 0 — Kawase (legacy / fallback)
		Pass
		{
			Name "WM Kawase Blur"
			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment FragKawase
			ENDHLSL
		}

		// Pass 1 — Dual Kawase Down
		Pass
		{
			Name "WM DualKawase Down"
			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment FragDualKawaseDown
			ENDHLSL
		}

		// Pass 2 — Dual Kawase Up
		Pass
		{
			Name "WM DualKawase Up"
			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment FragDualKawaseUp
			ENDHLSL
		}
	}
}
