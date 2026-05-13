using MessagePipe;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace WitchMendokusai
{
	public class RootLifetimeScope : LifetimeScope
	{
		protected override void Configure(IContainerBuilder builder)
		{
			builder.RegisterMessagePipe();

			// SOManager — ScriptableObject = RegisterInstance pattern (cross-scene global, TASK-WM-078 γ P2-2, 2026-05-13).
			// Resources.Load 의 lazy singleton ↔ VContainer RegisterInstance 가 같은 SO 가리킴 (caller transitional 0 변경).
			SOManager soManager = Resources.Load<SOManager>(nameof(SOManager));
			builder.RegisterInstance(soManager);

			// γ leaf 매니저 13 등록 (TASK-WM-078 P1, 2026-05-11)
			RegisterLeaf<AudioManager>(builder);
			RegisterLeaf<ShaderPackManager>(builder);
			RegisterLeaf<SkyDirector>(builder);
			RegisterLeaf<GameEventManager>(builder);
			RegisterLeaf<HoldingManager>(builder);
			RegisterLeaf<InputManager>(builder);
			RegisterLeaf<ObjectPoolManager>(builder);
			RegisterLeaf<UnitStatCalculator>(builder);
			RegisterLeaf<CodexPreviewController>(builder);
			RegisterLeaf<WorldClock>(builder);
			RegisterLeaf<PlayerProvider>(builder);
			RegisterLeaf<TimeManager>(builder);
			RegisterLeaf<WeatherSystem>(builder);

			// γ root 매니저 7 등록 (TASK-WM-078 P2, 2026-05-11)
			RegisterLeaf<WindowManager>(builder);
			RegisterLeaf<DataLoader>(builder);
			RegisterLeaf<TooltipController>(builder);
			RegisterLeaf<DataManager>(builder);
			RegisterLeaf<WeatherDirector>(builder);
			RegisterLeaf<GameManager>(builder);
			RegisterLeaf<UIRoot>(builder);

			// γ P3-K POCO 매니저 3 등록 — DataManager 가 소유권 대신 VContainer 소유로 이관 (TASK-WM-078, 2026-05-13).
			// Register<T> = POCO new + VContainer Singleton 관리. 에거 Resolve 불필요 (DataManager.Construct 가 트리거).
			builder.Register<QuestManager>(Lifetime.Singleton);
			builder.Register<WorkManager>(Lifetime.Singleton);
			builder.Register<SaveManager>(Lifetime.Singleton);
			builder.Register<WorldClockViewModel>(Lifetime.Singleton);

			// θ-5a InputStrategySelector — 새 GameObject + AddComponent (코드 spawn 의 VContainer 표준 흡수, TASK-WM-078 θ-5a, 2026-05-11).
			// Bootstrap.OnBooting 의 직접 GameObject 생성 폐기.
			builder.RegisterComponentOnNewGameObject<InputStrategySelector>(Lifetime.Singleton, nameof(InputStrategySelector))
				.DontDestroyOnLoad();

			// θ eager spawn — Bootstrap.OnBooting 의 21 Container.Resolve 명시 호출 흡수 (TASK-WM-078 θ + θ-5a, 2026-05-11).
			// Lifetime.Singleton = lazy default — Resolve 강제로 prefab Instantiate + Awake + raw Instance 셋 트리거.
			// 순서 = caller 의존 정합 (EventBus 우선, 그 다음 leaf 13, 마지막 root 7, InputStrategySelector 끝).
			builder.RegisterBuildCallback(container =>
			{
				GlobalMessagePipe.SetProvider(container.AsServiceProvider());
				container.Resolve<AudioManager>();
				container.Resolve<ShaderPackManager>();
				container.Resolve<SkyDirector>();
				container.Resolve<GameEventManager>();
				container.Resolve<HoldingManager>();
				container.Resolve<InputManager>();
				container.Resolve<ObjectPoolManager>();
				container.Resolve<UnitStatCalculator>();
				container.Resolve<CodexPreviewController>();
				container.Resolve<WorldClock>();
				container.Resolve<PlayerProvider>();
				container.Resolve<TimeManager>();
				container.Resolve<WeatherSystem>();
				container.Resolve<WindowManager>();
				container.Resolve<DataLoader>();
				container.Resolve<TooltipController>();
				container.Resolve<DataManager>();
				container.Resolve<WeatherDirector>();
				container.Resolve<GameManager>();
				container.Resolve<UIRoot>();
				container.Resolve<InputStrategySelector>();
			});
		}

		private static void RegisterLeaf<T>(IContainerBuilder builder) where T : MonoBehaviour
		{
			T prefab = Resources.Load<T>($"Singletons/{typeof(T).Name}");
			builder.RegisterComponentInNewPrefab(prefab, Lifetime.Singleton)
				.DontDestroyOnLoad();
		}
	}
}
