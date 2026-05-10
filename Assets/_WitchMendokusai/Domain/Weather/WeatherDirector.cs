using UnityEngine;

namespace WitchMendokusai
{
	// 시각 풀세트 매니저. WeatherSystem.OnWeatherChanged 구독 → WeatherSO.VisualPrefab Instantiate → SO 노브 적용.
	// E1 = Singleton + SO 해석 + Console log.
	// E2 = Visual prefab Instantiate (Rain/Snow/Fog/Storm) + 이전 visual destroy.
	// E2-fix (2026-05-09) = WeatherSO 가 visual 정본 — Material / startSize / shape 등 모두 SO 노브로 ApplyCurrentSOToParticle.
	// E3 (2026-05-10) = Wet shader — IsWet 날씨 시 Shader.SetGlobalFloat("_Wetness", 0→1 lerp).
	// E4 후속 = SFX. E5 후속 = Storm 번개.
	// (TASK-WM-054-E E1~E3)
	public class WeatherDirector : Singleton<WeatherDirector>
	{
		private static readonly int WetnessId = Shader.PropertyToID("_Wetness");

		// 현재 적용된 WeatherSO — sub-E 후속 (E4 SFX / E5 Storm) 가 읽음.
		public WeatherSO CurrentWeatherSO { get; private set; }
		public WeatherType LastApplied { get; private set; } = WeatherType.Clear;

		// E2: 현재 instantiate 된 visual instance + 캐시 component (매 frame ApplyCurrentSOToParticle 호출 cost ↓).
		private GameObject currentVisualInstance;
		private ParticleSystem currentParticleSystem;
		private ParticleSystemRenderer currentParticleRenderer;

		// E3: wetness 0~1 lerp 상태. SO의 FadeMinutes 기반 실시간 속도.
		[SerializeField] private float wetnessLerpSpeed = 0.033f; // 1 / ~30s ≈ 기본 FadeMinutes 체감
		private float currentWetness;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
		private static void EnsureSingletonOnPlay()
		{
			_ = Instance;
		}

		private void Start()
		{
			if (WeatherSystem.TryGetExistingInstance(out WeatherSystem weatherSystem) == false)
			{
				Debug.LogWarning($"[{nameof(WeatherDirector)}] WeatherSystem 미발견 — OnWeatherChanged hook 미등록");
				return;
			}

			weatherSystem.OnWeatherChanged += HandleWeatherChanged;

			// 초기 sync — Start 가 WeatherSystem.Awake 보다 먼저 호출될 수도 있어 현재 weather 직접 적용.
			HandleWeatherChanged(weatherSystem.Current);
		}

		protected override void OnDestroy()
		{
			if (WeatherSystem.TryGetExistingInstance(out WeatherSystem weatherSystem) == true)
				weatherSystem.OnWeatherChanged -= HandleWeatherChanged;

			DestroyCurrentVisual();
			Shader.SetGlobalFloat(WetnessId, 0f);
			AudioManager.Instance.StopAmbient();

			base.OnDestroy();
		}

		// 룰 「수치 노출 / 런타임 tweak」 — 인스펙터에서 ParticleStartSize / wetnessLerpSpeed 슬라이더 변경 시 즉시 반영.
		private void Update()
		{
			ApplyCurrentSOToParticle();
			ApplyWetnessGlobal();
		}

		private void HandleWeatherChanged(WeatherType type)
		{
			LastApplied = type;
			CurrentWeatherSO = ResolveSO(type);

			string soName = CurrentWeatherSO != null ? CurrentWeatherSO.Name : "<not loaded>";
			Debug.Log($"[{nameof(WeatherDirector)}] applied {type} → SO '{soName}' (sfx={GetSfxKey()}, isWet={GetIsWet()})");

			// E2: 이전 visual destroy + 새 visual instantiate + SO 노브 즉시 적용.
			DestroyCurrentVisual();
			SpawnCurrentVisual();

			// E4: SFX ambient — SfxKey null/empty 면 PlayAmbient 가 stop 만 처리.
			AudioManager.Instance.PlayAmbient(CurrentWeatherSO?.SfxKey);
		}

		private void SpawnCurrentVisual()
		{
			if (CurrentWeatherSO == null)
				return;

			GameObject prefab = CurrentWeatherSO.VisualPrefab;
			if (prefab == null)
				return;

			// WeatherDirector child 로 attach — Singleton 이 dontDestroyOnLoad 라 visual 도 자동 보존.
			currentVisualInstance = Instantiate(prefab, transform);
			currentVisualInstance.name = $"{prefab.name}_Active";

			currentParticleSystem = currentVisualInstance.GetComponent<ParticleSystem>();
			currentParticleRenderer = currentVisualInstance.GetComponent<ParticleSystemRenderer>();

			ApplyCurrentSOToParticle();
		}

		// SO 노브 → ParticleSystem 모듈 set. 매 frame Update + spawn 직후 호출.
		private void ApplyCurrentSOToParticle()
		{
			if (currentParticleSystem == null || CurrentWeatherSO == null)
				return;

			ParticleSystem.MainModule main = currentParticleSystem.main;
			main.startColor = CurrentWeatherSO.ParticleColor;
			main.startSize = CurrentWeatherSO.ParticleStartSize;
			main.startSpeed = CurrentWeatherSO.ParticleStartSpeed;
			main.startLifetime = CurrentWeatherSO.ParticleStartLifetime;
			main.gravityModifier = CurrentWeatherSO.ParticleGravityModifier;

			ParticleSystem.EmissionModule emission = currentParticleSystem.emission;
			emission.rateOverTime = CurrentWeatherSO.ParticleEmissionRate;

			ParticleSystem.ShapeModule shape = currentParticleSystem.shape;
			shape.shapeType = CurrentWeatherSO.ParticleShapeType;
			shape.scale = CurrentWeatherSO.ParticleShapeScale;
			shape.position = CurrentWeatherSO.ParticleShapePosition;
			shape.rotation = CurrentWeatherSO.ParticleShapeRotation;
			shape.radius = CurrentWeatherSO.ParticleShapeRadius;

			if (currentParticleRenderer != null)
				currentParticleRenderer.sharedMaterial = CurrentWeatherSO.ParticleMaterial;
		}

		// E3: IsWet 상태에 따라 _Wetness 0↔1 lerp → Shader.SetGlobalFloat broadcast.
		private void ApplyWetnessGlobal()
		{
			float target = GetIsWet() ? 1f : 0f;
			currentWetness = Mathf.MoveTowards(currentWetness, target, wetnessLerpSpeed * Time.deltaTime);
			Shader.SetGlobalFloat(WetnessId, currentWetness);
		}

		private void DestroyCurrentVisual()
		{
			if (currentVisualInstance == null)
				return;

			Destroy(currentVisualInstance);
			currentVisualInstance = null;
			currentParticleSystem = null;
			currentParticleRenderer = null;
		}

		// Resources/Weather/{type}.asset 직접 로드 (캐싱 X — SO 값 런타임 변경 시 즉시 반영).
		public WeatherSO ResolveSO(WeatherType type) => Resources.Load<WeatherSO>($"Weather/{type}");

		private string GetSfxKey() => CurrentWeatherSO != null ? CurrentWeatherSO.SfxKey : "(none)";
		private bool GetIsWet() => CurrentWeatherSO != null && CurrentWeatherSO.IsWet;
	}
}
