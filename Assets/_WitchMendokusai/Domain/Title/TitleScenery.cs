using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace WitchMendokusai
{
	// 시작 화면 무대. 하늘 시각 고정 적용과 카메라의 느린 흔들림
	public class TitleScenery : MonoBehaviour
	{
		[Header("Sky")]
		[SerializeField] private SkyPresetSO skyPreset;
		[SerializeField] private Material skyboxMaterial;
		[SerializeField, Range(0f, 24f)] private float hour = 14f;
		[SerializeField] private float hoursPerDay = 24f;
		[SerializeField] private Light sun;
		[SerializeField, Range(0f, 360f)] private float sunAzimuth = 30f;
		[Tooltip("게임 카메라 거리용 안개를 넓은 무대에 맞게 줄이는 배율")]
		[SerializeField] private float fogDensityScale = 0.12f;

		[Header("Post")]
		[Tooltip("시작 화면에서만 켜는 색 보정. -100..100")]
		[SerializeField] private float postSaturation = 8f;
		[SerializeField] private float postExposure = 0.2f;
		[SerializeField] private float postVolumePriority = 100f;

		[Header("Camera drift")]
		[SerializeField] private Transform cameraTransform;
		[SerializeField] private Vector3 driftAmplitude = new(0.6f, 0.15f, 0f);
		[SerializeField] private float driftSpeed = 0.12f;
		[SerializeField] private float lookYawAmplitude = 1.2f;

		private Vector3 cameraBasePosition;
		private Quaternion cameraBaseRotation;

		private Volume postVolume;

		private void Start()
		{
			ApplySky();
			BuildPostVolume();
			if (cameraTransform != null)
			{
				cameraBasePosition = cameraTransform.position;
				cameraBaseRotation = cameraTransform.rotation;
			}
		}

		// 월드 SkyDirector 가 Update 에서 월드 시각으로 덮으므로 매 프레임 뒤에 다시 적용
		private void LateUpdate()
		{
			ApplySky();
			if (cameraTransform == null)
				return;
			float phase = Time.unscaledTime * driftSpeed * Mathf.PI * 2f;
			Vector3 offset = new(Mathf.Sin(phase) * driftAmplitude.x, Mathf.Sin(phase * 0.7f) * driftAmplitude.y, Mathf.Sin(phase * 0.5f) * driftAmplitude.z);
			cameraTransform.position = cameraBasePosition + cameraBaseRotation * offset;
			cameraTransform.rotation = cameraBaseRotation * Quaternion.Euler(0f, Mathf.Sin(phase * 0.5f) * lookYawAmplitude, 0f);
		}

		private void OnDestroy()
		{
			if (postVolume != null)
				Destroy(postVolume.sharedProfile);
		}

		// 저장 자산 없이 런타임 프로필 하나. 카메라 후처리도 여기서 켬
		private void BuildPostVolume()
		{
			if (cameraTransform != null && cameraTransform.TryGetComponent(out UniversalAdditionalCameraData cameraData))
				cameraData.renderPostProcessing = true;
			postVolume = gameObject.AddComponent<Volume>();
			postVolume.isGlobal = true;
			postVolume.priority = postVolumePriority;
			VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
			ColorAdjustments colorAdjustments = profile.Add<ColorAdjustments>(true);
			colorAdjustments.saturation.Override(postSaturation);
			colorAdjustments.postExposure.Override(postExposure);
			postVolume.sharedProfile = profile;
		}

		[ContextMenu("Apply Sky")]
		public void ApplySky()
		{
			if (skyPreset == null)
				return;
			float normalizedTime = Mathf.Repeat(hour / hoursPerDay, 1f);
			if (skyboxMaterial != null)
				RenderSettings.skybox = skyboxMaterial;
			SkyPresetApplier.ApplySky(skyPreset, skyboxMaterial, normalizedTime);
			SkyPresetApplier.ApplyShaderGlobals(skyPreset, normalizedTime);
			if (sun != null)
			{
				SkyPresetApplier.ApplyDirectionalLight(skyPreset, sun, normalizedTime, sunAzimuth);
				RenderSettings.sun = sun;
			}
			SkyPresetApplier.ApplyEnvironment(skyPreset, normalizedTime);
			RenderSettings.fogDensity *= fogDensityScale;
		}
	}
}
