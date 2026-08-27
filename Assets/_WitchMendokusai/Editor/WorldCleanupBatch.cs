using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WitchMendokusai.EditorTools
{
	/// <summary>
	/// 사람 손 대신 도는 「세계 하나 되기」 뒷정리 (TASK-WM-217/218).
	///
	/// ★ 왜 배치인가: 이 세 가지는 여태 「에디터에서 사람이 한 번 해주세요」로 남아 있었다 —
	///   남아 있는 한 아무도 안 한다. 유니티를 창 없이 띄워 같은 일을 시키면 사람 손이 0 이 된다.
	///
	/// 하는 일 셋:
	///   ① 아이템 목록 뽑기 (서버·게임이 같은 목록을 본다)
	///   ② 로비의 「멀티」 버튼 오브젝트 제거 — 세계는 하나라 고를 것이 없다
	///   ③ 씬·프리팹에 남은 <b>빠진 스크립트</b> 정리 (FishNet 을 지운 자리)
	///
	/// 창 없이:
	///   Unity.exe -batchmode -quit -projectPath &lt;repo&gt; -executeMethod WitchMendokusai.EditorTools.WorldCleanupBatch.Run
	///
	/// 하나라도 못 하면 <b>0 이 아닌 값으로 죽는다</b> — 조용히 통과하면 안 한 것과 구별이 안 된다.
	/// </summary>
	public static class WorldCleanupBatch
	{
		private const string LOBBY_SCENE = "Assets/_WitchMendokusai/Scenes/Lobby.unity";
		private const string MULTI_BUTTON_NAME = "[Button] Multi";

		private static readonly StringBuilder report = new StringBuilder();

		[MenuItem("WM/World Cleanup (Item Catalog + Multi Buttons + Missing Scripts)")]
		public static void Run()
		{
			report.Clear();
			bool ok = true;

			ok &= Step("아이템 목록 뽑기", ExportItems);
			ok &= Step("솥 재료를 아이템에 잇기", BrewIngredientLinker.LinkAndReport);
			ok &= Step("멀티 버튼 제거", RemoveMultiButton);
			ok &= Step("빠진 스크립트 정리", RemoveMissingScripts);

			AssetDatabase.SaveAssets();
			Debug.Log("[world-cleanup]\n" + report);

			if (Application.isBatchMode)
				EditorApplication.Exit(ok ? 0 : 1);
		}

		private static bool Step(string name, System.Func<string> body)
		{
			try
			{
				string line = body();
				report.AppendLine("  OK   " + name + " — " + line);
				return true;
			}
			catch (System.Exception error)
			{
				report.AppendLine("  FAIL " + name + " — " + error.Message);
				return false;
			}
		}

		private static string ExportItems()
		{
			ItemCatalogExporter.Export();
			return "게임 자산 → items.json (서버 옆 + Resources)";
		}

		/// <summary>
		/// 「혼자/같이」를 묻던 버튼을 씬에서 지운다. 이미 없으면 그것도 통과다(두 번 돌려도 안전).
		/// </summary>
		private static string RemoveMultiButton()
		{
			Scene scene = EditorSceneManager.OpenScene(LOBBY_SCENE, OpenSceneMode.Single);
			GameObject found = FindByName(scene, MULTI_BUTTON_NAME);
			if (found == null)
				return "이미 없음";

			Object.DestroyImmediate(found);
			EditorSceneManager.MarkSceneDirty(scene);
			EditorSceneManager.SaveScene(scene);
			return "지움 (" + MULTI_BUTTON_NAME + ")";
		}

		private static GameObject FindByName(Scene scene, string name)
		{
			GameObject[] roots = scene.GetRootGameObjects();
			for (int i = 0; i < roots.Length; i++)
			{
				Transform[] all = roots[i].GetComponentsInChildren<Transform>(true);
				for (int j = 0; j < all.Length; j++)
				{
					if (all[j].name == name)
						return all[j].gameObject;
				}
			}

			return null;
		}

		/// <summary>
		/// 스크립트가 사라진 자리에 남은 빈 껍데기를 걷어낸다 — 프리팹 먼저, 그 다음 씬.
		/// (프리팹을 먼저 고쳐야 씬이 그 프리팹을 다시 열 때 껍데기가 되살아나지 않는다.)
		/// </summary>
		private static string RemoveMissingScripts()
		{
			int fromPrefabs = 0;
			string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
			for (int i = 0; i < prefabGuids.Length; i++)
			{
				string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
				GameObject root = PrefabUtility.LoadPrefabContents(path);
				int removed = CountAndRemove(root);
				if (removed > 0)
				{
					PrefabUtility.SaveAsPrefabAsset(root, path);
					fromPrefabs += removed;
				}

				PrefabUtility.UnloadPrefabContents(root);
			}

			int fromScenes = 0;
			List<string> scenePaths = new List<string>();
			string[] sceneGuids = AssetDatabase.FindAssets("t:Scene");
			for (int i = 0; i < sceneGuids.Length; i++)
			{
				string path = AssetDatabase.GUIDToAssetPath(sceneGuids[i]);
				if (path.StartsWith("Assets/") == false)
					continue;

				scenePaths.Add(path);
			}

			for (int i = 0; i < scenePaths.Count; i++)
			{
				Scene scene = EditorSceneManager.OpenScene(scenePaths[i], OpenSceneMode.Single);
				int removed = 0;
				GameObject[] roots = scene.GetRootGameObjects();
				for (int j = 0; j < roots.Length; j++)
					removed += CountAndRemove(roots[j]);

				if (removed > 0)
				{
					EditorSceneManager.MarkSceneDirty(scene);
					EditorSceneManager.SaveScene(scene);
					fromScenes += removed;
				}
			}

			return "프리팹 " + fromPrefabs + "개 · 씬 " + fromScenes + "개 걷어냄";
		}

		private static int CountAndRemove(GameObject root)
		{
			int removed = 0;
			Transform[] all = root.GetComponentsInChildren<Transform>(true);
			for (int i = 0; i < all.Length; i++)
			{
				GameObject target = all[i].gameObject;
				if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(target) == 0)
					continue;

				removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(target);
			}

			return removed;
		}
	}
}
