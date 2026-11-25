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
	// - 던전 귀환과 보스 처치를 규칙에 알림
	// - 화면 묶음 (UIHomestead) 과 알림 (HomesteadHud) 을 처음 쓸 때 만듦
	public sealed class HomesteadManager : MonoBehaviour
	{
		public const string BOSS_EVENT = "boss";

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
			lastClockMinutes = -1;
		}

		private void OnDestroy()
		{
			if (gameEventManager != null)
				gameEventManager.UnregisterCallback(GameEventType.OnDungeonReturn, OnDungeonReturn);
			EventBusBridge.Unsubscribe<BossKilledEvent>(OnBossKilled);
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

		private void OnDungeonReturn()
		{
			Sim.NotifyExpedition();
			Sim.RefreshRequests();
			Save();
		}

		private void OnBossKilled(BossKilledEvent killed) => Sim.NotifyEvent(BOSS_EVENT);

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
