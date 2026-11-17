using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace WitchMendokusai
{
	/// <summary>
	/// 전체 메뉴 가운데에서 도는 코어 모델의 무대. 전용 카메라가 RenderTexture 로 그리고 메뉴가 그 그림을 띄움
	/// - 메뉴가 열린 동안만 카메라가 켜짐
	/// - 게임이 멈춘 동안에도 돌아야 해서 시간은 unscaled
	/// - 빛은 만들지 않음 (URP 에서 빛이 메인 씬으로 샘, DiscoveryPreviewController 와 같은 이유). 명암은 재질 셰이더가 계산
	/// </summary>
	public class FullMenuCoreStage : MonoBehaviour
	{
		// 미리보기 전용 층. 도감 미리보기와 같은 층을 쓰고 자리로 떼어 놓음
		private const int PREVIEW_LAYER = 30;
		private const int DEPTH_BITS = 24;
		private const float FULL_TURN = 360f;
		private static readonly Vector3 STAGE_POSITION = new(-10000f, 10000f, 0f);
		private static readonly int PULSE_ID = Shader.PropertyToID("_Pulse");

		private FullMenuSettings settings;
		private Camera stageCamera;
		private Transform mount;
		private Renderer[] renderers;
		private MaterialPropertyBlock propertyBlock;
		private float angle;

		public RenderTexture Texture { get; private set; }
		public bool HasModel => mount != null && mount.childCount > 0;

		public void Build(FullMenuSettings settings)
		{
			this.settings = settings;
			transform.position = STAGE_POSITION;

			Texture = new RenderTexture(settings.CoreTextureSize, settings.CoreTextureSize, DEPTH_BITS, RenderTextureFormat.ARGB32)
			{
				name = "FullMenuCoreRT",
				antiAliasing = 4,
			};
			Texture.Create();

			GameObject cameraObject = new("FullMenuCoreCamera");
			cameraObject.transform.SetParent(transform, false);
			stageCamera = cameraObject.AddComponent<Camera>();
			stageCamera.targetTexture = Texture;
			stageCamera.clearFlags = CameraClearFlags.SolidColor;
			// 투명 배경. 메뉴의 빛 번짐 그림 위에 모델만 얹힘
			stageCamera.backgroundColor = Color.clear;
			stageCamera.cullingMask = 1 << PREVIEW_LAYER;
			stageCamera.enabled = false;

			UniversalAdditionalCameraData cameraData = stageCamera.GetUniversalAdditionalCameraData();
			cameraData.renderType = CameraRenderType.Base;
			cameraData.renderPostProcessing = false;
			cameraData.volumeLayerMask = 0;
			cameraData.requiresColorOption = CameraOverrideOption.Off;
			cameraData.requiresDepthOption = CameraOverrideOption.Off;
			cameraData.renderShadows = false;

			GameObject mountObject = new("FullMenuCoreMount");
			mountObject.transform.SetParent(transform, false);
			mount = mountObject.transform;

			if (settings.CorePrefab != null)
			{
				GameObject model = Instantiate(settings.CorePrefab, mount);
				model.transform.localPosition = Vector3.zero;
				SetLayerRecursive(model, PREVIEW_LAYER);
				renderers = model.GetComponentsInChildren<Renderer>();
			}
			propertyBlock = new MaterialPropertyBlock();
			PlaceCamera();
		}

		public void SetActive(bool active)
		{
			if (stageCamera == null)
				return;
			stageCamera.enabled = active && HasModel;
			if (active)
				PlaceCamera();
		}

		private void PlaceCamera()
		{
			Quaternion rotation = Quaternion.Euler(settings.CoreCameraPitch, 0f, 0f);
			stageCamera.transform.localPosition = rotation * (Vector3.back * settings.CoreCameraDistance);
			stageCamera.transform.localRotation = rotation;
			stageCamera.fieldOfView = settings.CoreCameraFieldOfView;
			stageCamera.nearClipPlane = settings.CoreCameraDistance * 0.1f;
			stageCamera.farClipPlane = settings.CoreCameraDistance * 4f;
		}

		private void Update()
		{
			if (stageCamera == null || stageCamera.enabled == false)
				return;

			float time = Time.unscaledTime;
			angle = Mathf.Repeat(angle + settings.CoreRotateSpeed * Time.unscaledDeltaTime, FULL_TURN);
			float bob = Mathf.Sin(time / settings.CoreBobPeriod * Mathf.PI * 2f) * settings.CoreBobHeight;
			mount.localPosition = Vector3.up * bob;
			mount.localRotation = Quaternion.Euler(0f, angle, 0f);

			// 0 ~ 1 로 오르내리는 값. 셰이더가 빛나는 부분 밝기에 곱함
			float pulse = Mathf.Sin(time / settings.CorePulsePeriod * Mathf.PI * 2f) * 0.5f + 0.5f;
			foreach (Renderer target in renderers)
			{
				target.GetPropertyBlock(propertyBlock);
				propertyBlock.SetFloat(PULSE_ID, pulse);
				target.SetPropertyBlock(propertyBlock);
			}
		}

		private void OnDestroy()
		{
			if (Texture != null)
			{
				Texture.Release();
				Destroy(Texture);
			}
		}

		private static void SetLayerRecursive(GameObject root, int layer)
		{
			root.layer = layer;
			for (int i = 0; i < root.transform.childCount; i++)
				SetLayerRecursive(root.transform.GetChild(i).gameObject, layer);
		}
	}
}
