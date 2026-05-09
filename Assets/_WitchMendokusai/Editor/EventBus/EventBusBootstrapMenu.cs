using UnityEditor;
using UnityEngine;

namespace WitchMendokusai
{
	public static class EventBusBootstrapMenu
	{
		private const string PREFAB_PATH = "Assets/_WitchMendokusai/Core/Resources/Singletons/EventBus.prefab";

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

		[MenuItem("WM/Setup/Recreate EventBus Bootstrap")]
		private static void RecreateMenuItem() => CreateBootstrap();

		private static void CreateBootstrap()
		{
			GameObject root = new GameObject(nameof(EventBus));
			EventBus eventBus = root.AddComponent<EventBus>();

			SerializedObject serializedObject = new SerializedObject(eventBus);
			SerializedProperty dontDestroyProp = serializedObject.FindProperty("dontDestroyOnLoad");
			if (dontDestroyProp != null)
				dontDestroyProp.boolValue = true;
			serializedObject.ApplyModifiedProperties();

			PrefabUtility.SaveAsPrefabAsset(root, PREFAB_PATH);
			Object.DestroyImmediate(root);

			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();

			Debug.Log($"[EventBusBootstrap] Created {PREFAB_PATH}");
		}

		private static void EnsurePrefabFlags()
		{
			GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PREFAB_PATH);
			try
			{
				EventBus eventBus = prefabRoot.GetComponent<EventBus>();
				if (eventBus == null)
					return;

				SerializedObject serializedObject = new SerializedObject(eventBus);
				SerializedProperty dontDestroyProp = serializedObject.FindProperty("dontDestroyOnLoad");
				if (dontDestroyProp == null || dontDestroyProp.boolValue == true)
					return;

				dontDestroyProp.boolValue = true;
				serializedObject.ApplyModifiedProperties();
				PrefabUtility.SaveAsPrefabAsset(prefabRoot, PREFAB_PATH);
				Debug.Log($"[EventBusBootstrap] Updated {PREFAB_PATH} (dontDestroyOnLoad=true)");
			}
			finally
			{
				PrefabUtility.UnloadPrefabContents(prefabRoot);
			}
		}
	}
}
