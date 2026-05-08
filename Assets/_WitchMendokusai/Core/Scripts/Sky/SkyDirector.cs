using UnityEngine;
using UnityEngine.Rendering;

namespace WitchMendokusai
{
	// 시간대별 하늘·라이팅 lerp. WorldClock 의 시간 → SkyPresetSO 의 Gradient 평가
	// → Skybox material property + DirLight + Fog/Ambient 적용.
	// SO 값 캐싱 X — 인스펙터 런타임 변경 즉시 반영.
	// (TASK-WM-054-C C1: Skybox 만 / C2: DirLight / C3a: Fog/Ambient)
	public class SkyDirector : Singleton<SkyDirector>
	{
		[field: SerializeField] public SkyPresetSO ActivePreset { get; set; }
		[field: SerializeField] public Material SkyboxMaterial { get; private set; }

		[Header("C2 / C3a — 단계별 활성")]
		[SerializeField] private bool applyDirectionalLight = true;
		[SerializeField] private bool applyEnvironment = true;

		[Header("C2 — Sun Rotation")]
		[SerializeField, Range(0f, 360f)] private float sunAzimuth = 30f;

		[Header("Debug")]
		[SerializeField] private bool debugLogStartup = false;

		private Light directionalLight;
		private float cachedNormalizedTime = -1f;

		private static readonly int ZenithColorId = Shader.PropertyToID("_ZenithColor");
		private static readonly int HorizonColorId = Shader.PropertyToID("_HorizonColor");
		private static readonly int SunDiscColorId = Shader.PropertyToID("_SunDiscColor");
		private static readonly int SunDirectionId = Shader.PropertyToID("_SunDirection");
		private static readonly int StarAlphaId = Shader.PropertyToID("_StarAlpha");

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
		private static void EnsureSingletonOnPlay()
		{
			_ = Instance;
		}

		protected override void Awake()
		{
			base.Awake();
			ApplySkyboxMaterial();
		}

		private void Start()
		{
			FindDirectionalLight();
			ApplyPreset(force: true);

			if (debugLogStartup == true)
			{
				Camera[] allCameras = FindObjectsByType<Camera>(FindObjectsInactive.Include);
				Debug.Log($"[SkyDirector] Camera scan: {allCameras.Length} cameras");
				foreach (Camera cam in allCameras)
				{
					Skybox sb = cam.GetComponent<Skybox>();
					string skyboxInfo = sb != null ? (sb.material != null ? sb.material.name : "null mat") : "no Skybox component";
					Debug.Log($"[SkyDirector]   '{cam.name}' depth={cam.depth} clearFlags={cam.clearFlags} | Skybox comp: {skyboxInfo}");
				}
			}
		}

		private void Update()
		{
			ApplyPreset(force: false);
		}

		private void ApplySkyboxMaterial()
		{
			if (SkyboxMaterial == null)
				return;

			RenderSettings.skybox = SkyboxMaterial;

			// Camera 에 Skybox component 가 attach 돼 있으면 RenderSettings.skybox 보다 우선됨 → 동기화 강제.
			Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Include);
			foreach (Camera camera in cameras)
			{
				Skybox cameraSkybox = camera.GetComponent<Skybox>();
				if (cameraSkybox != null)
				{
					Debug.Log($"[SkyDirector] Camera '{camera.name}' has Skybox component (was: {(cameraSkybox.material != null ? cameraSkybox.material.name : "null")}) → override with {SkyboxMaterial.name}");
					cameraSkybox.material = SkyboxMaterial;
				}
			}

			DynamicGI.UpdateEnvironment();
		}

		private void FindDirectionalLight()
		{
			Light[] lights = FindObjectsByType<Light>(FindObjectsInactive.Include);
			foreach (Light light in lights)
			{
				if (light.type == LightType.Directional)
				{
					directionalLight = light;
					return;
				}
			}
		}

		private void ApplyPreset(bool force)
		{
			if (ActivePreset == null)
				return;

			if (WorldClock.TryGetExistingInstance(out WorldClock worldClock) == false)
				return;

			if (worldClock.Config == null)
				return;

			float normalizedTime = ComputeNormalizedTime(worldClock);

			if (force == false && Mathf.Approximately(normalizedTime, cachedNormalizedTime) == true)
				return;

			cachedNormalizedTime = normalizedTime;

			ApplySky(normalizedTime);

			if (applyDirectionalLight == true)
				ApplyDirectionalLight(normalizedTime);

			if (applyEnvironment == true)
				ApplyEnvironment(normalizedTime);
		}

		private static float ComputeNormalizedTime(WorldClock worldClock)
		{
			float minutesPerDay = worldClock.Config.HoursPerDay * 60f;
			float currentMinutes = worldClock.Hour * 60f + worldClock.Minute;
			return currentMinutes / minutesPerDay;
		}

		private void ApplySky(float t)
		{
			if (SkyboxMaterial == null)
				return;

			SkyboxMaterial.SetColor(ZenithColorId, ActivePreset.ZenithColor.Evaluate(t));
			SkyboxMaterial.SetColor(HorizonColorId, ActivePreset.HorizonColor.Evaluate(t));
			SkyboxMaterial.SetColor(SunDiscColorId, ActivePreset.SunDiscColor.Evaluate(t));

			float altitude = ActivePreset.SunAltitude.Evaluate(t);
			Vector3 sunDirection = ComputeSunDirection(altitude);
			SkyboxMaterial.SetVector(SunDirectionId, sunDirection);

			SkyboxMaterial.SetFloat(StarAlphaId, ActivePreset.StarAlpha.Evaluate(t));
		}

		private static Vector3 ComputeSunDirection(float altitude)
		{
			// altitude: -1=수평선 아래 / 0=수평선 / 1=천정
			float pitch = altitude * 90f * Mathf.Deg2Rad;
			return new Vector3(0f, Mathf.Sin(pitch), Mathf.Cos(pitch));
		}

		private void ApplyDirectionalLight(float t)
		{
			if (directionalLight == null)
			{
				FindDirectionalLight();
				if (directionalLight == null)
					return;
			}

			directionalLight.color = ActivePreset.SunLightColor.Evaluate(t);
			directionalLight.intensity = ActivePreset.SunIntensity.Evaluate(t);

			float altitude = ActivePreset.SunAltitude.Evaluate(t);
			float pitchDegrees = altitude * 90f;
			directionalLight.transform.rotation = Quaternion.Euler(pitchDegrees, sunAzimuth, 0f);
		}

		private void ApplyEnvironment(float t)
		{
			RenderSettings.ambientMode = AmbientMode.Flat;
			RenderSettings.ambientLight = ActivePreset.AmbientColor.Evaluate(t);

			RenderSettings.fog = true;
			RenderSettings.fogMode = ActivePreset.FogMode;
			RenderSettings.fogColor = ActivePreset.FogColor.Evaluate(t);
			RenderSettings.fogDensity = ActivePreset.FogDensity.Evaluate(t);
		}

		[ContextMenu("Debug/Reapply Now")]
		private void DebugReapplyNow()
		{
			cachedNormalizedTime = -1f;
			Camera mainCamera = Camera.main;
			Color zenith = ActivePreset != null ? ActivePreset.ZenithColor.Evaluate(0.78f) : Color.magenta;
			Debug.Log($"[SkyDirector] DebugReapplyNow — RenderSettings.skybox: {(RenderSettings.skybox != null ? RenderSettings.skybox.name : "null")} | SkyboxMaterial: {(SkyboxMaterial != null ? SkyboxMaterial.name : "null")} | Camera.main.clearFlags: {(mainCamera != null ? mainCamera.clearFlags.ToString() : "n/a")} | Sunset zenith RGB: ({zenith.r:F2}, {zenith.g:F2}, {zenith.b:F2})");
			ApplyPreset(force: true);
		}

		[ContextMenu("Debug/Force time 0.00 (Midnight)")]
		private void DebugSetMidnight() => DebugApplyAtTime(0.00f);

		[ContextMenu("Debug/Force time 0.25 (Dawn)")]
		private void DebugSetDawn() => DebugApplyAtTime(0.25f);

		[ContextMenu("Debug/Force time 0.50 (Noon)")]
		private void DebugSetNoon() => DebugApplyAtTime(0.50f);

		[ContextMenu("Debug/Force time 0.78 (Sunset)")]
		private void DebugSetSunset() => DebugApplyAtTime(0.78f);

		[ContextMenu("Debug/Force time 0.90 (Night)")]
		private void DebugSetNight() => DebugApplyAtTime(0.90f);

		private void DebugApplyAtTime(float t)
		{
			cachedNormalizedTime = -1f;
			ApplySky(t);
			if (applyDirectionalLight == true)
				ApplyDirectionalLight(t);
			if (applyEnvironment == true)
				ApplyEnvironment(t);
		}
	}
}
