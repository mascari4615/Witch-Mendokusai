using UnityEditor;
using UnityEngine;
using KarmoLab.KarmoEditor.Settings;

namespace KarmoLab.KarmoEditor
{
	/// <summary>
	/// 패키지 설치 시 또는 메뉴를 통해 실행되는 초기 설정 마법사
	/// </summary>
	[InitializeOnLoad]
	public class KarmoEditorWelcomeWindow : EditorWindow
	{
		private const string ShowOnStartupKey = "KarmoEditor.ShowWelcomeOnStartup";

		static KarmoEditorWelcomeWindow()
		{
			// 에디터 시작 시 한 번만 체크
			EditorApplication.delayCall += () =>
			{
				if (EditorPrefs.GetBool(ShowOnStartupKey, true))
				{
					// 설정 에셋이 하나도 없으면 자동으로 띄움
					if (!HasAnySettings())
					{
						ShowWindow();
					}
				}
			};
		}

		[MenuItem(Define.RootMenu + "Welcome Wizard", priority = Define.DefaultPriority - 10)]
		public static void ShowWindow()
		{
			var window = GetWindow<KarmoEditorWelcomeWindow>("KarmoLab Welcome", true);
			window.minSize = new Vector2(400, 500);
			window.maxSize = new Vector2(400, 500);
			window.Show();
		}

		private static bool HasAnySettings()
		{
			return AssetDatabase.FindAssets("t:KarmoEditorSettings").Length > 0 ||
				   AssetDatabase.FindAssets("t:KarmoToolbarSettings").Length > 0;
		}

		private void OnGUI()
		{
			DrawHeader();

			EditorGUILayout.Space(5);
			// 언어 선택 UI
			EditorGUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();
			var currentLang = KarmoEditorLocalization.CurrentLanguage;
			var nextLang = (KarmoEditorLocalization.Language)EditorGUILayout.EnumPopup(currentLang, GUILayout.Width(100));
			if (currentLang != nextLang) KarmoEditorLocalization.CurrentLanguage = nextLang;
			EditorGUILayout.EndHorizontal();

			EditorGUILayout.Space(10);
			EditorGUILayout.BeginVertical(new GUIStyle { padding = new RectOffset(20, 20, 0, 0) });

			EditorGUILayout.LabelField(KarmoEditorLocalization.WelcomeTitle, EditorStyles.boldLabel);
			EditorGUILayout.HelpBox(KarmoEditorLocalization.Get(
				"이 마법사는 프로젝트의 초기 구성을 도와줍니다.",
				"This wizard will help you set up the initial configuration for your project."), MessageType.Info);

			EditorGUILayout.Space(20);

			if (HasAnySettings())
			{
				DrawAlreadyConfigured();
			}
			else
			{
				DrawSetupButton();
			}

			EditorGUILayout.Space(20);
			DrawLinks();

			EditorGUILayout.EndVertical();

			DrawFooter();
		}

		private void DrawHeader()
		{
			var headerStyle = new GUIStyle(EditorStyles.label);
			headerStyle.alignment = TextAnchor.MiddleCenter;
			headerStyle.fontSize = 24;
			headerStyle.fontStyle = FontStyle.Bold;

			EditorGUILayout.Space(10);
			EditorGUILayout.LabelField("KarmoLab", headerStyle, GUILayout.Height(40));
			EditorGUILayout.LabelField("KarmoEditor Package", EditorStyles.centeredGreyMiniLabel);
			EditorGUILayout.Space(10);

			Rect lineRect = EditorGUILayout.GetControlRect(false, 1);
			EditorGUI.DrawRect(lineRect, Color.gray);
		}

		private void DrawSetupButton()
		{
			if (GUILayout.Button(KarmoEditorLocalization.Get("🚀 초기 설정 시작", "🚀 Start Initial Setup"), GUILayout.Height(50)))
			{
				KarmoSettingsUtility.CreateKarmoSettings();
				KarmoSettingsUtility.CreateKarmoToolbarSettings();
				SettingsService.OpenProjectSettings(Define.ProjectSettingsPath);
				Close();
			}
			EditorGUILayout.LabelField(KarmoEditorLocalization.Get(
				$"위 버튼을 클릭하면 {Define.DefaultSettingsPath} 폴더에 기본 설정 에셋이 생성됩니다.",
				$"Click above to create default settings assets in {Define.DefaultSettingsPath}"), EditorStyles.miniLabel);
		}

		private void DrawAlreadyConfigured()
		{
			EditorGUILayout.HelpBox(KarmoEditorLocalization.Get(
				"프로젝트에서 설정 에셋을 이미 찾았습니다! ✨",
				"Settings assets are already found in your project! ✨"), MessageType.None);
			if (GUILayout.Button(KarmoEditorLocalization.Get("프로젝트 설정으로 이동", "Go to Project Settings"), GUILayout.Height(40)))
			{
				SettingsService.OpenProjectSettings(Define.ProjectSettingsPath);
			}
		}

		private void DrawLinks()
		{
			EditorGUILayout.LabelField("Resources", EditorStyles.boldLabel);
			if (GUILayout.Button("📖 Documentation (Local README)"))
			{
				string[] guids = AssetDatabase.FindAssets("README t:TextAsset");
				foreach (var guid in guids)
				{
					string path = AssetDatabase.GUIDToAssetPath(guid);
					if (path.Contains("com.mascari4615.karmo-editor"))
					{
						var readme = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
						AssetDatabase.OpenAsset(readme);
						return;
					}
				}
				EditorUtility.DisplayDialog("KarmoEditor", "README.md not found in package.", "OK");
			}
			if (GUILayout.Button("📦 Import Samples (Coming Soon)"))
			{
				EditorUtility.DisplayDialog("KarmoLab",
					KarmoEditorLocalization.Get("샘플 데이터는 현재 준비 중입니다! 차후 업데이트를 기대해주세요.",
					"Samples are currently being prepared! Please look forward to future updates."), "OK");
			}
		}

		private void DrawFooter()
		{
			GUILayout.FlexibleSpace();
			bool showOnStartup = EditorPrefs.GetBool(ShowOnStartupKey, true);
			bool nextShowOnStartup = EditorGUILayout.ToggleLeft("Show on startup if no configuration", showOnStartup);
			if (showOnStartup != nextShowOnStartup)
			{
				EditorPrefs.SetBool(ShowOnStartupKey, nextShowOnStartup);
			}
			EditorGUILayout.Space(5);
		}
	}
}
