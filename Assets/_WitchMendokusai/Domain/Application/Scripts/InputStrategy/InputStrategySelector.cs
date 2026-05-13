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
				default:
					Debug.LogWarning($"[InputStrategySelector] No strategy for scene: {sceneName}");
					break;
			}
		}
	}
}
