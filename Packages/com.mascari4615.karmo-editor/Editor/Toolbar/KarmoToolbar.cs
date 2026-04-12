using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Toolbars;
using UnityEngine;
using KarmoLab.KarmoEditor.Settings;

namespace KarmoLab.KarmoEditor.Toolbar
{
	/// <summary>
	/// Unity 6.3+ 메인 툴바 확장 클래스
	/// </summary>
	public static class KarmoToolbar
	{
		public const string SceneSelectorID = Define.RootMenu + "SceneSelector";
		public const string AssetSelectorID = Define.RootMenu + "AssetSelector";

		// Config 캐싱 (성능 최적화 - AssetDatabase 호출 최소화)
		private static KarmoToolbarSettings _cachedConfig;
		private static double _lastConfigCheckTime;
		private const double ConfigCacheLifetime = 1.0; // 1초마다 재검증

		// Scene Selector
		[MainToolbarElement(SceneSelectorID, defaultDockPosition = MainToolbarDockPosition.Middle)]
		static IEnumerable<MainToolbarElement> CreateSceneSelector()
		{
			string activeScene = EditorSceneManager.GetActiveScene().name;
			if (string.IsNullOrEmpty(activeScene))
				activeScene = "No Scene";

			MainToolbarContent content = new MainToolbarContent(activeScene, "설정된 씬 목록으로 빠르게 이동");
			content.image = EditorGUIUtility.IconContent("SceneAsset Icon").image as Texture2D;

			yield return new MainToolbarDropdown(content, ShowSceneMenu);
		}

		private static void ShowSceneMenu(Rect worldBound)
		{
			GenericMenu menu = new GenericMenu();
			KarmoToolbarSettings config = FindConfig();

			if (config == null)
			{
				menu.AddDisabledItem(new GUIContent("Config not found! Create one via Assets menu."));
			}
			else
			{
				List<string> paths = config.GetTargetScenePaths().OrderBy(p => p).ToList();

				if (paths.Count == 0)
					menu.AddDisabledItem(new GUIContent("No scenes found in config."));
				else
				{
					foreach (string path in paths)
					{
						string sceneName = Path.GetFileNameWithoutExtension(path);
						bool isActive = EditorSceneManager.GetActiveScene().path == path;

						menu.AddItem(new GUIContent(sceneName), isActive, () => OpenScene(path));
					}
				}
			}

			menu.DropDown(worldBound);
		}

		private static void OpenScene(string path)
		{
			if (EditorSceneManager.GetActiveScene().path == path)
				return;

			if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
			{
				EditorSceneManager.OpenScene(path);
				MainToolbar.Refresh(SceneSelectorID);
			}
		}

		// Asset Selector (Quick Inspector)
		[MainToolbarElement(AssetSelectorID, defaultDockPosition = MainToolbarDockPosition.Middle)]
		static IEnumerable<MainToolbarElement> CreateAssetSelector()
		{
			MainToolbarContent content = new MainToolbarContent("Assets", "등록된 에셋을 빠르게 Inspector에서 확인");
			content.image = EditorGUIUtility.IconContent("UnityEditor.InspectorWindow").image as Texture2D;

			yield return new MainToolbarDropdown(content, ShowAssetMenu);
		}

		private static void ShowAssetMenu(Rect worldBound)
		{
			GenericMenu menu = new GenericMenu();
			KarmoToolbarSettings config = FindConfig();

			if (config == null)
			{
				menu.AddDisabledItem(new GUIContent("Config not found! Create one via Assets menu."));
			}
			else
			{
				List<Object> assets = config.FavoriteAssets
					.Where(asset => asset != null)
					.OrderBy(asset => asset.name)
					.ToList();

				if (assets.Count == 0)
					menu.AddDisabledItem(new GUIContent("No assets registered in config."));
				else
				{
					foreach (Object asset in assets)
					{
						bool isSelected = Selection.activeObject == asset;
						menu.AddItem(new GUIContent(asset.name), isSelected, () => SelectAsset(asset));
					}
				}
			}

			menu.DropDown(worldBound);
		}

		private static void SelectAsset(Object asset)
		{
			Selection.activeObject = asset;
			EditorGUIUtility.PingObject(asset);
		}

		/// <summary>
		/// KarmoToolbarSettings 찾기 (캐싱 적용)
		/// </summary>
		private static KarmoToolbarSettings FindConfig()
		{
			// 캐시가 유효하면 재사용
			double currentTime = EditorApplication.timeSinceStartup;
			if (_cachedConfig != null && (currentTime - _lastConfigCheckTime) < ConfigCacheLifetime)
				return _cachedConfig;

			// 캐시 갱신
			string[] guids = AssetDatabase.FindAssets("t:" + nameof(KarmoToolbarSettings));
			if (guids.Length > 0)
			{
				string path = AssetDatabase.GUIDToAssetPath(guids[0]);
				_cachedConfig = AssetDatabase.LoadAssetAtPath<KarmoToolbarSettings>(path);
				_lastConfigCheckTime = currentTime;
				return _cachedConfig;
			}

			_cachedConfig = null;
			_lastConfigCheckTime = currentTime;
			return null;
		}
	}
}
