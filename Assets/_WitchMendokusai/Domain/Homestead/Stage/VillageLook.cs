using UnityEngine;

namespace WitchMendokusai
{
	// 마을에 있는 동안의 빛. 하늘 상자, 눕힌 해와 부드러운 그림자, 안개
	// 하늘 시계 (SkyDirector) 가 쓴 값 위에 얹고 나갈 때 되돌림. 스테이지의 자식이라 마을을 나가면 같이 꺼짐
	[DefaultExecutionOrder(200)]
	public sealed class VillageLook : MonoBehaviour
	{
		private static readonly int SUN_DIRECTION = Shader.PropertyToID("_VillageSunDirection");
		private static readonly int SUN_COLOR = Shader.PropertyToID("_VillageSunColor");
		private static readonly int WIND = Shader.PropertyToID("_WM_Wind");
		private static readonly int DAYLIGHT = Shader.PropertyToID("_VillageDaylight");
		private static readonly int NORMALIZED_TIME = Shader.PropertyToID("_WMNormalizedTime");

		[Header("Sun")]
		[Tooltip("해가 가장 높을 때의 각. 낮출수록 그림자가 김")]
		[SerializeField] private float highestSun = 48f;
		[SerializeField] private float lowestSun = 16f;
		[Tooltip("하루 동안 해가 도는 방위 (도). 시작과 끝")]
		[SerializeField] private Vector2 sunAzimuth = new(-70f, 70f);
		[SerializeField] private float sunIntensity = 1.5f;
		[SerializeField] private Color sunTint = new(1f, 0.95f, 0.84f);
		[SerializeField, Range(0f, 1f)] private float shadowStrength = 0.9f;

		[Header("Air")]
		[SerializeField] private float ambient = 0.86f;
		[SerializeField] private float fogDensity = 0.55f;
		[SerializeField, Range(0f, 1f)] private float fogBlue = 0.5f;

		[Header("Wind")]
		[Tooltip("방향과 세기 (m)")]
		[SerializeField] private Vector2 wind = new(0.1f, 0.06f);
		[SerializeField] private float windSpeed = 1.3f;

		// 화면 색 (노출, 대비, 채도, 빛 번짐, 비네트) 은 덮지 않음. 숲과 같은 카메라 공통 프로필 (Camera.prefab 의 Gaussian) 을 그대로 씀

		private Light sun;
		private LightShadows sunShadows;
		private float sunStrength;
		private Quaternion sunRotation;
		private Material skybox;

		public static void Ensure(Transform parent)
		{
			if (parent.GetComponentInChildren<VillageLook>(true) != null)
				return;
			GameObject look = new(nameof(VillageLook));
			look.transform.SetParent(parent, false);
			look.AddComponent<VillageLook>();
		}

		private void OnEnable()
		{
			skybox = RenderSettings.skybox;
			RenderSettings.skybox = HomesteadArt.Instance.Sky;
			Shader.SetGlobalVector(WIND, new Vector4(wind.x, wind.y, windSpeed, transform.position.y));
			sun = RenderSettings.sun;
			if (sun == null)
				return;
			sunShadows = sun.shadows;
			sunStrength = sun.shadowStrength;
			sunRotation = sun.transform.rotation;
			sun.shadows = LightShadows.Soft;
			sun.shadowStrength = shadowStrength;
		}

		private void OnDisable()
		{
			RenderSettings.skybox = skybox;
			Shader.SetGlobalVector(WIND, Vector4.zero);
			if (sun != null)
			{
				sun.shadows = sunShadows;
				sun.shadowStrength = sunStrength;
				sun.transform.rotation = sunRotation;
			}
			if (SkyDirector.TryGetExistingInstance(out SkyDirector sky) && sky.ActivePreset != null)
			{
				float time = Shader.GetGlobalFloat(NORMALIZED_TIME);
				// 박물관을 다시 지을 때처럼 실내인 채 꺼지면 실내 빛으로 되돌림
				if (IndoorScope.IsIndoor)
					SkyPresetApplier.ApplyIndoorEnvironment(sky.ActivePreset, time, sky.Indoor);
				else
					SkyPresetApplier.ApplyEnvironment(sky.ActivePreset, time);
				if (sun != null)
					SkyPresetApplier.ApplyDirectionalLight(sky.ActivePreset, sun, time, sunRotation.eulerAngles.y);
			}
		}

		private void LateUpdate()
		{
			if (sun == null || SkyDirector.TryGetExistingInstance(out SkyDirector sky) == false || sky.ActivePreset == null)
				return;
			SkyPresetSO preset = sky.ActivePreset;
			float time = Shader.GetGlobalFloat(NORMALIZED_TIME);
			float altitude = Mathf.Max(0f, preset.SunAltitude.Evaluate(time));
			Color sunColor = preset.SunLightColor.Evaluate(time) * sunTint;
			sun.color = sunColor;
			sun.intensity = preset.SunIntensity.Evaluate(time) * sunIntensity;
			sun.transform.rotation = Quaternion.Euler(Mathf.Lerp(lowestSun, highestSun, altitude), Mathf.Lerp(sunAzimuth.x, sunAzimuth.y, Mathf.InverseLerp(0.2f, 0.8f, time)), 0f);
			Shader.SetGlobalVector(SUN_DIRECTION, -sun.transform.forward);
			Shader.SetGlobalColor(SUN_COLOR, sunColor * Mathf.Clamp01(preset.SunIntensity.Evaluate(time)));
			// 하늘 시계의 해 높이 (0 밤, 1 한낮). 햇살 줄기처럼 낮에만 보이는 것
			Shader.SetGlobalFloat(DAYLIGHT, altitude);

			// 박물관과 갱도도 이 빛을 씀. 실내면 시계를 약하게만 따름
			bool isIndoor = IndoorScope.IsIndoor;
			Color air = (isIndoor ? sky.Indoor.Ambient(preset.AmbientColor, time) : preset.AmbientColor.Evaluate(time)) * ambient;
			RenderSettings.ambientSkyColor = air * preset.AmbientSkyTint;
			RenderSettings.ambientEquatorColor = air * preset.AmbientEquatorTint;
			RenderSettings.ambientGroundColor = air * preset.AmbientGroundTint;
			Color fog = isIndoor ? sky.Indoor.Blend(preset.FogColor, time) : preset.FogColor.Evaluate(time);
			Color zenith = isIndoor ? sky.Indoor.Blend(preset.ZenithColor, time) : preset.ZenithColor.Evaluate(time);
			RenderSettings.fogColor = Color.Lerp(fog, zenith, fogBlue);
			RenderSettings.fogDensity = preset.FogDensity.Evaluate(time) * fogDensity;
		}
	}
}
