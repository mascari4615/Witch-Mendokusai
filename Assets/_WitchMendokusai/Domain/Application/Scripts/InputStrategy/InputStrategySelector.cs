using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;

namespace WitchMendokusai
{
	public class InputStrategySelector : MonoBehaviour
	{
		private InputManager inputManager;

		[Inject]
		public void Construct(InputManager inputManager)
		{
			this.inputManager = inputManager;
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
					inputManager.SetInputStrategy(new InputStrategyWorld());
					break;
				case "Lobby":
					inputManager.SetInputStrategy(new InputStrategyLobby());
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
