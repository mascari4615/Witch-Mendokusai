using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;
using VContainer.Unity;

namespace WitchMendokusai
{
	public static class Bootstrap
	{
		private const string WORLD_SCENE_NAME = "World";

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
		public static void OnBooting()
		{
			// RootLifetimeScope spawn 강제 — caller 매니저 Awake (씬 안 GameObject) 가
			// Container 의존 시 ready 보장. VContainerSettings.OnFirstSceneLoaded (AfterSceneLoad) 보다
			// 먼저 spawn 해야 race 0. eager Resolve 20 매니저 = RootLifetimeScope.Configure 의
			// RegisterBuildCallback 가 흡수 (TASK-WM-078 θ, 2026-05-11).
			if (VContainerSettings.Instance == null)
			{
				VContainerSettings.LoadInstanceFromPreloadAssets();
			}
			VContainerSettings.Instance.GetOrCreateRootLifetimeScopeInstance();

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
