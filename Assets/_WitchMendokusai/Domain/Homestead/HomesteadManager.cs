using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using WitchMendokusai.DomainSDK.Homestead;

namespace WitchMendokusai
{
	// 생활 규칙 (HomesteadSim) 과 게임을 잇는 자리
	// - 벽시계가 흐른 만큼 규칙의 시간을 밂. 던전에 있는 동안에도 흐름
	// - 던전 귀환, 보스 처치, 구출을 규칙에 알림. 기다리는 구출 열쇠를 던전에 적어 둠
	// - 화면 묶음 (UIHomestead) 과 알림 (HomesteadHud) 을 처음 쓸 때 만듦
	public sealed class HomesteadManager : MonoBehaviour
	{
		[Tooltip("한 번에 흐른 것으로 볼 최대 분. 넘으면 시각을 새로 맞춘 것")]
		[SerializeField] private int maxStepMinutes = 240;
		private const string RESOURCE_FOLDER = "Homestead";
		private const int MINUTES_PER_HOUR = 60;

		public static HomesteadManager Instance { get; private set; }

		public static bool TryGetExistingInstance(out HomesteadManager mgr)
		{
			mgr = Instance;
			return mgr != null;
		}

		private DataManager dataManager;
		private SOManager soManager;
		private GameEventManager gameEventManager;
		private IObjectResolver container;

		public HomesteadSim Sim { get; private set; }
		public UIHomestead Panels { get; private set; }
		public HomesteadHud Hud { get; private set; }
		public HomesteadCostumeDresser Dresser { get; private set; }

		private long lastClockMinutes = -1;
		// 다음 판에 둘 갇힌 사람. 주민이 오면 다시 셈
		private string rescueWanted = string.Empty;

		[Inject]
		public void Construct(DataManager dataManager, SOManager soManager, GameEventManager gameEventManager, IObjectResolver container)
		{
			this.dataManager = dataManager;
			this.soManager = soManager;
			this.gameEventManager = gameEventManager;
			this.container = container;
		}

		// 세이브가 통째로 바뀌어 (처음부터 다시) 규칙을 새로 세움. 마을이 떠 있으면 몸을 다시 지음
		public event Action Rebuilt = delegate { };

		private HomesteadState SavedState => dataManager.FeatureSave<HomesteadSaveSlice>().State;

		private void Awake()
		{
			if (Instance != null && Instance != this)
			{
				Destroy(gameObject);
				return;
			}
			Instance = this;
		}

		private void Start()
		{
			BuildSim();
			gameEventManager.RegisterCallback(GameEventType.OnDungeonReturn, OnDungeonReturn);
			EventBusBridge.Subscribe<BossKilledEvent>(OnBossKilled);
			EventBusBridge.Subscribe<CaptiveRescuedEvent>(OnCaptiveRescued);
			StageManager.OnStageChanged += OnStageChanged;
		}

		private void BuildSim()
		{
			HomesteadCatalog catalog = LoadCatalog();
			HomesteadState state = SavedState;
			List<string> dropped = HomesteadRepair.DropUnknown(state, catalog);
			if (dropped.Count > 0)
				Debug.LogWarning($"[{nameof(HomesteadManager)}] 자료에 없는 이름을 세이브에서 뺌: {string.Join(", ", dropped)}");
			Sim = new HomesteadSim(catalog, state, new HomesteadPurse(soManager.ItemInventory, dataManager.GameStat), new System.Random());
			Sim.RefreshRequests();
			Dresser = new HomesteadCostumeDresser(Sim);
			Sim.Arrived += _ => RecountRescue();
			RecountRescue();
			lastClockMinutes = -1;
		}

		private void OnDestroy()
		{
			if (gameEventManager != null)
				gameEventManager.UnregisterCallback(GameEventType.OnDungeonReturn, OnDungeonReturn);
			EventBusBridge.Unsubscribe<BossKilledEvent>(OnBossKilled);
			EventBusBridge.Unsubscribe<CaptiveRescuedEvent>(OnCaptiveRescued);
			StageManager.OnStageChanged -= OnStageChanged;
			Dresser?.Dispose();
			DisposeScreens();
			if (Instance == this)
				Instance = null;
		}

		private void DisposeScreens()
		{
			if (Panels != null)
			{
				Panels.Dispose();
				Destroy(Panels.gameObject);
			}
			if (Hud != null)
				Destroy(Hud);
			Panels = null;
			Hud = null;
		}

		// 풀에 남은 스테이지는 옛 월드의 것을 쥐고 있음. 지금 월드 것으로 다시
		public void InjectInto(GameObject target) => container.InjectGameObject(target);

		public static HomesteadCatalog LoadCatalog()
		{
			TextAsset[] files = Resources.LoadAll<TextAsset>(RESOURCE_FOLDER);
			Array.Sort(files, (first, second) => string.CompareOrdinal(first.name, second.name));
			List<string> texts = new();
			foreach (TextAsset file in files)
				texts.Add(file.text);
			return HomesteadCatalogJson.Load(texts);
		}

		private void Update()
		{
			if (SavedState != Sim.State)
			{
				Dresser.Dispose();
				DisposeScreens();
				BuildSim();
				Rebuilt();
			}
			// 던전 관리자는 던전 씬에만 있어 있을 때마다 적음. 판 시작의 배치가 읽음
			if (DungeonManager.TryGetExistingInstance(out DungeonManager dungeonManager))
				dungeonManager.RescueWanted = rescueWanted;
			if (WorldClock.TryGetExistingInstance(out WorldClock clock) == false)
				return;
			long now = ClockMinutes(clock);
			// 세계 시각이 늦게 붙어 크게 건너뛴 것은 흐른 시간이 아님
			if (lastClockMinutes >= 0 && now - lastClockMinutes <= maxStepMinutes)
				Sim.AdvanceMinutes(now - lastClockMinutes);
			lastClockMinutes = now;
		}

		private static long ClockMinutes(WorldClock clock)
		{
			long days = ((long)(clock.Year - 1) * clock.Config.SeasonsPerYear + clock.Season) * clock.Config.DaysPerSeason + (clock.Day - 1);
			return (days * clock.Config.HoursPerDay + clock.Hour) * MINUTES_PER_HOUR + clock.Minute;
		}

		public int Season => WorldClock.TryGetExistingInstance(out WorldClock clock) ? clock.Season : 0;
		public int Hour => WorldClock.TryGetExistingInstance(out WorldClock clock) ? clock.Hour : 0;
		public int Minute => WorldClock.TryGetExistingInstance(out WorldClock clock) ? clock.Minute : 0;

		private long CalendarDay => WorldClock.TryGetExistingInstance(out WorldClock clock) ? ClockMinutes(clock) / Sim.Catalog.Tuning.MinutesPerDay : Sim.Day;

		public bool CanRest => Sim.CanRest(CalendarDay);

		// 생활 시간을 아침까지 밂. 하늘의 시각은 세계가 주므로 그대로
		public HomesteadResult RestUntilMorning()
		{
			HomesteadResult result = Sim.Rest(CalendarDay, Sim.MinutesUntilMorning(Hour, Minute));
			if (result.Ok)
				Save();
			return result;
		}

		public void Save() => dataManager.SaveManager.SaveData();

		// 욘의 집 안과 박물관 안에 들어서면 방을 붙임
		private void OnStageChanged(Stage stage, StageObject stageObject)
		{
			if (stage is WorldStage == false)
				return;
			if (stage.ID == HomeRoom.STAGE_ID)
				HomeRoom.Attach(this, stageObject);
			else if (stage.ID == MuseumRoom.STAGE_ID)
				MuseumRoom.Attach(this, stageObject);
		}

		private void OnDungeonReturn()
		{
			Sim.NotifyExpedition();
			Sim.RefreshRequests();
			Save();
		}

		private void OnBossKilled(BossKilledEvent killed) => Sim.NotifyEvent(HomesteadSim.BOSS_EVENT);

		// 쓰러져 돌아와도 구출은 그대로 (벌 없음). 새로 온 사람은 귀환 소식에 뜸
		private void OnCaptiveRescued(CaptiveRescuedEvent rescued)
		{
			HomesteadResult result = Sim.NotifyRescue(rescued.Key);
			if (result.Ok == false)
				Debug.LogWarning($"[{nameof(HomesteadManager)}] 기다리지 않는 구출 {rescued.Key}: {result.Failure}");
		}

		// 기다리는 구출 중 첫째 하나만. 한 판에 갇힌 사람은 하나
		private void RecountRescue()
		{
			List<string> wanted = Sim.WantedRescues();
			rescueWanted = wanted.Count > 0 ? wanted[0] : string.Empty;
		}

		// 화면은 마을에 처음 들어설 때. 로비와 던전만 오가는 판에는 만들지 않음
		public void EnsureScreens(UIManager uiManager, UIRoot uiRoot)
		{
			if (Hud != null)
				return;
			Hud = uiRoot.gameObject.AddComponent<HomesteadHud>();
			Hud.Build(uiRoot, this);
			GameObject panelObject = new(nameof(UIHomestead));
			panelObject.transform.SetParent(transform, false);
			Panels = panelObject.AddComponent<UIHomestead>();
			container.InjectGameObject(panelObject);
			Panels.Open(this);
		}
	}
}
