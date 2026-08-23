using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace WitchMendokusai.EditorTools
{
	/// <summary>
	/// V2 작전 씬을 코드로 짓는다 (concept-v2 — 쿼터뷰 무대 + HUD).
	///
	/// ★ 손으로 안 만드는 이유·순서(에셋 먼저, 씬은 뒤)·다시 열어 검사하는 이유는
	///   <see cref="IdleSceneBuilder"/> 와 같다 — 실측으로 얻은 규칙 그대로.
	/// </summary>
	public static class IdleV2SceneBuilder
	{
		private const string SCENE_PATH = "Assets/_WitchMendokusai/Scenes/Idle/IdleV2.unity";
		private const string PANEL_PATH = "Assets/_WitchMendokusai/Scenes/Idle/PS_0001_Idle.asset";
		private const string TUNING_PATH = "Assets/_WitchMendokusai/Scenes/Idle/TU_0001_Idle.asset";
		private const string STYLE_PATH = "Assets/_WitchMendokusai/Idle/IdleBattleScreen.uss";
		private const string TAG = "[IdleV2Scene]";

		[MenuItem("WM/Idle/V2 열고 플레이 %#u")]
		public static void OpenAndPlay()
		{
			if (EditorApplication.isPlaying)
			{
				EditorApplication.isPlaying = false;
				return;
			}

			if (File.Exists(SCENE_PATH) == false)
			{
				Debug.LogError(TAG + " 씬이 없다 — 먼저 WM/Idle/V2 씬 짓기: " + SCENE_PATH);
				return;
			}

			if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo() == false)
			{
				return;
			}

			EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Single);
			EditorSceneManager.playModeStartScene = null;
			EditorApplication.isPlaying = true;
		}

		[MenuItem("WM/Idle/V2 씬 짓기")]
		public static void Build()
		{
			// 에셋 먼저 디스크에 확정 — 씬을 연 뒤 경로로 다시 읽는다 (IdleSceneBuilder 의 실측 규칙).
			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();

			Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

			PanelSettings panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PANEL_PATH);
			IdleTuningSO tuning = AssetDatabase.LoadAssetAtPath<IdleTuningSO>(TUNING_PATH);
			StyleSheet style = AssetDatabase.LoadAssetAtPath<StyleSheet>(STYLE_PATH);

			if (panel == null || tuning == null || style == null)
			{
				Debug.LogError(TAG + " 붙일 것을 못 읽었다 — panel/tuning/style = "
					+ (panel != null) + "/" + (tuning != null) + "/" + (style != null)
					+ " (panel·tuning 은 WM/Idle/씬 짓기가 만든다)");
				return;
			}

			// 카메라 — 쿼터뷰. 하늘색 단색이 배경이다 (V2 톤: 밝은 판).
			GameObject cameraObject = new GameObject("Main Camera");
			cameraObject.tag = "MainCamera";
			Camera camera = cameraObject.AddComponent<Camera>();
			camera.clearFlags = CameraClearFlags.SolidColor;
			camera.backgroundColor = new Color(0.75f, 0.88f, 0.96f);
			camera.fieldOfView = 42f;
			cameraObject.transform.position = new Vector3(0f, 7.4f, -6.8f);
			cameraObject.transform.rotation = Quaternion.Euler(50f, 4f, 0f);

			GameObject lightObject = new GameObject("Directional Light");
			Light light = lightObject.AddComponent<Light>();
			light.type = LightType.Directional;
			light.intensity = 1.15f;
			light.shadows = LightShadows.Soft;
			lightObject.transform.rotation = Quaternion.Euler(52f, -28f, 0f);

			GameObject stageObject = new GameObject("BattleStage");
			IdleBattleStage stage = stageObject.AddComponent<IdleBattleStage>();

			GameObject screenObject = new GameObject("IdleBattleScreen");
			UIDocument document = screenObject.AddComponent<UIDocument>();
			document.panelSettings = panel;

			IdleBattleScreen screen = screenObject.AddComponent<IdleBattleScreen>();
			AssignPrivateField(screen, "tuningAsset", tuning);
			AssignPrivateField(screen, "styleSheet", style);
			AssignPrivateField(screen, "stage", stage);

			// 이게 없으면 버튼이 안 눌린다 — 화면은 멀쩡해 눈으로 못 잡는다.
			GameObject eventSystem = new GameObject("EventSystem");
			eventSystem.AddComponent<EventSystem>();
			eventSystem.AddComponent<InputSystemUIInputModule>();

			EditorSceneManager.MarkSceneDirty(scene);
			EditorSceneManager.SaveScene(scene, SCENE_PATH);

			AddToBuildSettings();
			AssetDatabase.SaveAssets();

			if (Verify() == false)
			{
				return;
			}

			Debug.Log(TAG + " 씬을 지었다: " + SCENE_PATH);
		}

		/// <summary>저장된 씬을 다시 열어 빈 참조를 본다 — 메모리와 디스크는 다른 말이다.</summary>
		[MenuItem("WM/Idle/V2 씬 검사")]
		public static bool Verify()
		{
			Scene scene = EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Single);

			IdleBattleScreen screen = Object.FindAnyObjectByType<IdleBattleScreen>();
			IdleBattleStage stage = Object.FindAnyObjectByType<IdleBattleStage>();
			UIDocument document = Object.FindAnyObjectByType<UIDocument>();
			EventSystem events = Object.FindAnyObjectByType<EventSystem>();
			Camera camera = Object.FindAnyObjectByType<Camera>();

			List<string> missing = new List<string>();

			if (screen == null) { missing.Add("IdleBattleScreen"); }
			if (stage == null) { missing.Add("IdleBattleStage"); }
			if (camera == null) { missing.Add("Main Camera (없으면 무대가 안 보인다)"); }
			if (events == null) { missing.Add("EventSystem (없으면 버튼이 안 눌린다)"); }

			if (document == null)
			{
				missing.Add("UIDocument");
			}
			else if (document.panelSettings == null)
			{
				missing.Add("UIDocument.panelSettings");
			}

			if (screen != null)
			{
				SerializedObject serialized = new SerializedObject(screen);
				if (serialized.FindProperty("styleSheet").objectReferenceValue == null)
				{
					missing.Add("IdleBattleScreen.styleSheet");
				}
				if (serialized.FindProperty("tuningAsset").objectReferenceValue == null)
				{
					missing.Add("IdleBattleScreen.tuningAsset");
				}
				if (serialized.FindProperty("stage").objectReferenceValue == null)
				{
					missing.Add("IdleBattleScreen.stage (무대 없이 HUD 만 뜬다)");
				}
			}

			if (missing.Count > 0)
			{
				Debug.LogError(TAG + " 씬이 비었다 — " + string.Join(" · ", missing));
				return false;
			}

			Debug.Log(TAG + " 검사 통과 — 붙을 것이 다 붙어 있다 (" + scene.name + ")");
			return true;
		}

		private static void AssignPrivateField(Object target, string fieldName, Object value)
		{
			SerializedObject serialized = new SerializedObject(target);
			serialized.FindProperty(fieldName).objectReferenceValue = value;
			serialized.ApplyModifiedPropertiesWithoutUndo();
		}

		private static void AddToBuildSettings()
		{
			List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

			foreach (EditorBuildSettingsScene listed in scenes)
			{
				if (listed.path == SCENE_PATH)
				{
					return;
				}
			}

			scenes.Add(new EditorBuildSettingsScene(SCENE_PATH, true));
			EditorBuildSettings.scenes = scenes.ToArray();
		}
	}
}
