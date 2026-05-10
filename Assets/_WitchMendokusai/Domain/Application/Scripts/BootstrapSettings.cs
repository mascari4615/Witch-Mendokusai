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
		[field: SerializeField] public TimeManager TimeManagerPrefab { get; private set; }
		[field: SerializeField] public DataManager DataManagerPrefab { get; private set; }
		[field: SerializeField] public AudioManager AudioManagerPrefab { get; private set; }
		[field: SerializeField] public InputManager InputManagerPrefab { get; private set; }
		[field: SerializeField] public ShaderPackManager ShaderPackManagerPrefab { get; private set; }
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

			Object.Instantiate(bootstrapStuff.TimeManagerPrefab);
			Object.Instantiate(bootstrapStuff.DataManagerPrefab);
			Object.Instantiate(bootstrapStuff.AudioManagerPrefab);

			// GameManager.Awake 가 GameConditionBridge.Register / JoystickBridge / WindowLayoutBridge wire 담당.
			// InputManager 가 input 바인딩 시 Bridge 호출하므로 그 전에 GameManager 가 Awake 돼야 함.
			// Singleton<T>.Instance 접근 = Resources/Singletons/{T}.prefab lazy-load + Awake 트리거.
			_ = GameManager.Instance;

			Object.Instantiate(bootstrapStuff.InputManagerPrefab);
			Object.Instantiate(bootstrapStuff.ShaderPackManagerPrefab);

			// World-state Singleton lazy-load 강제 — Sky/Weather/Player 흐름은
			// 기존엔 씬 배치 또는 다른 매니저 접근으로 우연히 트리거되던 것을 명시적으로 ensure.
			// (asmdef 분할 이후 load 순서 불확정성 회복용. WM-056-A 후속.)
			// EventBus = TASK-WM-078 γ 에서 Container 가 spawn 책임 (위 RootLifetimeScope).
			_ = PlayerProvider.Instance;
			_ = WorldClock.Instance;
			_ = SkyDirector.Instance;
			_ = WeatherDirector.Instance;
			_ = WeatherSystem.Instance;

			// InputStrategySelector 가 SceneManager.sceneLoaded 구독 → 씬별 InputStrategy 설정
			// (World→InputStrategyWorld, Lobby→InputStrategyLobby). Resources prefab 없으므로
			// 직접 GameObject + AddComponent. 첫 sceneLoaded 이벤트 잡으려면 BeforeSceneLoad 시점 등록 필수.
			GameObject inputStrategySelectorGo = new GameObject(nameof(InputStrategySelector));
			inputStrategySelectorGo.AddComponent<InputStrategySelector>();
			Object.DontDestroyOnLoad(inputStrategySelectorGo);
		}
	}
}