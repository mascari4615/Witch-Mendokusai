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
		public const string BOSS_EVENT = "boss:0";
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
		private int lastBossKills;

		[Inject]
		public void Construct(DataManager dataManager, SOManager soManager, GameEventManager gameEventManager, IObjectResolver container)
		{
			this.dataManager = dataManager;
			this.soManager = soManager;
			this.gameEventManager = gameEventManager;
			this.container = container;
		}

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
			Sim = new HomesteadSim(LoadCatalog(), dataManager.FeatureSave<HomesteadSaveSlice>().State,
				new HomesteadPurse(soManager.ItemInventory, dataManager.GameStat), new System.Random());
			Sim.RefreshRequests();
			lastBossKills = (int)dataManager.GameStat[GameStatType.TOTAL_BOSS_KILL];
			gameEventManager.RegisterCallback(GameEventType.OnDungeonReturn, OnDungeonReturn);
			Dresser = new HomesteadCostumeDresser(Sim);
		}

		private void OnDestroy()
		{
			if (gameEventManager != null)
				gameEventManager.UnregisterCallback(GameEventType.OnDungeonReturn, OnDungeonReturn);
			Dresser?.Dispose();
			if (Panels != null)
				Panels.Dispose();
			if (Hud != null)
				Destroy(Hud);
			if (Instance == this)
				Instance = null;
		}

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
			if (WorldClock.TryGetExistingInstance(out WorldClock clock) == false)
				return;
			long now = ClockMinutes(clock);
			if (lastClockMinutes >= 0)
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

		// 아침까지 쉼. 하늘의 시계를 밀 수 있으면 밀고 (분은 Update 가 따라감), 세계가 시각을 주는 동안에는 생활 시간만 밂
		public void RestUntilMorning()
		{
			int minutes = Sim.MinutesUntilMorning(Hour, Minute);
			if (WorldClock.TryGetExistingInstance(out WorldClock clock) && clock.AdvanceMinutes(minutes))
				Update();
			else
				Sim.AdvanceMinutes(minutes);
			Sim.Rest();
			Save();
		}

		public void Save() => dataManager.SaveManager.SaveData();

		private void OnDungeonReturn()
		{
			Sim.NotifyExpedition();
			int kills = (int)dataManager.GameStat[GameStatType.TOTAL_BOSS_KILL];
			if (kills > lastBossKills)
				Sim.NotifyEvent(BOSS_EVENT);
			lastBossKills = kills;
			Sim.RefreshRequests();
			Save();
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
