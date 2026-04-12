using System.IO;
using UnityEditor;
using UnityEngine;
using KarmoLab.KarmoEditor;

namespace KarmoLab.KarmoEditor.Settings
{
	/// <summary>
	/// KarmoEditor 설정 에셋 생성을 위한 유틸리티 클래스
	/// </summary>
	public static class KarmoSettingsUtility
	{
		[MenuItem(Define.RootMenu + "Settings/Open Karmo Settings %&k")]
		public static void OpenSettings() => SettingsService.OpenProjectSettings(Define.ProjectSettingsPath);

		[MenuItem(Define.RootMenu + "Settings/Create KarmoEditorSettings")]
		public static void CreateKarmoSettings() => CreateAsset<KarmoEditorSettings>();

		[MenuItem(Define.RootMenu + "Settings/Create KarmoToolbarSettings")]
		public static void CreateKarmoToolbarSettings() => CreateAsset<KarmoToolbarSettings>();

		/// <summary>
		/// 지정된 타입의 ScriptableObject 에셋을 공용 설정 폴더에 생성
		/// </summary>
		public static void CreateAsset<T>() where T : ScriptableObject
		{
			string typeName = typeof(T).Name;
			string path = Define.DefaultSettingsPath;

			if (!Directory.Exists(path))
			{
				Directory.CreateDirectory(path);
				AssetDatabase.Refresh();
			}

			string assetPath = $"{path}/{typeName}.asset";

			if (File.Exists(assetPath))
			{
				UnityEngine.Debug.Log($"{Define.LogPrefix} {typeName} already exists at {assetPath}. Selecting it.");
				Selection.activeObject = AssetDatabase.LoadAssetAtPath<T>(assetPath);
				EditorGUIUtility.PingObject(Selection.activeObject);
				return;
			}

			T asset = ScriptableObject.CreateInstance<T>();
			AssetDatabase.CreateAsset(asset, assetPath);
			AssetDatabase.SaveAssets();

			UnityEngine.Debug.Log($"{Define.LogPrefix} Created {typeName} at {assetPath}");

			EditorUtility.FocusProjectWindow();
			Selection.activeObject = asset;
			EditorGUIUtility.PingObject(asset);
		}
	}
}
