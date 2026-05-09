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

		public CustomBlurPass(CustomBlurFeature feature)
		{
			this.feature = feature;
			profilingSampler = new ProfilingSampler(PASS_NAME);
			propertyBlock = new MaterialPropertyBlock();
		}

		public void Dispose()
		{
		}

		private class PassData
		{
			public TextureHandle ColorSource;
			public TextureHandle Source;
			public TextureHandle Destination;
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

			RenderTextureDescriptor cameraDescriptor = renderGraph.GetTextureDesc(cameraColorSource).ToDescriptor();
			int width = Mathf.Max(1, Mathf.RoundToInt(cameraDescriptor.width / feature.Downsample));
			int height = Mathf.Max(1, Mathf.RoundToInt(cameraDescriptor.height / feature.Downsample));

			RenderTextureDescriptor blurDescriptor = new RenderTextureDescriptor(width, height, GraphicsFormat.B10G11R11_UFloatPack32, 0);

			TextureDesc sourceDesc = new TextureDesc(blurDescriptor) { name = SOURCE_RT_NAME };
			TextureDesc destDesc = new TextureDesc(blurDescriptor) { name = DEST_RT_NAME };

			TextureHandle source = renderGraph.CreateTexture(sourceDesc);
			TextureHandle destination = renderGraph.CreateTexture(destDesc);

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

		private static void Execute(PassData data, UnityEngine.Rendering.CommandBuffer cmd)
		{
			// Initial blit: cameraColor → source (downsample)
			Blitter.BlitCameraTexture(cmd, data.ColorSource, data.Source);

			TextureHandle current = data.Source;
			TextureHandle target = data.Destination;

			// Kawase iterations — 매 iteration 의 offset 증가 (1.5, 2.5, 3.5, ...) for typical Kawase progression
			for (int i = 0; i < data.Iterations; i++)
			{
				float iterationScale = (i + 0.5f) * data.Offset;

				data.PropertyBlock.SetVector(CustomBlurFeature.BlurParamsId,
					new Vector4(data.Intensity, iterationScale, 0f, 0f));

				Blitter.BlitTexture(cmd, current, target, data.BlurMaterial, 0);

				// ping-pong swap
				(current, target) = (target, current);
			}

			// 최종 결과는 current (마지막 iteration 의 destination → swap 후 current). SetGlobalTextureAfterPass 의 destination 과 일치하도록 마지막 swap 후 current 를 destination 처럼 처리.
			// 단 SetGlobalTextureAfterPass 의 ref 가 *처음 declare* 된 destination handle — iteration 홀수/짝수 따라 미스매치 가능.
			// 보장: iteration 짝수 시 final = source, 홀수 시 final = destination.
			// 짝수 케이스 1회 추가 blit 으로 destination 박음.
			if (data.Iterations % 2 == 0)
			{
				Blitter.BlitTexture(cmd, current, target, data.BlurMaterial, 0);
			}
		}
	}

	internal static class TextureDescriptorExtensions
	{
		public static RenderTextureDescriptor ToDescriptor(this TextureDesc desc)
		{
			return new RenderTextureDescriptor((int)desc.width, (int)desc.height);
		}
	}
}
