using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace WitchMendokusai
{
	// WM 자체 Kawase blur — Universal-Blur 패키지 폐기 (TASK-WM-076).
	// Bunkasha Games GDC 2003 — 4 corner sample + average, multi-iteration ping-pong.
	// 결과 = `_GlobalFullScreenBlurTexture` global texture (UI / postprocess 사용처가 SAMPLE).
	public class CustomBlurFeature : ScriptableRendererFeature
	{
		public static readonly int GlobalBlurTextureId = Shader.PropertyToID("_GlobalFullScreenBlurTexture");
		public static readonly int BlurParamsId = Shader.PropertyToID("_BlurParams");

		[Header("Blur Settings")]
		[SerializeField] private bool useDualKawase = true;

		[Range(1, 8)]
		[SerializeField] private int iterations = 4;

		[Range(1f, 10f)]
		[SerializeField] private float downsample = 2.0f;

		[Range(0f, 4f)]
		[SerializeField] private float intensity = 1f;

		[Range(0f, 4f)]
		[SerializeField] private float offset = 1f;

		[Header("Injection")]
		[Tooltip("Overlay Canvas: AfterRenderingPostProcessing\n그 외: BeforeRenderingTransparents")]
		[SerializeField] private RenderPassEvent injectionPoint = RenderPassEvent.AfterRenderingPostProcessing;

		[Header("Material")]
		[SerializeField] private Shader blurShader;

		[Header("Output")]
		[Tooltip("Blur 결과를 외부에서 sample 할 RT asset. UI Toolkit USS background-image 등 사용처가 reference. 미할당 시 globaltexture 만 출력 (76 호환).")]
		[SerializeField] private RenderTexture targetRT;

		private Material runtimeMaterial;
		private CustomBlurPass blurPass;

		public bool UseDualKawase => useDualKawase;
		public int Iterations => iterations;
		public float Downsample => downsample;
		public float Intensity => intensity;
		public float Offset => offset;
		public Material BlurMaterial => runtimeMaterial;
		public RenderTexture TargetRT => targetRT;

		public override void Create()
		{
			blurPass = new CustomBlurPass(this);
			blurPass.renderPassEvent = injectionPoint;
		}

		public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
		{
			if (EnsureMaterial() == false)
			{
				Debug.LogError($"[{nameof(CustomBlurFeature)}] blur shader 또는 material 누락. 'WM/CustomBlur' shader 확인.");
				return;
			}

			if (renderingData.cameraData.isPreviewCamera == true || renderingData.cameraData.isSceneViewCamera == true)
			{
				Shader.SetGlobalTexture(GlobalBlurTextureId, Texture2D.linearGrayTexture);
				return;
			}

			// Game 카메라만 blur pass 활성. Reflection Probe / sub-camera 등 다른 type 은 skip — RT overwrite 방지.
			if (renderingData.cameraData.cameraType != CameraType.Game)
			{
				return;
			}

			// 사용처 (SettingView 등) 가 BlurRequest.Add() 한 동안만 pass 활성화. 닫혀있으면 GPU 비용 0.
			if (BlurRequest.Count == 0)
			{
				return;
			}

			renderer.EnqueuePass(blurPass);
		}

		protected override void Dispose(bool disposing)
		{
			blurPass?.Dispose();
			if (runtimeMaterial != null)
				CoreUtils.Destroy(runtimeMaterial);
		}

		private bool EnsureMaterial()
		{
			if (blurShader == null)
				blurShader = Shader.Find("WM/CustomBlur");

			if (blurShader == null)
				return false;

			if (runtimeMaterial == null)
				runtimeMaterial = CoreUtils.CreateEngineMaterial(blurShader);

			return runtimeMaterial != null;
		}
	}
}
