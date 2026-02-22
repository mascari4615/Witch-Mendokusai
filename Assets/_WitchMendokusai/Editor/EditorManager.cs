using System.Collections;
using System.Linq;
using Unity.EditorCoroutines.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WitchMendokusai
{
	[InitializeOnLoad]
	public class EditorManager
	{
		// 씬을 바뀌면 자동으로 씬을 열어주는 기능
		static EditorManager()
		{
			EditorSceneManager.sceneOpened +=
				(scene, mode) =>
				{
					if (scene.name.Contains("World"))
					{
						// OpenScene(DataSOWindow.Instance.DataSOs[typeof(WorldStage)].Values.FirstOrDefault() as WorldStage);
						OpenScene(EditorSetting.Data.StartWorldStage);
					}
				};

			EditorCoroutineUtility.StartCoroutine(WaitForSceneLoaded(), null);
		}

		private static IEnumerator WaitForSceneLoaded()
		{
			Debug.Log($"{nameof(EditorManager)} : {nameof(WaitForSceneLoaded)}");
			while (true)
			{
				if (EditorSceneManager.GetActiveScene().isLoaded == false)
				{
					yield return null;
					continue;
				}

				if (EditorSceneManager.GetActiveScene().name.Contains("World") == false)
				{
					OpenScene(EditorSetting.Data.StartWorldStage);
					yield break;
				}
			}
		}

		public static void OpenScene(WorldStage worldStage)
		{
			Debug.Log($"OpenScene : {worldStage.Name}");

			for (int i = 0; i < EditorSceneManager.sceneCount; i++)
			{
				Scene scene = EditorSceneManager.GetSceneAt(i);
				if (scene.name.Contains("World") == false && scene.isLoaded)
				{
					EditorSceneManager.CloseScene(scene, true);

					// string scenePath = AssetDatabase.FindAssets($"t:Scene Stage_{scene.Name}").Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault();
					// EditorSceneManager.CloseScene(EditorSceneManager.GetSceneByPath(scenePath), true);
				}
			}

			string scenePath = AssetDatabase.FindAssets($"t:Scene Stage_{worldStage.Name}").Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault();
			EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

			// EditorApplication.Beep();
		}
	}
}