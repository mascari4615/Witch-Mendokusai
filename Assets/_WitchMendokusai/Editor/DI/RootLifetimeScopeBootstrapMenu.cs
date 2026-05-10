using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VContainer.Unity;

namespace WitchMendokusai.Editor
{
	public static class RootLifetimeScopeBootstrapMenu
	{
		private const string PREFAB_PATH = "Assets/_WitchMendokusai/Core/Resources/Singletons/RootLifetimeScope.prefab";
		private const string SETTINGS_PATH = "Assets/_WitchMendokusai/Core/VContainer/VContainerSettings.asset";

		[InitializeOnLoadMethod]
		private static void EnsureWiring()
		{
			GameObject prefab = EnsurePrefab();
			VContainerSettings settings = EnsureSettings(prefab);
			EnsurePreloaded(settings);
		}

		[MenuItem("WM/Setup/RootLifetimeScope (DI)")]
		private static void EnsureWiringMenu()
		{
			EnsureWiring();
			Debug.Log($"[RootLifetimeScopeBootstrap] wired. prefab={PREFAB_PATH}, settings={SETTINGS_PATH}, preloaded={PlayerSettings.GetPreloadedAssets().Length}");
		}

		private static GameObject EnsurePrefab()
		{
			GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH);
			if (existing != null && existing.GetComponent<RootLifetimeScope>() != null)
			{
				return existing;
			}

			string directory = Path.GetDirectoryName(PREFAB_PATH);
			EnsureFolder(directory);

			GameObject temp = new GameObject(nameof(RootLifetimeScope));
			temp.AddComponent<RootLifetimeScope>();

			GameObject prefab = PrefabUtility.SaveAsPrefabAsset(temp, PREFAB_PATH);
			Object.DestroyImmediate(temp);
			return prefab;
		}

		private static VContainerSettings EnsureSettings(GameObject prefab)
		{
			RootLifetimeScope prefabScope = prefab != null ? prefab.GetComponent<RootLifetimeScope>() : null;

			VContainerSettings existing = AssetDatabase.LoadAssetAtPath<VContainerSettings>(SETTINGS_PATH);
			if (existing != null)
			{
				if (existing.RootLifetimeScope == null && prefabScope != null)
				{
					existing.RootLifetimeScope = prefabScope;
					EditorUtility.SetDirty(existing);
					AssetDatabase.SaveAssetIfDirty(existing);
				}
				return existing;
			}

			string directory = Path.GetDirectoryName(SETTINGS_PATH);
			EnsureFolder(directory);

			VContainerSettings settings = ScriptableObject.CreateInstance<VContainerSettings>();
			if (prefabScope != null)
			{
				settings.RootLifetimeScope = prefabScope;
			}
			AssetDatabase.CreateAsset(settings, SETTINGS_PATH);
			AssetDatabase.SaveAssetIfDirty(settings);
			return settings;
		}

		private static void EnsurePreloaded(VContainerSettings settings)
		{
			List<Object> preloaded = PlayerSettings.GetPreloadedAssets().ToList();
			bool changed = false;

			int nullsRemoved = preloaded.RemoveAll(asset => asset == null);
			if (nullsRemoved > 0)
			{
				changed = true;
			}

			if (preloaded.Contains(settings) == false)
			{
				preloaded.Add(settings);
				changed = true;
			}

			if (changed == true)
			{
				PlayerSettings.SetPreloadedAssets(preloaded.ToArray());
				AssetDatabase.SaveAssets();
			}
		}

		private static void EnsureFolder(string folderPath)
		{
			if (AssetDatabase.IsValidFolder(folderPath) == true)
			{
				return;
			}

			string parent = Path.GetDirectoryName(folderPath);
			if (string.IsNullOrEmpty(parent) == false && AssetDatabase.IsValidFolder(parent) == false)
			{
				EnsureFolder(parent);
			}

			AssetDatabase.CreateFolder(parent, Path.GetFileName(folderPath));
		}
	}
}
