using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;

namespace WitchMendokusai
{
	public class InputStrategySelector : MonoBehaviour
	{
		private InputManager inputManager;

		// 씬이 넘겨 준 매니저. 선택기는 뿌리 스코프라 씬 것을 주입 못 받음. World 씬 스코프와 LobbyManager 가 꽂음
		private CameraManager worldCameraManager;
		private GameModeManager worldGameModeManager;
		private UIManager worldUIManager;
		private LobbyManager lobbyManager;

		[Inject]
		public void Construct(InputManager inputManager)
		{
			this.inputManager = inputManager;
		}

		/// <summary>World 씬 스코프의 build callback 이 부름. 씬이 다시 실리면 다시 꽂힘</summary>
		public void BindWorld(CameraManager cameraManager, GameModeManager gameModeManager, UIManager uiManager)
		{
			worldCameraManager = cameraManager;
			worldGameModeManager = gameModeManager;
			worldUIManager = uiManager;
		}

		/// <summary>월드 조작으로. 씬 로드 때와 모드 (투기장, 개척) 이탈 때 부름. 월드 전략을 짓는 자리는 여기 하나</summary>
		public void RestoreWorldStrategy()
		{
			if (worldGameModeManager == null || worldUIManager == null)
			{
				Debug.LogError("[InputStrategySelector] World 씬 매니저가 안 꽂힘. SceneLifetimeScope 가 BindWorld 를 불러야 함");
				return;
			}
			inputManager.SetInputStrategy(new InputStrategyWorld(worldCameraManager, worldGameModeManager, worldUIManager));
		}

		/// <summary>LobbyManager 가 주입받는 자리에서 부름 (Lobby 씬은 스코프 없음)</summary>
		public void BindLobby(LobbyManager lobbyManager)
		{
			this.lobbyManager = lobbyManager;
		}

		private void Awake()
		{
			SceneManager.sceneLoaded += OnSceneLoaded;
		}

		private void OnDestroy()
		{
			SceneManager.sceneLoaded -= OnSceneLoaded;
		}

		private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
		{
			StartCoroutine(SetStrategyAfterStart(scene.name));
		}

		private IEnumerator SetStrategyAfterStart(string sceneName)
		{
			yield return new WaitForEndOfFrame();

			// ★ 조작의 주인이 둘이면 나중 것이 앞의 것을 덮는다. 여기(선택기)는 *씬*으로 정하고,
			//   개척·투기장 같은 모드는 *모드*로 정한다 — 개척에 들어간 뒤 씬이 하나라도 더 실리면
			//   개척 조작이 조용히 월드 조작으로 되돌아간다(실측: 모드=개척인데 조작=월드).
			//   모드가 조작을 쥐고 있는 동안에는 선택기가 물러선다.
			if (GameModeManager.TryGetExistingInstance(out GameModeManager gameModeManager)
				&& gameModeManager.CurrentMode != GameMode.Default)
			{
				yield break;
			}

			switch (sceneName)
			{
				case "World":
					RestoreWorldStrategy();
					break;
				case "Lobby":
					if (lobbyManager == null)
					{
						Debug.LogError("[InputStrategySelector] LobbyManager 가 안 꽂힘. LobbyManager.Construct 가 BindLobby 를 불러야 함");
						break;
					}
					inputManager.SetInputStrategy(new InputStrategyLobby(lobbyManager));
					break;
				case "Loading":
					inputManager.SetInputStrategy(new InputStrategyLoading());
					break;
				case "Intro":
					// TASK-WM-115 — Intro = 설계상 비대화형 (IntroManager 타이머 패널 자동진행
					// → Lobby, 입력 처리 0). 입력전략 부재가 *정상* → no-op. default Warning 은
					// 진짜 미매핑 씬 신호로 보존 (R1 「정상을 경고로」 안티패턴 제거).
					break;
				default:
					Debug.LogWarning($"[InputStrategySelector] No strategy for scene: {sceneName}");
					break;
			}
		}
	}
}
