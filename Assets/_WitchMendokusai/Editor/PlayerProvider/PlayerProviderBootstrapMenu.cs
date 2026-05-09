using UnityEditor;
using UnityEngine;

namespace WitchMendokusai
{
	public static class PlayerProviderBootstrapMenu
	{
		private const string PREFAB_PATH = "Assets/_WitchMendokusai/Core/Resources/Singletons/PlayerProvider.prefab";

		[InitializeOnLoadMethod]
		private static void AutoBootstrapIfMissing()
		{
			if (AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH) == null)
			{
				CreateBootstrap();
				return;
			}

			EnsurePrefabFlags();
		}

		[MenuItem("WM/Setup/Recreate PlayerProvider Bootstrap")]
		private static void RecreateMenuItem() => CreateBootstrap();

		private static void CreateBootstrap()
		{
			GameObject root = new GameObject(nameof(PlayerProvider));
			PlayerProvider playerProvider = root.AddComponent<PlayerProvider>();

			SerializedObject serializedObject = new SerializedObject(playerProvider);
			SerializedProperty dontDestroyProp = serializedObject.FindProperty("dontDestroyOnLoad");
			if (dontDestroyProp != null)
				dontDestroyProp.boolValue = true;
			serializedObject.ApplyModifiedProperties();

			PrefabUtility.SaveAsPrefabAsset(root, PREFAB_PATH);
			Object.DestroyImmediate(root);

			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();

			Debug.Log($"[PlayerProviderBootstrap] Created {PREFAB_PATH}");
		}

		private static void EnsurePrefabFlags()
		{
			GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PREFAB_PATH);
			try
			{
				PlayerProvider playerProvider = prefabRoot.GetComponent<PlayerProvider>();
				if (playerProvider == null)
					return;

				SerializedObject serializedObject = new SerializedObject(playerProvider);
				SerializedProperty dontDestroyProp = serializedObject.FindProperty("dontDestroyOnLoad");
				if (dontDestroyProp == null || dontDestroyProp.boolValue == true)
					return;

				dontDestroyProp.boolValue = true;
				serializedObject.ApplyModifiedProperties();
				PrefabUtility.SaveAsPrefabAsset(prefabRoot, PREFAB_PATH);
				Debug.Log($"[PlayerProviderBootstrap] Updated {PREFAB_PATH} (dontDestroyOnLoad=true)");
			}
			finally
			{
				PrefabUtility.UnloadPrefabContents(prefabRoot);
			}
		}
	}
}
