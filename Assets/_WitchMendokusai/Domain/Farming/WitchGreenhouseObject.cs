using System.Collections.Generic;
using UnityEngine;
using WitchMendokusai.DomainSDK.Farming;

namespace WitchMendokusai
{
	// 마도 온실 씬 컴포넌트 — 순수 Greenhouse(여러 칸 + 인형 자동돌봄 안전망)를 게임 시간에 잇는 얇은 래퍼.
	// 무거운 의존 0(RequireComponent X, abstract 의존 X) → EditMode 에서 new GameObject + AddComponent +
	// TickDay 로 직접 behavior 검증 가능(D 세션 [[wm-monobehaviour-editmode-decouple]] 패턴).
	//
	// ★ 씬 드롭 = 즉시 동작: 빈 GameObject 에 이 컴포넌트만 붙이고 Play → Start 가 스스로 칸 생성+작물 심기
	//   +placeholder 큐브(색=phase)+인형 carer 기본값. demoTick 으로 WorldClock 없어도 N초마다 자라고 색 변함
	//   (눈에 보임). WorldClock 있으면 OnDayChanged 도 같이 구동.
	//
	// ⚠ SerializeField 직렬화 함정 방어: 인스펙터로 AddComponent 하면 모든 [SerializeField] 가 직렬화
	//   디폴트(0/false/null)로 덮인다(이니셜라이저 무시 — WitchPlantSO 0값 버그와 동일). 그래서 bool 자동
	//   플래그를 두지 않고 Start 가 *항상* 자립(칸 0개일 때만) + 모든 수치 0이면 기본값 자가보정 → 이미 붙은
	//   컴포넌트도 Play 만 다시 누르면 동작(소급). Reset() = 신규 추가 시 인스펙터 디폴트 보강.
	public sealed class WitchGreenhouseObject : MonoBehaviour
	{
		private const int DEFAULT_MINUTES_PER_DAY = 30;
		private const int DEFAULT_PLOT_COUNT = 4;
		private const float DEFAULT_SPACING = 1.5f;
		private const int DEFAULT_CARER_COUNT = 2;
		private const float DEFAULT_DEMO_TICK = 2f;

		// 한 틱(하루)에 흐르는 게임 분(성장·시듦 진행량). SO 캐싱 X(수치노출 룰).
		[SerializeField, Min(1)] private int minutesPerDay = DEFAULT_MINUTES_PER_DAY;

		[Header("자립 데모 (씬 드롭 시 자동) — 실 배선 전 placeholder")]
		[SerializeField, Min(1)] private int autoPlotCount = DEFAULT_PLOT_COUNT;
		// 빈 칸 간격(placeholder 큐브 배치 m). 실 밭 레이아웃 = Grey Box.
		[SerializeField, Min(0.1f)] private float autoPlotSpacing = DEFAULT_SPACING;
		// 자립 시 심을 마도작물. null = 런타임 기본작물(ApplyDefaults) 생성 → asset 없이도 동작.
		[SerializeField] private WitchPlantSO samplePlant;
		// 자립 시 인형 carer 수(매 틱 이만큼 자동돌봄). 실 인형 풀 연결 전 placeholder.
		[SerializeField, Min(0)] private int autoCarerCount = DEFAULT_CARER_COUNT;
		// 데모 틱 간격(초). 이 주기마다 TickDay = WorldClock 없어도 눈에 보임. <=0 면 기본값 자가보정.
		[SerializeField] private float demoTickSeconds = DEFAULT_DEMO_TICK;

		private readonly Greenhouse greenhouse = new();
		private readonly Dictionary<int, GameObject> plotVisuals = new();
		// 칸별 IInteractable 래퍼 — Fourth 가 클릭(Z키)해 관찰·수확하는 씬 진입점(plotId → 그 칸 오브젝트).
		private readonly Dictionary<int, WitchGreenhousePlotObject> plotObjects = new();
		// placeholder 큐브 색 = MaterialPropertyBlock(에디트 모드 material 인스턴스화 경고·런타임 누수 방지). URP = _BaseColor.
		private static readonly int BASE_COLOR_ID = Shader.PropertyToID("_BaseColor");
		private static readonly int COLOR_ID = Shader.PropertyToID("_Color");
		private MaterialPropertyBlock colorBlock;

		// 이번 틱에 돌볼 인형 id 들을 주는 콜백(인형 풀=상위 소유). null/빈 = 돌봄 0(전부 시간만).
		private System.Func<IReadOnlyList<int>> carerProvider;
		private WorldClock clock;
		private float demoAccumulator;
		private bool selfWired;

		// 개화·시듦이 일어난 칸을 상위에 알림(이벤트 발행 표면 — 연출은 구독자 몫). 초기값 = NRE 방지.
		public System.Action<int> OnPlotBloomed = delegate { };
		public System.Action<int> OnPlotWithered = delegate { };
		// 관찰된 개체가 수확돼 영구 표본으로 「진짜」가 됐다(plotId, plantDataId). Codex 박물관·연출 구독 표면.
		public System.Action<int, int> OnPlotBecameSpecimen = delegate { };

		public Greenhouse Model => greenhouse;
		public int MinutesPerDay => minutesPerDay;

		// 인스펙터 컴포넌트 추가/우클릭 Reset 시 호출 — 직렬화 디폴트(0/null) 덮어쓰기 방지.
		private void Reset()
		{
			minutesPerDay = DEFAULT_MINUTES_PER_DAY;
			autoPlotCount = DEFAULT_PLOT_COUNT;
			autoPlotSpacing = DEFAULT_SPACING;
			autoCarerCount = DEFAULT_CARER_COUNT;
			demoTickSeconds = DEFAULT_DEMO_TICK;
		}

		// 상태 주입(틱 소스 없이도 검증 가능하게 분리 — D 패턴). carer 풀 콜백 등록.
		public void Initialize(System.Func<IReadOnlyList<int>> carerProvider)
		{
			this.carerProvider = carerProvider;
		}

		// 씬 드롭 자립 — Start 자동. 칸 0개면 스스로 구축(외부 Initialize/BuildSelfContained 와 공존).
		private void Start()
		{
			CoerceDefaults();

			// 이미 외부에서 칸을 채웠으면(테스트/실배선) 자동 구축 skip — 중복 방지.
			if (greenhouse.PlotCount == 0)
			{
				BuildSelfContained(autoPlotCount, ResolvePlant(), withVisuals: true);
				selfWired = true;

				if (carerProvider == null)
				{
					int carers = autoCarerCount;
					carerProvider = () => BuildCarerIds(carers);
				}

				if (clock == null && WorldClock.Instance != null)
				{
					AttachClock(WorldClock.Instance);
				}

				Debug.Log($"[WitchGreenhouse] 자립 구축: {greenhouse.PlotCount}칸 / carer {autoCarerCount} / demoTick {demoTickSeconds}s / WorldClock {(clock == null ? "없음(데모틱만)" : "구독")}", this);
			}
		}

		// 직렬화 디폴트(0) 자가보정 — AddComponent 함정 소급 방어. public = 에디트 모드 드라이버(Sandbox)도
		// Start 없이 호출해 수치를 보장(WM-177). 멱등 — 0 이 아니면 no-op.
		public void CoerceDefaults()
		{
			if (minutesPerDay <= 0)
			{
				minutesPerDay = DEFAULT_MINUTES_PER_DAY;
			}

			if (autoPlotCount <= 0)
			{
				autoPlotCount = DEFAULT_PLOT_COUNT;
			}

			if (autoPlotSpacing <= 0f)
			{
				autoPlotSpacing = DEFAULT_SPACING;
			}

			if (demoTickSeconds <= 0f)
			{
				demoTickSeconds = DEFAULT_DEMO_TICK;
			}
		}

		// 데모 틱 — WorldClock 없어도 demoTickSeconds 마다 하루 진행(눈에 보이는 성장·시듦). 자립 시만.
		private void Update()
		{
			if (selfWired == false)
			{
				return;
			}

			demoAccumulator += Time.deltaTime;
			if (demoAccumulator >= demoTickSeconds)
			{
				demoAccumulator = 0f;
				TickDay();
			}
		}

		// 자립 작물 결정 — ① 인스펙터 samplePlant ② 등록된 마도 식물 종(SOManager) ③ 런타임 기본작물.
		// ②가 핵심: 수확이 기록하는 종 ID(DataManager.SpecimenCollected)가 Codex(박물관)에 나열되는 *등록된*
		// WitchPlantSO 의 ID 와 일치해야 「봐줘야 진짜 → 영구 표본」이 도감에 보인다. 런타임 throwaway 는
		// SOManager 미등록이라 도감에 안 떠서 "수확했는데 도감 비었다" 가 됨(루프 단절). 등록 종이 0개이거나
		// 부트 전(EditMode)일 때만 ③ 런타임 폴백 — asset 없이도 자립 데모는 굴러간다.
		private WitchPlantSO ResolvePlant()
		{
			if (samplePlant != null)
			{
				return samplePlant;
			}

			if (SOManagerBridge.HasInstance
				&& SOManagerBridge.DataSOs.TryGetValue(typeof(WitchPlantSO), out Dictionary<int, DataSO> plants))
			{
				foreach (DataSO dataSO in plants.Values)
				{
					if (dataSO is WitchPlantSO registered)
					{
						return registered;
					}
				}
			}

			// 등록 종 0개. Play 중(부트 후)인데도 없으면 = 도감 표본이 안 뜨는 *무음 실패*(사용자가 밟은 그 버그)의
			// 재발 — 무음 폴백으로 덮지 말고 큰 소리로 알린다(FastFail / No-news is bad-news). 종 asset 삭제·등록 깨짐
			// 같은 회귀를 에러 없이 흘려보내지 않게. EditMode·부트 전(SOManager 미로드)은 정상이라 경고 X.
			if (Application.isPlaying)
			{
				Debug.LogWarning("[WitchGreenhouse] 등록된 WitchPlantSO 종이 0개 — 임시 종으로 자립하지만 도감(Codex) 표본엔 안 뜬다. "
					+ "WM/Farming/Ensure Sample Plant 실행 또는 WitchPlantSO .asset 을 Addressable(label WitchPlantSO)로 등록할 것.", this);
			}

			WitchPlantSO runtimePlant = ScriptableObject.CreateInstance<WitchPlantSO>();
			runtimePlant.ApplyDefaults();
			return runtimePlant;
		}

		private static IReadOnlyList<int> BuildCarerIds(int count)
		{
			List<int> ids = new(count);
			for (int index = 0; index < count; index++)
			{
				ids.Add(index);
			}

			return ids;
		}

		// ★ 자립 구축(EditMode 검증 진입점) — plotCount 칸 생성 + plant 심기 (+선택 placeholder 큐브).
		// withVisuals=false 면 GameObject 생성 0 = 순수 로직만(테스트용).
		public void BuildSelfContained(int plotCount, WitchPlantSO plant, bool withVisuals)
		{
			for (int plotId = 0; plotId < plotCount; plotId++)
			{
				greenhouse.AddPlot(plotId).Plant(plant.ID, plant.ToGrowthParams(), plant.StartVitality);

				if (withVisuals)
				{
					SpawnPlaceholderVisual(plotId, plant);
				}
			}

			RefreshVisuals();
		}

		// 칸마다 다른 작물을 심는 자립 구축(혼합 정원 — 코지/마도 대비 데모용, WM-177). plants[i] = 칸 i 작물.
		public void BuildSelfContained(IReadOnlyList<WitchPlantSO> plants, bool withVisuals)
		{
			for (int plotId = 0; plotId < plants.Count; plotId++)
			{
				WitchPlantSO plant = plants[plotId];
				greenhouse.AddPlot(plotId).Plant(plant.ID, plant.ToGrowthParams(), plant.StartVitality);

				if (withVisuals)
				{
					SpawnPlaceholderVisual(plotId, plant);
				}
			}

			RefreshVisuals();
		}

		// 마도작물을 한 칸에 심는다(SO 수치 → 성장 파라미터). 빈 칸 없으면 먼저 AddPlot.
		public bool Plant(int plotId, WitchPlantSO plant)
		{
			if (plant == null)
			{
				return false;
			}

			GreenhousePlot plot = greenhouse.GetPlot(plotId) ?? greenhouse.AddPlot(plotId);
			return plot.Plant(plant.ID, plant.ToGrowthParams(), plant.StartVitality);
		}

		// ★ Fourth(플레이어) 관찰 = 「진짜화」 — 그 칸을 봐줬다고 표시(시들기 전이면 영구 표본 자격) + 즉시 gold
		// 시각 갱신. 인형 자동돌봄(TickWithCarers)은 살리지만 진짜로 만들진 못함 — 진짜화는 오직 이 입력.
		// 칸별 IInteractable(WitchGreenhousePlotObject) 또는 데모가 호출. 빈/시든 칸엔 무효(false).
		public bool Observe(int plotId)
		{
			GreenhousePlot plot = greenhouse.GetPlot(plotId);
			if (plot == null || plot.Phase == PlotPhase.Empty || plot.Phase == PlotPhase.Withered)
			{
				return false;
			}

			plot.Observe();
			RefreshVisuals();
			return true;
		}

		// 지금 「진짜화」 자격(관찰+개화+안시듦)을 갖춘 칸 수 — "봐준 것만 진짜" 집계(Codex 표본 후보).
		public int SpecimenCount => greenhouse.SpecimenCount();

		// ★ 개화한 칸을 수확한다(Fourth 입력 또는 시스템). 성공 시 칸을 비우고 시각 갱신. 관찰된 개체(IsSpecimen)면
		// 영구 표본으로 「진짜」가 된 것 — OnPlotBecameSpecimen 발행 + DataManager.SpecimenCollected 에 영구 기록
		// (수확해 사라져도 도감엔 영영 남는다). DataManager 미존재(EditMode·부트 전)면 이벤트만 — 기록은 best-effort.
		// 빈/개화 전/시듦 칸이면 거부(false).
		public bool Harvest(int plotId)
		{
			GreenhousePlot plot = greenhouse.GetPlot(plotId);
			if (plot == null || plot.TryHarvest(out HarvestResult result) == false)
			{
				return false;
			}

			GrantHarvestItem(result);

			if (result.IsSpecimen)
			{
				HandleSpecimen(plotId, result.PlantDataId);
			}
			else
			{
				RefreshVisuals();
			}

			return true;
		}

		// 수확물을 플레이어 인벤토리에 넣는다 — 변이(누가 길렀나=DominantCarerId) 반영. 종 SO 의 HarvestLoots/CarerLoots 기반.
		// DomainSDK 순수 plot 은 ItemData/Inventory 를 모르므로 Domain 측(여기)이 책임(단방향 정합). 부트 전·종 미등록·
		// ItemInventory 미등록·수확물 표 빔 = 무음 skip(데모/EditMode 안전 — Inventory 는 런타임 부트 후에만 존재).
		private void GrantHarvestItem(HarvestResult result)
		{
			WitchPlantSO plant = PlantById(result.PlantDataId);
			if (plant == null)
			{
				return;
			}

			ItemData loot = plant.ResolveHarvestItem(result.HasDominantCarer, result.DominantCarerId);
			if (loot != null && SOManagerBridge.HasInstance && SOManagerBridge.ItemInventory != null)
			{
				SOManagerBridge.ItemInventory.Add(loot, 1);
			}
		}

		// 수확된 plotDataId 의 종 SO 조회 — 수확물·변이 데이터 소유자. 등록 종 우선, 미등록(런타임 데모종)이면 samplePlant.
		private WitchPlantSO PlantById(int plantDataId)
		{
			if (SOManagerBridge.HasInstance
				&& SOManagerBridge.DataSOs.TryGetValue(typeof(WitchPlantSO), out Dictionary<int, DataSO> plants)
				&& plants.TryGetValue(plantDataId, out DataSO dataSO)
				&& dataSO is WitchPlantSO registered)
			{
				return registered;
			}

			return samplePlant;
		}

		// 표본 「진짜화」 영구 기록 — Harvest(시스템) + WitchGreenhousePlotObject(플레이어 클릭) 공통 경로(DRY).
		// 도감(DataManager.SpecimenCollected)에 박고 상위 알림 + 시각 갱신. 부트 전(EditMode 등)엔 이벤트만.
		private void HandleSpecimen(int plotId, int plantDataId)
		{
			OnPlotBecameSpecimen.Invoke(plotId, plantDataId);

			if (DataManager.TryGetExistingInstance(out DataManager dataManager))
			{
				dataManager.SpecimenCollected[plantDataId] = true;
			}

			RefreshVisuals();
		}

		// 칸별 IInteractable 오브젝트 접근(상위 배선·쿼리·테스트). 미생성(withVisuals=false)이면 null.
		public WitchGreenhousePlotObject GetPlotObject(int plotId)
		{
			return plotObjects.TryGetValue(plotId, out WitchGreenhousePlotObject plotObject) ? plotObject : null;
		}

		// ★ 핵심 public 진입점(틱 소스 무관 — EditMode 직접 호출). 하루치 시간 경과 + 인형 자동돌봄 +
		// 개화/시듦 전이 이벤트 발행. WorldClock 없이도 이 메서드로 한 사이클을 결정적으로 검증.
		public void TickDay()
		{
			IReadOnlyList<int> carers = carerProvider == null ? null : carerProvider();

			// 전이 감지를 위해 틱 전 phase 스냅샷.
			Dictionary<int, PlotPhase> before = new();
			foreach (KeyValuePair<int, GreenhousePlot> entry in greenhouse.Plots)
			{
				before[entry.Key] = entry.Value.Phase;
			}

			greenhouse.TickWithCarers(carers, minutesPerDay);

			foreach (KeyValuePair<int, GreenhousePlot> entry in greenhouse.Plots)
			{
				PlotPhase now = entry.Value.Phase;
				if (before.TryGetValue(entry.Key, out PlotPhase was) == false || was == now)
				{
					continue;
				}

				if (now == PlotPhase.Bloomed)
				{
					OnPlotBloomed.Invoke(entry.Key);
				}
				else if (now == PlotPhase.Withered)
				{
					OnPlotWithered.Invoke(entry.Key);
				}
			}

			RefreshVisuals();
		}

		private void HandleDayChanged(int day)
		{
			TickDay();
		}

		// 틱 소스(WorldClock) 구독 분리 — 클럭 없이도 Initialize+TickDay 로 상태 검증 가능.
		public void AttachClock(WorldClock worldClock)
		{
			DetachClock();
			clock = worldClock;
			if (clock != null)
			{
				clock.OnDayChanged += HandleDayChanged;
			}
		}

		public void DetachClock()
		{
			if (clock != null)
			{
				clock.OnDayChanged -= HandleDayChanged;
				clock = null;
			}
		}

		// 빈 칸 추가(상위 배선용 — 씬의 밭 한 칸 = 한 plotId).
		public GreenhousePlot AddPlot(int plotId)
		{
			return greenhouse.AddPlot(plotId);
		}

		// placeholder 큐브 1개 생성(한 칸) + 클릭 가능하게 배선. 실 모델 = Grey Box. 색은 RefreshVisuals 가 phase 로.
		private void SpawnPlaceholderVisual(int plotId, WitchPlantSO plant)
		{
			if (plotVisuals.ContainsKey(plotId))
			{
				return;
			}

			GameObject cube = CombatPrimitive.Create(PrimitiveType.Cube);
			cube.name = $"Plot_{plotId}";
			cube.transform.SetParent(transform, worldPositionStays: false);
			cube.transform.localPosition = new Vector3(plotId * autoPlotSpacing, 0f, 0f);

			// 상호작용은 거리 기반(InteractiveObject.GetNearest 1.5f) — 물리 충돌 불요. placeholder 콜라이더 제거.
			Collider primitiveCollider = cube.GetComponent<Collider>();
			if (primitiveCollider != null)
			{
				if (Application.isPlaying)
				{
					Destroy(primitiveCollider);
				}
				else
				{
					DestroyImmediate(primitiveCollider);
				}
			}

			WireInteractable(cube, plotId, plant);
			plotVisuals[plotId] = cube;
		}

		// 칸 GameObject 를 Fourth 클릭 대상으로 배선. WitchGreenhousePlotObject(IInteractable — phase별 동사:
		// Empty=심기/Growing=관찰/Bloomed=수확/Withered=치움) + InteractiveObject(PlayerInteraction 이 1.5f 내 탐색→OnInteract).
		// 칸과 같은 GreenhousePlot 을 공유(인형 자동돌봄과 동일 모델) — 칸 이벤트를 온실로 끌어올려 시각·표본 영구화.
		private void WireInteractable(GameObject cube, int plotId, WitchPlantSO plant)
		{
			// WitchGreenhousePlotObject 를 먼저 붙여야 InteractiveObject.Awake 의 GetComponents<IInteractable>() 가 잡는다.
			WitchGreenhousePlotObject plotObject = cube.AddComponent<WitchGreenhousePlotObject>();
			plotObject.Bind(plotId, greenhouse.GetPlot(plotId));
			plotObject.SetPlant(plant); // 수확 후 빈 칸 재심기용

			plotObject.OnObserved += _ => RefreshVisuals();
			plotObject.OnHarvested += _ => RefreshVisuals();
			plotObject.OnBecameSpecimen += specimen => HandleSpecimen(specimen.FieldId, specimen.PlantDataId);

			cube.AddComponent<InteractiveObject>();
			plotObjects[plotId] = plotObject;
		}

		// 칸 phase 로 placeholder 큐브 색 갱신(Growing 초록 / Bloomed 노랑 / Withered 갈색 / Empty 회색).
		// 시각=placeholder(사용자 비전 아님). visual 없으면(EditMode) no-op.
		private void RefreshVisuals()
		{
			if (plotVisuals.Count == 0)
			{
				return;
			}

			if (colorBlock == null)
			{
				colorBlock = new MaterialPropertyBlock();
			}

			foreach (KeyValuePair<int, GameObject> entry in plotVisuals)
			{
				GreenhousePlot plot = greenhouse.GetPlot(entry.Key);
				Renderer renderer = entry.Value == null ? null : entry.Value.GetComponent<Renderer>();
				if (plot == null || renderer == null)
				{
					continue;
				}

				Color color = ColorFor(plot.Phase, plot.Observed);
				renderer.GetPropertyBlock(colorBlock);
				colorBlock.SetColor(BASE_COLOR_ID, color);
				colorBlock.SetColor(COLOR_ID, color);
				renderer.SetPropertyBlock(colorBlock);
			}
		}

		// phase 색 + 「봐줘야 진짜」 시각: 관찰된(witnessed) 살아있는 칸은 gold 로 띄워 "이건 진짜가 됐다"를
		// 즉시 보여준다 — 개화한 관찰칸 = 밝은 금색(영구 표본), 자라는 관찰칸 = 금빛 green(증언 진행 중).
		// 안 봐준 칸은 평범한 green/yellow. 시듦/빈 칸은 관찰 무관(brown/grey).
		private static Color ColorFor(PlotPhase phase, bool observed)
		{
			switch (phase)
			{
				case PlotPhase.Growing: return observed ? new Color(0.6f, 0.85f, 0.35f) : new Color(0.4f, 0.8f, 0.4f);
				case PlotPhase.Bloomed: return observed ? new Color(1f, 0.84f, 0.25f) : new Color(0.78f, 0.7f, 0.32f);
				case PlotPhase.Withered: return new Color(0.45f, 0.32f, 0.2f);
				default: return new Color(0.6f, 0.6f, 0.6f);
			}
		}

		private void OnDestroy()
		{
			DetachClock();
		}
	}
}
