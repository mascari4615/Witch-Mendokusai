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
	/// 방치 씬을 <b>코드로 짓는다</b> (TASK-WM-406).
	///
	/// ★ 왜 손으로 안 만드나 — 씬·PanelSettings 는 YAML 이라 손으로 쓰면 조용히 깨진다.
	///   그리고 손으로 만든 씬은 <b>어떻게 만들었는지가 어디에도 안 남는다</b>.
	///   여기서는 짓는 방법 자체가 코드라 다시 지을 수 있고, 무엇이 왜 붙었는지 읽힌다.
	///
	/// ★ 배치(<c>-executeMethod</c>)에서도 돈다 — 사람이 에디터를 열지 않아도 씬이 선다.
	/// </summary>
	public static class IdleSceneBuilder
	{
		private const string SCENE_PATH = "Assets/_WitchMendokusai/Scenes/Idle/Idle.unity";
		private const string PANEL_PATH = "Assets/_WitchMendokusai/Scenes/Idle/PS_0001_Idle.asset";
		private const string TUNING_PATH = "Assets/_WitchMendokusai/Scenes/Idle/TU_0001_Idle.asset";
		private const string STYLE_PATH = "Assets/_WitchMendokusai/Domain/Idle/IdleScreen.uss";
		private const string THEME_PATH = "Assets/Settings/UnityDefaultRuntimeTheme.tss";
		private const string TAG = "[IdleScene]";

		/// <summary>
		/// 방치형 씬을 열고 바로 <b>Play</b> — 한 번에 (TASK-WM-406).
		///
		/// ★ 왜 필요한가 — 본편 씬이 열린 채로 Play 를 누르면 당연히 본편이 뜬다(실제로 겪었다).
		///   방치형은 따로 파는 게임이라 <b>들어가는 문이 따로</b> 있어야 헷갈리지 않는다.
		/// </summary>
		[MenuItem("WM/Idle/열고 플레이 %#i")]
		public static void OpenAndPlay()
		{
			if (EditorApplication.isPlaying)
			{
				EditorApplication.isPlaying = false;
				return;
			}

			if (File.Exists(SCENE_PATH) == false)
			{
				Debug.LogError(TAG + " 씬이 없다 — 먼저 WM/Idle/씬 짓기: " + SCENE_PATH);
				return;
			}

			if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo() == false)
			{
				return;
			}

			EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Single);

			// ★ Play 시작 씬 가로채기를 끈다 — `EditorStartInit` 가 첫 씬(Intro)으로 고정해 둬서
			//   방치형 씬을 열어도 계속 본편이 떴다(실측 2026-08-16: 활성 씬이 'Intro' 였다).
			EditorSceneManager.playModeStartScene = null;

			EditorApplication.isPlaying = true;
		}

		[MenuItem("WM/Idle/씬 짓기")]
		public static void Build()
		{
			// ★ 순서가 중요하다 (실측 2026-08-16). 처음엔 에셋을 만들어 쥔 채로 `NewScene` 을 불렀는데,
			//   새 씬이 열리면서 <b>그 인스턴스가 무효가 됐다</b> — 씬은 멀쩡히 저장되고 로그도 「지었다」인데
			//   열어 보면 참조가 전부 `fileID: 0`, 즉 <b>켜면 빈 화면</b>이었다. 로그가 거짓 초록을 낸 것이다.
			//   그래서 ① 에셋을 먼저 만들어 디스크에 확정하고 ② 씬을 연 <b>뒤에</b> 경로로 다시 읽는다.
			EnsurePanelSettings();
			EnsureTuning();
			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();

			Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

			PanelSettings panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PANEL_PATH);
			IdleTuningSO tuning = AssetDatabase.LoadAssetAtPath<IdleTuningSO>(TUNING_PATH);
			StyleSheet style = AssetDatabase.LoadAssetAtPath<StyleSheet>(STYLE_PATH);

			if (panel == null || tuning == null || style == null)
			{
				Debug.LogError(TAG + " 붙일 것을 못 읽었다 — panel/tuning/style = "
					+ (panel != null) + "/" + (tuning != null) + "/" + (style != null));
				return;
			}

			// UI 만으로 도는 게임이라 카메라도 빛도 없다 — UIDocument 하나면 그린다.
			GameObject screenObject = new GameObject("IdleScreen");
			UIDocument document = screenObject.AddComponent<UIDocument>();
			document.panelSettings = panel;

			IdleScreen screen = screenObject.AddComponent<IdleScreen>();
			AssignPrivateField(screen, "tuningAsset", tuning);
			AssignPrivateField(screen, "styleSheet", style);

			// ★ 이게 없으면 <b>버튼이 안 눌린다</b>. 화면은 멀쩡히 그려지므로 눈으로는 못 잡는다 —
			//   판이 도는데 아무것도 살 수 없는 상태가 되고, 원인은 씬에 없는 물건이다.
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

		/// <summary>
		/// 지어 놓고 <b>비었는지</b> 본다 (실측으로 생긴 검사, 2026-08-16).
		///
		/// ★ 저장된 씬을 <b>다시 열어서</b> 확인한다 — 메모리에 쥔 것이 멀쩡한 것과
		///   디스크에 적힌 것이 멀쩡한 것은 다른 말이고, 여기서 갈렸던 게 실제 결함이었다.
		///   빈 참조는 화면이 그냥 안 그려지는 것으로 나타나 눈으로는 원인을 못 짚는다.
		/// </summary>
		[MenuItem("WM/Idle/씬 검사")]
		public static bool Verify()
		{
			Scene scene = EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Single);

			IdleScreen screen = Object.FindAnyObjectByType<IdleScreen>();
			UIDocument document = Object.FindAnyObjectByType<UIDocument>();
			EventSystem events = Object.FindAnyObjectByType<EventSystem>();

			List<string> missing = new List<string>();

			if (screen == null) { missing.Add("IdleScreen"); }
			if (events == null) { missing.Add("EventSystem (없으면 버튼이 안 눌린다)"); }

			if (document == null)
			{
				missing.Add("UIDocument");
			}
			else if (document.panelSettings == null)
			{
				missing.Add("UIDocument.panelSettings (없으면 아무것도 안 그려진다)");
			}

			if (screen != null)
			{
				SerializedObject serialized = new SerializedObject(screen);
				if (serialized.FindProperty("styleSheet").objectReferenceValue == null)
				{
					missing.Add("IdleScreen.styleSheet");
				}
				if (serialized.FindProperty("tuningAsset").objectReferenceValue == null)
				{
					missing.Add("IdleScreen.tuningAsset");
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

		private static PanelSettings EnsurePanelSettings()
		{
			PanelSettings panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PANEL_PATH);
			if (panel != null)
			{
				return panel;
			}

			panel = ScriptableObject.CreateInstance<PanelSettings>();
			panel.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(THEME_PATH);
			// 화면 크기가 달라도 같은 판으로 보이게 — 창 크기를 기준 해상도에 맞춰 늘린다.
			panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
			panel.referenceResolution = new Vector2Int(1280, 720);

			AssetDatabase.CreateAsset(panel, PANEL_PATH);
			Debug.Log(TAG + " PanelSettings 를 만들었다: " + PANEL_PATH);
			return panel;
		}

		private static IdleTuningSO EnsureTuning()
		{
			IdleTuningSO tuning = AssetDatabase.LoadAssetAtPath<IdleTuningSO>(TUNING_PATH);
			if (tuning != null)
			{
				return tuning;
			}

			tuning = ScriptableObject.CreateInstance<IdleTuningSO>();
			AssetDatabase.CreateAsset(tuning, TUNING_PATH);
			Debug.Log(TAG + " 수치 에셋을 만들었다: " + TUNING_PATH);
			return tuning;
		}

		/// <summary>인스펙터에 내놓은 칸은 private 이라 <see cref="SerializedObject"/> 로 채운다.</summary>
		private static void AssignPrivateField(Object target, string fieldName, Object value)
		{
			SerializedObject serialized = new SerializedObject(target);
			serialized.FindProperty(fieldName).objectReferenceValue = value;
			serialized.ApplyModifiedPropertiesWithoutUndo();
		}

		/// <summary>빌드 목록에 없으면 씬은 <b>빌드에 안 실린다</b> — 지어 놓고 못 켜는 상태가 된다.</summary>
		private static void AddToBuildSettings()
		{
			List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

			foreach (EditorBuildSettingsScene one in scenes)
			{
				if (one.path == SCENE_PATH)
				{
					return;
				}
			}

			scenes.Add(new EditorBuildSettingsScene(SCENE_PATH, true));
			EditorBuildSettings.scenes = scenes.ToArray();
			Debug.Log(TAG + " 빌드 목록에 넣었다 (index " + (scenes.Count - 1) + ")");
		}
	}
}
