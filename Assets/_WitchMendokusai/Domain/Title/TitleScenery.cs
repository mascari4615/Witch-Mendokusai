using UnityEngine;

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

		[Header("Camera drift")]
		[SerializeField] private Transform cameraTransform;
		[SerializeField] private Vector3 driftAmplitude = new(0.6f, 0.15f, 0f);
		[SerializeField] private float driftSpeed = 0.12f;
		[SerializeField] private float lookYawAmplitude = 1.2f;

		private Vector3 cameraBasePosition;
		private Quaternion cameraBaseRotation;

		private void Start()
		{
			ApplySky();
			if (cameraTransform != null)
			{
				cameraBasePosition = cameraTransform.position;
				cameraBaseRotation = cameraTransform.rotation;
			}
		}

		private void Update()
		{
			if (cameraTransform == null)
				return;
			float phase = Time.unscaledTime * driftSpeed * Mathf.PI * 2f;
			Vector3 offset = new(Mathf.Sin(phase) * driftAmplitude.x, Mathf.Sin(phase * 0.7f) * driftAmplitude.y, Mathf.Sin(phase * 0.5f) * driftAmplitude.z);
			cameraTransform.position = cameraBasePosition + cameraBaseRotation * offset;
			cameraTransform.rotation = cameraBaseRotation * Quaternion.Euler(0f, Mathf.Sin(phase * 0.5f) * lookYawAmplitude, 0f);
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
		}
	}
}
