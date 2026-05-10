using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace WitchMendokusai
{
	[CreateAssetMenu(fileName = nameof(BootstrapSettings), menuName = "WM/BootstrapSettings")]
	public class BootstrapSettings : ScriptableObject
	{
		[field: Header("_" + nameof(BootstrapSettings))]
		[field: SerializeField] public DataManager DataManagerPrefab { get; private set; }
		[field: SerializeField] public bool UseBootstrap { get; private set; } = true;
	}

	public static class Bootstrap
	{
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

			// γ leaf 매니저 13 eager spawn — Container.Resolve 강제 (lazy spawn 트리거)
			// caller Awake 가 X.Instance 호출 시 raw Instance accessor 박혀있어야 race 0.
			// (TASK-WM-078 P1, 2026-05-11)
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

			// DataManager 는 leaf 13 안 들어감 (root 매니저, root γ 후속). BootstrapSettings 보존.
			Object.Instantiate(bootstrapStuff.DataManagerPrefab);

			// GameManager.Awake 가 GameConditionBridge.Register / JoystickBridge / WindowLayoutBridge wire 담당.
			// InputManager 가 input 바인딩 시 Bridge 호출하므로 그 전에 GameManager 가 Awake 돼야 함.
			// Singleton<T>.Instance 접근 = Resources/Singletons/{T}.prefab lazy-load + Awake 트리거.
			_ = GameManager.Instance;

			// World-state lazy-load — γ leaf 외 root 매니저 (WeatherDirector 등) 보존.
			// (TASK-WM-078 P2 후속에서 root 매니저들도 Container spawn 으로 마이그)
			_ = WeatherDirector.Instance;

			// InputStrategySelector 가 SceneManager.sceneLoaded 구독 → 씬별 InputStrategy 설정
			// (World→InputStrategyWorld, Lobby→InputStrategyLobby). Resources prefab 없으므로
			// 직접 GameObject + AddComponent. 첫 sceneLoaded 이벤트 잡으려면 BeforeSceneLoad 시점 등록 필수.
			GameObject inputStrategySelectorGo = new GameObject(nameof(InputStrategySelector));
			inputStrategySelectorGo.AddComponent<InputStrategySelector>();
			Object.DontDestroyOnLoad(inputStrategySelectorGo);
		}
	}
}
