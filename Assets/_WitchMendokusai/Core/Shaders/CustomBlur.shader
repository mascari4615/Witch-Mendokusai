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

		// Kawase blur params (set by RenderPass via MaterialPropertyBlock)
		half4 _BlurParams;  // x=intensity, y=iteration scale, z=unused, w=unused

		#define INTENSITY _BlurParams.x
		#define ITERATION_SCALE _BlurParams.y

		// Kawase blur (Bunkasha Games GDC 2003 — DOUBLE-S.T.E.A.L.) — 4 corner sample + average.
		half4 KawaseBlurFilter(half2 texCoord, half2 pixelSize, half iteration)
		{
			half2 halfPixelSize = pixelSize * half(0.5);
			half2 dUV = (pixelSize * half2(iteration, iteration)) + halfPixelSize;

			half4 cOut;

			// top-left
			cOut  = SAMPLE_BASEMAP(half2(texCoord.x - dUV.x, texCoord.y + dUV.y));
			// top-right
			cOut += SAMPLE_BASEMAP(half2(texCoord.x + dUV.x, texCoord.y + dUV.y));
			// bottom-right
			cOut += SAMPLE_BASEMAP(half2(texCoord.x + dUV.x, texCoord.y - dUV.y));
			// bottom-left
			cOut += SAMPLE_BASEMAP(half2(texCoord.x - dUV.x, texCoord.y - dUV.y));

			cOut *= half(0.25);

			return cOut;
		}

		half4 FragKawase(Varyings input) : SV_Target
		{
			UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

			half2 uv = input.texcoord;
			half2 texelSize = _BlitTexture_TexelSize.xy;

			half4 col = KawaseBlurFilter(uv, texelSize * INTENSITY, ITERATION_SCALE);

			return col;
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

		Pass
		{
			Name "WM Kawase Blur"

			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment FragKawase
			ENDHLSL
		}
	}
}
