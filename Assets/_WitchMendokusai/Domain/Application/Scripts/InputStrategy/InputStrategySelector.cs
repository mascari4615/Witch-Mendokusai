using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WitchMendokusai
{
	public class InputStrategySelector : MonoBehaviour
	{
		public static InputStrategySelector Instance { get; private set; }

		public static bool TryGetExistingInstance(out InputStrategySelector mgr)
		{
			mgr = Instance;
			return mgr != null;
		}

		private void Awake()
		{
			if (Instance != null && Instance != this) { Destroy(gameObject); return; }
			Instance = this;
			SceneManager.sceneLoaded += OnSceneLoaded;
		}

		private void OnDestroy()
		{
			SceneManager.sceneLoaded -= OnSceneLoaded;
			if (Instance == this)
				Instance = null;
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
