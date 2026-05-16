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
