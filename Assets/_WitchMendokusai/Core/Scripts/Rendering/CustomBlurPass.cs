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
		private readonly MaterialPropertyBlock propertyBlock;

		private RTHandle externalRTHandle;
		private RenderTexture lastExternalRT;

		public CustomBlurPass(CustomBlurFeature feature)
		{
			this.feature = feature;
			// 베이스(ScriptableRenderPass)가 이미 profilingSampler 를 들고 있다 — 같은 이름으로 하나 더 두면
			// 프로파일러가 어느 쪽을 보느냐에 따라 이 패스가 이름 없이 잡힌다. 베이스 것을 그대로 채운다.
			profilingSampler = new ProfilingSampler(PASS_NAME);
			propertyBlock = new MaterialPropertyBlock();
		}

		public void Dispose()
		{
			externalRTHandle?.Release();
			externalRTHandle = null;
			lastExternalRT = null;
		}

		private const int PASS_KAWASE = 0;
		private const int PASS_DUAL_DOWN = 1;
		private const int PASS_DUAL_UP = 2;

		private class PassData
		{
			public TextureHandle ColorSource;
			public TextureHandle Source;
			public TextureHandle Destination;
			public RTHandle ExternalRT;
			public Material BlurMaterial;
			public MaterialPropertyBlock PropertyBlock;
			public bool UseDualKawase;
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
				passData.UseDualKawase = feature.UseDualKawase;
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

		// ping-pong blit helper.
		private static void Blit(UnsafeCommandBuffer cmd, PassData data, Texture source, Texture target, int passIndex, float iterationScale)
		{
			data.PropertyBlock.SetTexture(BlitTextureId, source);
			data.PropertyBlock.SetVector(CustomBlurFeature.BlurParamsId, new Vector4(data.Intensity, iterationScale, 0f, 0f));
			data.PropertyBlock.SetVector(BlitScaleBiasId, new Vector4(1f, 1f, 0f, 0f));
			cmd.SetRenderTarget(target, 0, CubemapFace.Unknown, 0);
			cmd.DrawProcedural(Matrix4x4.identity, data.BlurMaterial, passIndex, MeshTopology.Triangles, 3, 1, data.PropertyBlock);
		}

		// DualKawase: N down passes + N up passes (ARM GDC 2015).
		// Kawase fallback: legacy ping-pong.
		// Unity 6 RG: MeshTopology.Triangles 3 vertex + _BlitScaleBias=(1,1,0,0) (Blit.hlsl GetFullScreenTriangleTexCoord).
		private static void Execute(PassData data, UnsafeCommandBuffer cmd)
		{
			data.PropertyBlock.Clear();

			if (data.UseDualKawase == true)
			{
				// down pass (0..N-1): source→source/destination ping-pong with Down shader
				Texture current = data.ColorSource;
				for (int i = 0; i < data.Iterations; i++)
				{
					Texture target = (i % 2 == 0) ? (Texture)data.Source : (Texture)data.Destination;
					Blit(cmd, data, current, target, PASS_DUAL_DOWN, (i + 0.5f) * data.Offset);
					current = target;
				}
				// up pass (N-1..0): reverse with Up shader, final result lands on Destination
				for (int i = data.Iterations - 1; i >= 0; i--)
				{
					Texture target = (i % 2 == 0) ? (Texture)data.Destination : (Texture)data.Source;
					Blit(cmd, data, current, target, PASS_DUAL_UP, (i + 0.5f) * data.Offset);
					current = target;
				}
			}
			else
			{
				// Kawase legacy ping-pong
				Texture current = data.ColorSource;
				for (int i = 0; i < data.Iterations; i++)
				{
					Texture target = (i % 2 == 0) ? (Texture)data.Source : (Texture)data.Destination;
					Blit(cmd, data, current, target, PASS_KAWASE, (i + 0.5f) * data.Offset);
					current = target;
				}
			}
		}

		private static readonly int BlitTextureId = Shader.PropertyToID("_BlitTexture");
		private static readonly int BlitScaleBiasId = Shader.PropertyToID("_BlitScaleBias");
	}
}
