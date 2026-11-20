using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace WitchMendokusai
{
	/// <summary>
	/// 궁극기 임팩트 화면 왜곡 (방사 흐림, 굴절 고리, 임팩트 프레임) 렌더러 기능. 렌더러 에셋에 한 번 붙임 (메뉴 WM/CombatFx/Build Ultimate Feel V2)
	/// - 후처리 뒤 카메라 색을 셰이더 한 번으로 다시 그림. 화면 위 그림 판이 아니라 그려진 장면 자체를 휨
	/// - UltimateImpactScreen.Active 인 동안만 패스를 올림. 평소 비용 0
	/// </summary>
	public sealed class UltimateImpactScreenFeature : ScriptableRendererFeature
	{
		[SerializeField] private Shader shader;
		[SerializeField] private RenderPassEvent injectionPoint = RenderPassEvent.AfterRenderingPostProcessing;

		private Material material;
		private ImpactPass pass;

		public override void Create()
		{
			pass = new ImpactPass { renderPassEvent = injectionPoint };
		}

		public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
		{
			if (UltimateImpactScreen.Active == false || renderingData.cameraData.cameraType != CameraType.Game || renderingData.cameraData.camera != UltimateImpactScreen.TargetCamera)
				return;
			if (material == null)
			{
				if (shader == null)
					shader = Shader.Find("WM/Fx/UltimateImpactScreen");
				if (shader == null)
				{
					Debug.LogError($"[{nameof(UltimateImpactScreenFeature)}] 셰이더 WM/Fx/UltimateImpactScreen 없음");
					return;
				}
				material = CoreUtils.CreateEngineMaterial(shader);
			}
			pass.Material = material;
			renderer.EnqueuePass(pass);
		}

		protected override void Dispose(bool disposing)
		{
			if (material != null)
				CoreUtils.Destroy(material);
		}

		private sealed class ImpactPass : ScriptableRenderPass
		{
			private const string PASS_NAME = "WM Ultimate Impact Screen";

			public Material Material;

			public ImpactPass()
			{
				profilingSampler = new ProfilingSampler(PASS_NAME);
				requiresIntermediateTexture = true;
			}

			public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
			{
				UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
				if (resourceData.isActiveTargetBackBuffer)
					return;
				TextureHandle source = resourceData.activeColorTexture;
				TextureDesc description = renderGraph.GetTextureDesc(source);
				description.name = PASS_NAME;
				description.clearBuffer = false;
				TextureHandle destination = renderGraph.CreateTexture(description);
				RenderGraphUtils.BlitMaterialParameters parameters = new(source, destination, Material, 0);
				renderGraph.AddBlitPass(parameters, PASS_NAME);
				resourceData.cameraColor = destination;
			}
		}
	}
}
