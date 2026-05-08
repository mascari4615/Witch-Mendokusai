using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WitchMendokusai
{
	// 모든 .unity 씬의 Lighting Settings Skybox Material 을 SkyboxGradient.mat 으로 영구 변경.
	// sub-C C1 의 SkyDirector.Update 매 프레임 guard 가 임시 fix — 본 EditorWindow 가 영구 fix.
	// (TASK-WM-061)
	public class SceneSkyboxConverterMenu : EditorWindow
	{
		private const string SKYBOX_MAT_PATH = "Assets/_WitchMendokusai/Core/Resources/SkyboxGradient.mat";

		private readonly List<string> scenePaths = new List<string>();
		private Material targetSkybox;
		private Vector2 scrollPosition;

		[MenuItem("WM/Setup/Convert All Scene Skyboxes")]
		private static void Open()
		{
			SceneSkyboxConverterMenu window = GetWindow<SceneSkyboxConverterMenu>(true, "Convert All Scene Skyboxes");
			window.minSize = new Vector2(560f, 480f);
			window.Refresh();
		}

		private void Refresh()
		{
			targetSkybox = AssetDatabase.LoadAssetAtPath<Material>(SKYBOX_MAT_PATH);

			scenePaths.Clear();
			string[] sceneGuids = AssetDatabase.FindAssets("t:Scene");
			foreach (string guid in sceneGuids)
			{
				string path = AssetDatabase.GUIDToAssetPath(guid);
				if (path.StartsWith("Packages/") == true)
					continue;
				scenePaths.Add(path);
			}
			scenePaths.Sort();
		}

		private void OnGUI()
		{
			EditorGUILayout.LabelField("TASK-WM-061 — 모든 .unity 씬의 Lighting Settings Skybox 를 SkyboxGradient.mat 으로 영구 변경", EditorStyles.boldLabel);
			EditorGUILayout.HelpBox("sub-C C1 의 SkyDirector.Update 매 프레임 guard 의 영구 fix.\n실행 흐름: 각 씬 OpenScene → RenderSettings.skybox = SkyboxGradient → MarkSceneDirty + SaveScene.\n끝나면 시작 씬으로 복원.", MessageType.Info);

			EditorGUILayout.Space();

			EditorGUILayout.LabelField("Target", EditorStyles.miniBoldLabel);
			EditorGUILayout.ObjectField("SkyboxGradient.mat", targetSkybox, typeof(Material), false);

			if (targetSkybox == null)
				EditorGUILayout.HelpBox($"{SKYBOX_MAT_PATH} 미발견. SkyDirectorBootstrap 가 자동 생성하므로 한 번 Domain Reload 후 재시도.", MessageType.Error);

			EditorGUILayout.Space();

			using (new EditorGUI.DisabledScope(targetSkybox == null))
			{
				if (GUILayout.Button("Dry-run (씬별 현재 Skybox 출력만)"))
					Run(applyChanges: false);

				GUI.backgroundColor = new Color(0.6f, 1f, 0.6f);
				if (GUILayout.Button("Apply (모든 씬 변경 + 저장)"))
					Run(applyChanges: true);
				GUI.backgroundColor = Color.white;
			}

			EditorGUILayout.Space();
			EditorGUILayout.LabelField($"Scenes ({scenePaths.Count})", EditorStyles.miniBoldLabel);
			scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
			foreach (string path in scenePaths)
				EditorGUILayout.LabelField(path);
			EditorGUILayout.EndScrollView();

			if (GUILayout.Button("Refresh scene list"))
				Refresh();
		}

		private void Run(bool applyChanges)
		{
			if (targetSkybox == null)
			{
				Debug.LogError($"[SceneSkyboxConverter] {SKYBOX_MAT_PATH} 미발견");
				return;
			}

			string activeScenePath = EditorSceneManager.GetActiveScene().path;
			int changed = 0;
			int skipped = 0;

			try
			{
				foreach (string path in scenePaths)
				{
					Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

					Material currentSkybox = RenderSettings.skybox;
					string currentName = currentSkybox != null ? currentSkybox.name : "<null>";

					if (currentSkybox == targetSkybox)
					{
						Debug.Log($"[SceneSkyboxConverter] SKIP {path} — 이미 SkyboxGradient");
						skipped++;
						continue;
					}

					Debug.Log($"[SceneSkyboxConverter] {(applyChanges == true ? "APPLY" : "DRY")} {path} — was '{currentName}' → SkyboxGradient");

					if (applyChanges == false)
						continue;

					RenderSettings.skybox = targetSkybox;
					EditorSceneManager.MarkSceneDirty(scene);
					EditorSceneManager.SaveScene(scene);
					changed++;
				}
			}
			finally
			{
				if (string.IsNullOrEmpty(activeScenePath) == false && System.IO.File.Exists(activeScenePath) == true)
					EditorSceneManager.OpenScene(activeScenePath, OpenSceneMode.Single);
			}

			Debug.Log($"[SceneSkyboxConverter] 완료 — applied={changed}, skipped={skipped}, total={scenePaths.Count}");
		}
	}
}
