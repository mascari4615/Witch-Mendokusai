using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace KarmoLab.KarmoEditor.Settings
{
	/// <summary>
	/// 툴바에 표시할 씬 및 에셋 정보를 저장하는 설정 파일
	/// </summary>
	[CreateAssetMenu(fileName = nameof(KarmoToolbarSettings), menuName = Define.CreateAssetMenuSettings + "/" + nameof(KarmoToolbarSettings))]
	public class KarmoToolbarSettings : ScriptableObject
	{
		// ===== Scene Selector 설정 =====
		[Header("Scene Selector")]
		[Tooltip("툴바 드롭다운에 항상 표시할 씬 목록")]
		public List<SceneAsset> FavoriteScenes = new List<SceneAsset>();

		[Tooltip("내부의 모든 씬을 자동으로 툴바에 포함할 폴더 목록")]
		public List<DefaultAsset> TargetFolders = new List<DefaultAsset>();

		[Tooltip("빌드 설정에 포함된 씬만 필터링할지 여부")]
		public bool ShowOnlyBuildSettingsScenes = false;

		// ===== Asset Selector 설정 =====
		[Header("Asset Selector")]
		[Tooltip("툴바 드롭다운에 항상 표시할 에셋 목록 (Selection 용)")]
		public List<Object> FavoriteAssets = new List<Object>();

		/// <summary>
		/// 설정에 따라 유효한 모든 씬 경로를 반환
		/// </summary>
		public IEnumerable<string> GetTargetScenePaths()
		{
			HashSet<string> paths = new HashSet<string>();

			// 1. Favorite Scenes
			foreach (SceneAsset scene in FavoriteScenes)
			{
				if (scene != null)
					paths.Add(AssetDatabase.GetAssetPath(scene));
			}

			// 2. Target Folders
			foreach (DefaultAsset folder in TargetFolders)
			{
				if (folder == null)
					continue;

				string folderPath = AssetDatabase.GetAssetPath(folder);
				if (!AssetDatabase.IsValidFolder(folderPath))
					continue;

				string[] guids = AssetDatabase.FindAssets("t:Scene", new[] { folderPath });
				foreach (string guid in guids)
					paths.Add(AssetDatabase.GUIDToAssetPath(guid));
			}

			// 3. Filter by Build Settings if enabled
			if (ShowOnlyBuildSettingsScenes)
			{
				HashSet<string> buildScenes = new HashSet<string>(
					EditorBuildSettings.scenes.Select(scene => scene.path)
				);
				paths.RemoveWhere(path => !buildScenes.Contains(path));
			}

			return paths;
		}
	}
}
