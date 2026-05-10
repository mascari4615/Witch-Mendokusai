using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;
using VContainer.Unity;

namespace WitchMendokusai
{
	[CreateAssetMenu(fileName = nameof(BootstrapSettings), menuName = "WM/BootstrapSettings")]
	public class BootstrapSettings : ScriptableObject
	{
		[field: Header("_" + nameof(BootstrapSettings))]
		[field: SerializeField] public bool UseBootstrap { get; private set; } = true;
	}

	public static class Bootstrap
	{
		private const string WORLD_SCENE_NAME = "World";

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
		public static void OnBooting()
		{
			Debug.Log("SANS");

			BootstrapSettings bootstrapStuff = Resources.Load<BootstrapSettings>(nameof(BootstrapSettings));
			if (bootstrapStuff == null)
			{
				Debug.LogError("BootStrapSettings not found");
				return;
			}

			// RootLifetimeScope spawn 강제 — caller 매니저 Awake (씬 안 GameObject + 아래 lazy-load)
			// 가 EventBusBridge 호출 시 Container ready 보장. VContainerSettings.OnFirstSceneLoaded
			// (AfterSceneLoad) 보다 먼저 spawn 해야 race 0. (TASK-WM-078 γ EventBus)
			if (VContainerSettings.Instance == null)
			{
				VContainerSettings.LoadInstanceFromPreloadAssets();
			}
			LifetimeScope rootScope = VContainerSettings.Instance.GetOrCreateRootLifetimeScopeInstance();
			// VContainer Lifetime.Singleton = lazy spawn — Resolve 강제로 EventBus instance + Awake + Bridge.Register 트리거.
			// 이게 없으면 caller Awake 시 EventBusBridge.instance == null → NPE.
			rootScope.Container.Resolve<IEventBus>();

			// γ P1 leaf 매니저 13 eager spawn (TASK-WM-078 P1, 2026-05-11)
			rootScope.Container.Resolve<AudioManager>();
			rootScope.Container.Resolve<ShaderPackManager>();
			rootScope.Container.Resolve<SkyDirector>();
			rootScope.Container.Resolve<GameEventManager>();
			rootScope.Container.Resolve<HoldingManager>();
			rootScope.Container.Resolve<InputManager>();
			rootScope.Container.Resolve<ObjectPoolManager>();
			rootScope.Container.Resolve<UnitStatCalculator>();
			rootScope.Container.Resolve<CodexPreviewController>();
			rootScope.Container.Resolve<WorldClock>();
			rootScope.Container.Resolve<PlayerProvider>();
			rootScope.Container.Resolve<TimeManager>();
			rootScope.Container.Resolve<WeatherSystem>();

			// γ P2 root 매니저 7 eager spawn (TASK-WM-078 P2, 2026-05-11)
			// GameManager / WeatherDirector lazy-load 폐기 — Container spawn 책임.
			// DataManager Object.Instantiate(DataManagerPrefab) 폐기 — Container 가 Resources/Singletons/DataManager.prefab spawn.
			rootScope.Container.Resolve<WindowManager>();
			rootScope.Container.Resolve<DataLoader>();
			rootScope.Container.Resolve<TooltipController>();
			rootScope.Container.Resolve<DataManager>();
			rootScope.Container.Resolve<WeatherDirector>();
			rootScope.Container.Resolve<GameManager>();
			rootScope.Container.Resolve<UIRoot>();

			// InputStrategySelector 가 SceneManager.sceneLoaded 구독 → 씬별 InputStrategy 설정
			// (World→InputStrategyWorld, Lobby→InputStrategyLobby). Resources prefab 없으므로
			// 직접 GameObject + AddComponent. 첫 sceneLoaded 이벤트 잡으려면 BeforeSceneLoad 시점 등록 필수.
			// (P3 후속 — Container 로 마이그)
			GameObject inputStrategySelectorGo = new GameObject(nameof(InputStrategySelector));
			inputStrategySelectorGo.AddComponent<InputStrategySelector>();
			Object.DontDestroyOnLoad(inputStrategySelectorGo);

			// ζ World 씬 한정 4 매니저 (StageManager / DevWindowController / CodexWindowController / DungeonManager)
			// — SceneLifetimeScope 자율 spawn (TASK-WM-078 ζ, 2026-05-11). World 씬 진입 시 GameObject 생성
			// → 4 매니저 Lifetime.Scoped spawn. 씬 unload 시 자동 dispose → 4 매니저 destroy.
			SceneManager.sceneLoaded -= OnSceneLoaded;
			SceneManager.sceneLoaded += OnSceneLoaded;
		}

		private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
		{
			if (scene.name != WORLD_SCENE_NAME)
				return;

			GameObject sceneLifetimeScopeGo = new GameObject(nameof(SceneLifetimeScope));
			SceneManager.MoveGameObjectToScene(sceneLifetimeScopeGo, scene);
			sceneLifetimeScopeGo.AddComponent<SceneLifetimeScope>();
		}
	}
}
