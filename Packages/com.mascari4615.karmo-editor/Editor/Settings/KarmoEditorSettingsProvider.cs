using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using KarmoLab.KarmoEditor;

namespace KarmoLab.KarmoEditor.Settings
{
	/// <summary>
	/// Project Settings 창에 KarmoLab 설정을 통합 리스팅하는 프로바이더
	/// </summary>
	public class KarmoEditorSettingsProvider : SettingsProvider
	{
		private Editor _karmoSettingsEditor;
		private Editor _karmoToolbarSettingsEditor;

		public KarmoEditorSettingsProvider(string path, SettingsScope scope = SettingsScope.Project)
			: base(path, scope) { }

		public override void OnActivate(string searchContext, UnityEngine.UIElements.VisualElement rootElement)
		{
			// 설정 에셋 로드 (없으면 생성하지 않고 대기)
			LoadEditors();
		}

		private void LoadEditors()
		{
			var karmoSettings = FindAsset<KarmoEditorSettings>();
			if (karmoSettings != null && _karmoSettingsEditor == null)
				_karmoSettingsEditor = Editor.CreateEditor(karmoSettings);

			var toolbarSettings = FindAsset<KarmoToolbarSettings>();
			if (toolbarSettings != null && _karmoToolbarSettingsEditor == null)
				_karmoToolbarSettingsEditor = Editor.CreateEditor(toolbarSettings);
		}

		public override void OnGUI(string searchContext)
		{
			EditorGUILayout.Space();
			EditorGUILayout.LabelField("KarmoLab Editor Settings", EditorStyles.boldLabel);
			EditorGUILayout.Space();

			DrawSettingsSection("General Settings", _karmoSettingsEditor, typeof(KarmoEditorSettings));
			EditorGUILayout.Space(10);
			DrawSettingsSection("Toolbar Settings", _karmoToolbarSettingsEditor, typeof(KarmoToolbarSettings));

			if (_karmoSettingsEditor == null || _karmoToolbarSettingsEditor == null)
			{
				EditorGUILayout.Space(20);
				if (GUILayout.Button("Create Missing Settings Assets", GUILayout.Height(30)))
				{
					KarmoSettingsUtility.CreateKarmoSettings();
					KarmoSettingsUtility.CreateKarmoToolbarSettings();
					LoadEditors();
				}
			}
		}

		private void DrawSettingsSection(string title, Editor editor, System.Type type)
		{
			EditorGUILayout.BeginVertical(EditorStyles.helpBox);
			EditorGUILayout.LabelField(title, EditorStyles.miniBoldLabel);

			if (editor != null)
			{
				editor.OnInspectorGUI();
			}
			else
			{
				EditorGUILayout.HelpBox($"{type.Name} asset not found in {Define.DefaultSettingsPath}.", MessageType.Info);
			}
			EditorGUILayout.EndVertical();
		}

		private T FindAsset<T>() where T : UnityEngine.Object
		{
			string[] guids = AssetDatabase.FindAssets("t:" + typeof(T).Name);
			if (guids.Length > 0)
			{
				return AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[0]));
			}
			return null;
		}

		[SettingsProvider]
		public static SettingsProvider CreateProvider()
		{
			var provider = new KarmoEditorSettingsProvider(Define.ProjectSettingsPath, SettingsScope.Project);

			// 검색 키워드 등록
			provider.keywords = new HashSet<string>(new[] { "Karmo", "Lab", "Editor", "Settings", "Mutex", "Toolbar" });
			return provider;
		}
	}
}
