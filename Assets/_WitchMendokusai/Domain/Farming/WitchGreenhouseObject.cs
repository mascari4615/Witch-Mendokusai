using System.Collections.Generic;
using UnityEngine;
using WitchMendokusai.DomainSDK.Farming;

namespace WitchMendokusai
{
	// 마도 온실 씬 컴포넌트 — 순수 Greenhouse(여러 칸 + 인형 자동돌봄 안전망)를 게임 시간에 잇는 얇은 래퍼.
	// 무거운 의존 0(RequireComponent X, abstract 의존 X) → EditMode 에서 new GameObject + AddComponent +
	// TickDay 로 직접 behavior 검증 가능(D 세션 [[wm-monobehaviour-editmode-decouple]] 패턴).
	//
	// 역할: ① WorldClock.OnDayChanged 구독 → 매일 ② carerProvider 가 주는 인형 id 들로 자동돌봄 틱
	// (게으른 욘 대신 인형이 살림) ③ 시듦/개화 시 GardenEvents 발행(상위 UI·Codex·마도서 구독).
	// 씬/프리팹/모델/SO 인스턴스 배선 = 사용자 Grey Box. 본 컴포넌트 = 시간 구동 + 이벤트 표면만.
	public sealed class WitchGreenhouseObject : MonoBehaviour
	{
		// 하루에 흐르는 게임 분(성장·시듦 진행량). SO 캐싱 X — 런타임 변경 즉시 반영(수치노출 룰).
		[SerializeField, Min(1)] private int minutesPerDay = 480;

		[Header("자립 배선 (씬 드롭 시 자동) — 실 배선 전 placeholder")]
		[SerializeField] private bool autoWireOnStart = true;
		[SerializeField, Min(1)] private int autoPlotCount = 4;
		// 빈 칸 간격(placeholder 큐브 배치 m). 실 밭 레이아웃 = Grey Box.
		[SerializeField, Min(0.1f)] private float autoPlotSpacing = 1.5f;
		// 자립 시 심을 마도작물. null = 런타임 기본작물(ApplyDefaults) 생성 → asset 없이도 동작.
		[SerializeField] private WitchPlantSO samplePlant;
		// 자립 시 인형 carer 수(매일 이만큼 자동돌봄). 실 인형 풀 연결 전 placeholder.
		[SerializeField, Min(0)] private int autoCarerCount = 2;
		// placeholder 큐브 시각 생성 여부(실 모델 붙이면 false).
		[SerializeField] private bool spawnPlaceholderVisuals = true;

		private readonly Greenhouse greenhouse = new();
		private readonly Dictionary<int, GameObject> plotVisuals = new();

		// 이번 틱에 돌볼 인형 id 들을 주는 콜백(인형 풀=상위 소유). null/빈 = 돌봄 0(전부 시간만).
		private System.Func<IReadOnlyList<int>> carerProvider;
		private WorldClock clock;

		// 개화·시듦이 일어난 칸을 상위에 알림(이벤트 발행 표면 — 연출은 구독자 몫). 초기값 = NRE 방지.
		public System.Action<int> OnPlotBloomed = delegate { };
		public System.Action<int> OnPlotWithered = delegate { };

		public Greenhouse Model => greenhouse;
		public int MinutesPerDay => minutesPerDay;

		// 상태 주입(틱 소스 없이도 검증 가능하게 분리 — D 패턴). carer 풀 콜백 등록.
		public void Initialize(System.Func<IReadOnlyList<int>> carerProvider)
		{
			this.carerProvider = carerProvider;
		}

		// 씬 드롭 자립 — Start 자동 호출. 칸 생성 + 작물 심기 + 인형 carer 기본값 + WorldClock 구독.
		private void Start()
		{
			if (autoWireOnStart == false)
			{
				return;
			}

			BuildSelfContained(autoPlotCount, ResolvePlant(), spawnPlaceholderVisuals);

			if (carerProvider == null)
			{
				int carers = autoCarerCount;
				carerProvider = () => BuildCarerIds(carers);
			}

			if (clock == null && WorldClock.Instance != null)
			{
				AttachClock(WorldClock.Instance);
			}
		}

		// 자립 작물 결정 — samplePlant 있으면 그것, 없으면 런타임 기본작물(asset 없이도 동작).
		private WitchPlantSO ResolvePlant()
		{
			if (samplePlant != null)
			{
				return samplePlant;
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
		// withVisuals=false 면 GameObject 생성 0 = 순수 로직만(테스트용). Start 가 play 에서 visuals=true 로 호출.
		public void BuildSelfContained(int plotCount, WitchPlantSO plant, bool withVisuals)
		{
			for (int plotId = 0; plotId < plotCount; plotId++)
			{
				greenhouse.AddPlot(plotId).Plant(plant.ID, plant.ToGrowthParams(), plant.StartVitality);

				if (withVisuals)
				{
					SpawnPlaceholderVisual(plotId);
				}
			}

			RefreshVisuals();
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

		// placeholder 큐브 1개 생성(한 칸). 실 모델 = Grey Box. 색은 RefreshVisuals 가 phase 로 칠함.
		private void SpawnPlaceholderVisual(int plotId)
		{
			if (plotVisuals.ContainsKey(plotId))
			{
				return;
			}

			GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
			cube.name = $"Plot_{plotId}";
			cube.transform.SetParent(transform, worldPositionStays: false);
			cube.transform.localPosition = new Vector3(plotId * autoPlotSpacing, 0f, 0f);
			plotVisuals[plotId] = cube;
		}

		// 칸 phase 로 placeholder 큐브 색 갱신(Growing 초록 / Bloomed 노랑 / Withered 갈색 / Empty 회색).
		// 시각=placeholder(사용자 비전 아님). visual 없으면(EditMode) no-op.
		private void RefreshVisuals()
		{
			if (plotVisuals.Count == 0)
			{
				return;
			}

			foreach (KeyValuePair<int, GameObject> entry in plotVisuals)
			{
				GreenhousePlot plot = greenhouse.GetPlot(entry.Key);
				Renderer renderer = entry.Value == null ? null : entry.Value.GetComponent<Renderer>();
				if (plot == null || renderer == null)
				{
					continue;
				}

				renderer.material.color = ColorFor(plot.Phase);
			}
		}

		private static Color ColorFor(PlotPhase phase)
		{
			switch (phase)
			{
				case PlotPhase.Growing: return new Color(0.4f, 0.8f, 0.4f);
				case PlotPhase.Bloomed: return new Color(0.95f, 0.85f, 0.3f);
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
