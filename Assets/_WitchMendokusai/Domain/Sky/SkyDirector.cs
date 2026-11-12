using UnityEngine;
using UnityEngine.Rendering;
using VContainer;

namespace WitchMendokusai
{
	// 시간대별 하늘·라이팅 lerp. WorldClock 의 시간 → SkyPresetSO 의 Gradient 평가
	// → Skybox material property + DirLight + Fog/Ambient + Shader Global 적용.
	// SO 값 캐싱 X — 인스펙터 런타임 변경 즉시 반영.
	// (TASK-WM-054-C C1: Skybox 만 / C2: DirLight / C3a: Fog/Ambient / C6: Shader Modding Contract)
	public class SkyDirector : MonoBehaviour
	{
		public static SkyDirector Instance { get; private set; }

		public static bool TryGetExistingInstance(out SkyDirector mgr)
		{
			mgr = Instance;
			return mgr != null;
		}

		private WorldClock worldClock;

		[Inject]
		public void Construct(WorldClock worldClock)
		{
			this.worldClock = worldClock;
		}

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

		private void Awake()
		{
			if (Instance != null && Instance != this)
			{
				Destroy(gameObject);
				return;
			}
			Instance = this;
			ApplySkyboxMaterial();
			StageManager.OnStageChanged += OnStageChanged;
		}

		private void OnDestroy()
		{
			if (Instance == this)
			{
				Instance = null;
				StageManager.OnStageChanged -= OnStageChanged;
			}
		}

		// 자기 직접광이 있는 스테이지(Home, StartTown)는 씬 태양 끔
		private void OnStageChanged(Stage stage, StageObject stageObject)
		{
			Light sun = RenderSettings.sun;
			if (sun != null)
				sun.enabled = HasOwnDirectionalLight(stageObject) == false;
		}

		public static bool HasOwnDirectionalLight(StageObject stageObject)
		{
			foreach (Light light in stageObject.GetComponentsInChildren<Light>())
			{
				if (light.type == LightType.Directional && light.enabled == true)
					return true;
			}
			return false;
		}

		private void Start()
		{
			FindDirectionalLight();
			ApplyPreset(force: true);

			if (debugLogStartup == true)
			{
				// init-order-ok: debugLogStartup-gated 디버그 진단 로그 전용 — find 결과에 기능 의존 0 (WM-118 적용외).
				Camera[] allCameras = FindObjectsByType<Camera>(FindObjectsInactive.Include); // init-order-ok
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

		// 정본은 씬 Lighting의 Sun Source. 없는 씬(Idle, Arena)만 켜진 직접광 탐색
		// 꺼진 빛이나 풀에 든 스테이지 빛은 제외. 값만 바뀌고 화면 변화 없음
		private void FindDirectionalLight()
		{
			if (RenderSettings.sun != null)
			{
				directionalLight = RenderSettings.sun;
				return;
			}

			Light[] lights = FindObjectsByType<Light>(FindObjectsInactive.Exclude);
			foreach (Light light in lights)
			{
				if (light.type == LightType.Directional && light.enabled == true)
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

			if (worldClock.Config == null)
				return;

			float normalizedTime = SkyTimeOverride.IsActive
				? Mathf.Repeat(SkyTimeOverride.Hour / worldClock.Config.HoursPerDay, 1f)
				: ComputeNormalizedTime(worldClock);

			if (force == false && Mathf.Approximately(normalizedTime, cachedNormalizedTime) == true)
				return;

			cachedNormalizedTime = normalizedTime;

			ApplySky(normalizedTime);
			ApplyShaderGlobals(normalizedTime);

			if (applyDirectionalLight == true)
				ApplyDirectionalLight(normalizedTime);

			if (applyEnvironment == true)
				ApplyEnvironment(normalizedTime);
		}

		// 모더 ShaderGraph contract — SkyboxMaterial 유무와 무관하게 항상 노출.
		// 모더 Skybox/Water/PostFX 등 셰이더가 _WM* 글로벌 받아 시간대 색감 합성.
		private void ApplyShaderGlobals(float normalizedTime) => SkyPresetApplier.ApplyShaderGlobals(ActivePreset, normalizedTime);

		private static float ComputeNormalizedTime(WorldClock worldClock)
		{
			float minutesPerDay = worldClock.Config.HoursPerDay * 60f;
			float currentMinutes = worldClock.Hour * 60f + worldClock.Minute;
			return currentMinutes / minutesPerDay;
		}

		private void ApplySky(float t) => SkyPresetApplier.ApplySky(ActivePreset, SkyboxMaterial, t);

		private void ApplyDirectionalLight(float t)
		{
			if (directionalLight == null)
			{
				FindDirectionalLight();
				if (directionalLight == null)
					return;
			}

			SkyPresetApplier.ApplyDirectionalLight(ActivePreset, directionalLight, t, sunAzimuth);
		}

		// 밤의 달빛 겸용. 해가 지평선 아래여도 빛 방향은 위쪽
		public static float ComputeLightPitch(float altitude, float minPitch) => SkyPresetApplier.ComputeLightPitch(altitude, minPitch);

		private void ApplyEnvironment(float t) => SkyPresetApplier.ApplyEnvironment(ActivePreset, t);

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
			ApplyShaderGlobals(t);
			if (applyDirectionalLight == true)
				ApplyDirectionalLight(t);
			if (applyEnvironment == true)
				ApplyEnvironment(t);
		}
	}
}
