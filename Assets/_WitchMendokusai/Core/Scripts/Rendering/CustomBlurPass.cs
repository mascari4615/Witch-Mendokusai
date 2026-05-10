using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace WitchMendokusai
{
	// Kawase blur 멀티 iteration ping-pong. RenderGraph (Unity 6) API.
	// 결과 = CustomBlurFeature.GlobalBlurTextureId 에 SetGlobal.
	internal class CustomBlurPass : ScriptableRenderPass, IDisposable
	{
		private const string PASS_NAME = "WM Custom Blur";
		private const string SOURCE_RT_NAME = PASS_NAME + " - Source";
		private const string DEST_RT_NAME = PASS_NAME + " - Destination";

		private readonly CustomBlurFeature feature;
		private readonly ProfilingSampler profilingSampler;
		private readonly MaterialPropertyBlock propertyBlock;

		private RTHandle externalRTHandle;
		private RenderTexture lastExternalRT;

		public CustomBlurPass(CustomBlurFeature feature)
		{
			this.feature = feature;
			profilingSampler = new ProfilingSampler(PASS_NAME);
			propertyBlock = new MaterialPropertyBlock();
		}

		public void Dispose()
		{
			externalRTHandle?.Release();
			externalRTHandle = null;
			lastExternalRT = null;
		}

		private class PassData
		{
			public TextureHandle ColorSource;
			public TextureHandle Source;
			public TextureHandle Destination;
			public RTHandle ExternalRT;
			public Material BlurMaterial;
			public MaterialPropertyBlock PropertyBlock;
			public int Iterations;
			public float Intensity;
			public float Offset;
		}

		public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
		{
			UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

			if (resourceData.isActiveTargetBackBuffer == true)
			{
				Debug.LogError($"[{nameof(CustomBlurPass)}] BackBuffer 를 input texture 로 사용 불가 — intermediate ColorTexture 필수.");
				return;
			}

			TextureHandle cameraColorSource = resourceData.activeColorTexture;

			UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
			RenderTextureDescriptor cameraDescriptor = cameraData.cameraTargetDescriptor;
			int width = Mathf.Max(1, Mathf.RoundToInt(cameraDescriptor.width / feature.Downsample));
			int height = Mathf.Max(1, Mathf.RoundToInt(cameraDescriptor.height / feature.Downsample));

			RenderTextureDescriptor blurDescriptor = new RenderTextureDescriptor(width, height, GraphicsFormat.B10G11R11_UFloatPack32, 0);

			TextureDesc sourceDesc = new TextureDesc(blurDescriptor) { name = SOURCE_RT_NAME };
			TextureDesc destDesc = new TextureDesc(blurDescriptor) { name = DEST_RT_NAME };

			TextureHandle source = renderGraph.CreateTexture(sourceDesc);

			// destination 자체를 external RT 로 (있을 때) — RG 가 처음부터 인식. ping-pong 마지막 결과가 직접 external 에 박힘.
			RenderTexture currentExternalRT = feature.TargetRT;
			TextureHandle destination;
			bool useExternal = (currentExternalRT != null);
			if (useExternal == true)
			{
				if (externalRTHandle == null || lastExternalRT != currentExternalRT)
				{
					externalRTHandle?.Release();
					externalRTHandle = RTHandles.Alloc(currentExternalRT);
					lastExternalRT = currentExternalRT;
				}
				destination = renderGraph.ImportTexture(externalRTHandle);
			}
			else
			{
				destination = renderGraph.CreateTexture(destDesc);
			}

			using (IUnsafeRenderGraphBuilder builder = renderGraph.AddUnsafePass<PassData>(PASS_NAME, out PassData passData, profilingSampler))
			{
				passData.ColorSource = cameraColorSource;
				passData.Source = source;
				passData.Destination = destination;
				passData.BlurMaterial = feature.BlurMaterial;
				passData.PropertyBlock = propertyBlock;
				passData.Iterations = feature.Iterations;
				passData.Intensity = feature.Intensity;
				passData.Offset = feature.Offset;

				builder.AllowPassCulling(false);

				builder.UseTexture(passData.ColorSource, AccessFlags.Read);
				builder.UseTexture(source, AccessFlags.ReadWrite);
				builder.UseTexture(destination, AccessFlags.ReadWrite);

				builder.SetGlobalTextureAfterPass(destination, CustomBlurFeature.GlobalBlurTextureId);

				builder.SetRenderFunc<PassData>((data, ctx) => Execute(data, ctx.cmd));
			}
		}

		// Kawase ping-pong loop. UnsafeCommandBuffer 직접 — SetRenderTarget + DrawProcedural fullscreen triangle.
		// Unity 6 RG 정합: MeshTopology.Triangles 3 vertex + _BlitScaleBias=(1,1,0,0) 명시 (Blit.hlsl 의 GetFullScreenTriangleTexCoord 가 사용).
		// destination = imported external RT (TargetRT 있을 때) → iterations 짝수 시 마지막 결과가 external 에 직접 박힘.
		private static void Execute(PassData data, UnsafeCommandBuffer cmd)
		{
			data.PropertyBlock.Clear();

			Texture current = data.ColorSource;

			for (int i = 0; i < data.Iterations; i++)
			{
				Texture target = (i % 2 == 0) ? (Texture)data.Source : (Texture)data.Destination;
				float iterationScale = (i + 0.5f) * data.Offset;

				data.PropertyBlock.SetTexture(BlitTextureId, current);
				data.PropertyBlock.SetVector(CustomBlurFeature.BlurParamsId,
					new Vector4(data.Intensity, iterationScale, 0f, 0f));
				data.PropertyBlock.SetVector(BlitScaleBiasId, new Vector4(1f, 1f, 0f, 0f));

				cmd.SetRenderTarget(target, 0, CubemapFace.Unknown, 0);
				cmd.DrawProcedural(Matrix4x4.identity, data.BlurMaterial, 0, MeshTopology.Triangles, 3, 1, data.PropertyBlock);

				current = target;
			}
		}

		private static readonly int BlitTextureId = Shader.PropertyToID("_BlitTexture");
		private static readonly int BlitScaleBiasId = Shader.PropertyToID("_BlitScaleBias");
	}
}
