using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WitchMendokusai
{
	public class InputStrategySelector : Singleton<InputStrategySelector>
	{
		protected override void Awake()
		{
			base.Awake();
			SceneManager.sceneLoaded += OnSceneLoaded;
		}

		protected override void OnDestroy()
		{
			SceneManager.sceneLoaded -= OnSceneLoaded;
			base.OnDestroy();
		}

		private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
		{
			Debug.Log($"[InputStrategySelector] Scene loaded: {scene.name}");
			StartCoroutine(SetStrategyAfterStart(scene.name));
		}

		private IEnumerator SetStrategyAfterStart(string sceneName)
		{
			yield return new WaitForEndOfFrame();

			switch (sceneName)
			{
				case "World":
					InputManager.Instance.SetInputStrategy(new InputStrategyWorld());
					break;
				case "Lobby":
					InputManager.Instance.SetInputStrategy(new InputStrategyLobby());
					break;
				case "Loading":
					InputManager.Instance.SetInputStrategy(new InputStrategyLoading());
					break;
				default:
					Debug.LogWarning($"[InputStrategySelector] No strategy for scene: {sceneName}");
					break;
			}
		}
	}
}
