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
			// 사람이 에디터를 켜고 작업할 때만 쓰는 편의 기능이다.
			// 배치 빌드에서는 도움이 되기는커녕 **빌드가 여는 씬을 도로 바꿔치기한다.**
			// 실제로 노트북 빌드 로그마다 여기서 널 참조가 났다(설정 에셋을 못 찾아서).
			// 빌드는 그래도 끝났지만, 매 빌드가 에러 1건을 달고 나온다.
			if (Application.isBatchMode)
			{
				return;
			}

			EditorSceneManager.sceneOpened +=
				(scene, mode) =>
				{
					if (scene.name.Contains("World"))
					{
						// OpenScene(DataSOWindow.Instance.DataSOs[typeof(WorldStage)].Values.FirstOrDefault() as WorldStage);
						OpenScene(StartStage());
					}
				};

			EditorCoroutineUtility.StartCoroutine(WaitForSceneLoaded(), null);
		}

		private static IEnumerator WaitForSceneLoaded()
		{
			Debug.Log($"{nameof(EditorManager)} : {nameof(WaitForSceneLoaded)}");
			while (true)
			{
				if (SceneManager.GetActiveScene().isLoaded == false)
				{
					yield return null;
					continue;
				}

				if (SceneManager.GetActiveScene().name.Contains("World") == false)
				{
					OpenScene(StartStage());
					yield break;
				}
			}
		}

		/// <summary>
		/// 시작 월드 스테이지. 설정 에셋 자체가 없을 수도 있어 한 곳에서만 꺼낸다 —
		/// 두 곳에서 각자 꺼내면 한쪽만 널 검사를 빠뜨린다(실제로 그래서 터졌다).
		/// </summary>
		private static WorldStage StartStage()
		{
			EditorSettings settings = EditorSetting.Data;
			return settings == null ? null : settings.StartWorldStage;
		}

		public static void OpenScene(WorldStage worldStage)
		{
			// 설정 에셋이 없거나 시작 스테이지가 안 꽂혀 있으면 여기로 null 이 들어온다.
			// 그대로 쓰면 널 참조로 터지고, 부르는 쪽이 씬 열림 콜백이라 **에디터를 켤 때마다**
			// 같은 에러가 반복된다. 못 여는 건 못 여는 대로 한 줄 말하고 조용히 끝낸다.
			if (worldStage == null)
			{
				Debug.LogWarning($"{nameof(OpenScene)} : 시작 월드 스테이지가 없다 (에디터 설정 확인).");
				return;
			}

			Debug.Log($"OpenScene : {worldStage.Name}");

			for (int i = 0; i < SceneManager.sceneCount; i++)
			{
				Scene scene = SceneManager.GetSceneAt(i);
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