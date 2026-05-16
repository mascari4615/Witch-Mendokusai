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
			SOManagerBridge.Register(soManager);

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

			// TASK-WM-107 Slice 2A — POCO Effect dispatch DI 진입점 (static Effect.ApplyEffect 우회 대체).
			builder.Register<EffectRunner>(Lifetime.Singleton).As<IEffectRunner>();

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
				// TASK-WM-118 I1 — 손-순서 eager 리스트를 BootGuard 경유: 순서가 진짜
				// [Inject] 위상과 어긋나거나 dep 미해결/순환이면 *부팅 시점에 타입 귀속
				// 명시 차단* (조용한 NRE 가 게임플레이까지 잠복 X). 성공 시 동작 무변경.
				BootGuard.EagerResolve<AudioManager>(container, "Root");
				BootGuard.EagerResolve<ShaderPackManager>(container, "Root");
				BootGuard.EagerResolve<SkyDirector>(container, "Root");
				BootGuard.EagerResolve<GameEventManager>(container, "Root");
				BootGuard.EagerResolve<HoldingManager>(container, "Root");
				BootGuard.EagerResolve<InputManager>(container, "Root");
				BootGuard.EagerResolve<ObjectPoolManager>(container, "Root");
				BootGuard.EagerResolve<UnitStatCalculator>(container, "Root");
				BootGuard.EagerResolve<CodexPreviewController>(container, "Root");
				BootGuard.EagerResolve<WorldClock>(container, "Root");
				BootGuard.EagerResolve<PlayerProvider>(container, "Root");
				BootGuard.EagerResolve<TimeManager>(container, "Root");
				BootGuard.EagerResolve<WeatherSystem>(container, "Root");
				BootGuard.EagerResolve<WindowManager>(container, "Root");
				BootGuard.EagerResolve<DataLoader>(container, "Root");
				BootGuard.EagerResolve<TooltipController>(container, "Root");
				BootGuard.EagerResolve<DataManager>(container, "Root");
				BootGuard.EagerResolve<WeatherDirector>(container, "Root");
				BootGuard.EagerResolve<GameManager>(container, "Root");
				BootGuard.EagerResolve<UIRoot>(container, "Root");
				BootGuard.EagerResolve<InputStrategySelector>(container, "Root");
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
