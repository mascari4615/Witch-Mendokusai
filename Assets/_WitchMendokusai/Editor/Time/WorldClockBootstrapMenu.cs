using UnityEditor;
using UnityEngine;

namespace WitchMendokusai
{
	// WorldClockSO.asset + WorldClock.prefab (with WorldClockHUD) 자동 생성.
	// Domain Reload 시 missing 검사 + 누락이면 자동 생성. (TASK-WM-054-A)
	public static class WorldClockBootstrapMenu
	{
		private const string SO_PATH = "Assets/_WitchMendokusai/Core/Resources/WorldClockSO.asset";
		private const string PREFAB_PATH = "Assets/_WitchMendokusai/Core/Resources/Singletons/WorldClock.prefab";

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

		[MenuItem("WM/Setup/Recreate WorldClock Bootstrap")]
		private static void RecreateMenuItem() => CreateBootstrap();

		private static void CreateBootstrap()
		{
			WorldClockSO worldClockSO = AssetDatabase.LoadAssetAtPath<WorldClockSO>(SO_PATH);
			if (worldClockSO == null)
			{
				worldClockSO = ScriptableObject.CreateInstance<WorldClockSO>();
				AssetDatabase.CreateAsset(worldClockSO, SO_PATH);
				Debug.Log($"[WorldClockBootstrap] Created {SO_PATH}");
			}

			GameObject root = new GameObject(nameof(WorldClock));
			WorldClock worldClock = root.AddComponent<WorldClock>();
			// WorldClockHUD 제거 — TASK-WM-096 (DevWindow TimeWeatherMode 로 마이그)

			SerializedObject serializedObject = new SerializedObject(worldClock);

			SerializedProperty configProperty = serializedObject.FindProperty("<Config>k__BackingField");
			if (configProperty != null)
				configProperty.objectReferenceValue = worldClockSO;

			SerializedProperty dontDestroyProp = serializedObject.FindProperty("dontDestroyOnLoad");
			if (dontDestroyProp != null)
				dontDestroyProp.boolValue = true;

			serializedObject.ApplyModifiedProperties();

			PrefabUtility.SaveAsPrefabAsset(root, PREFAB_PATH);
			Object.DestroyImmediate(root);

			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();

			Debug.Log($"[WorldClockBootstrap] Created {PREFAB_PATH}");
		}

		// 기존 prefab 의 SerializedField 보정 (dontDestroyOnLoad 등). idempotent.
		private static void EnsurePrefabFlags()
		{
			GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PREFAB_PATH);
			try
			{
				WorldClock worldClock = prefabRoot.GetComponent<WorldClock>();
				if (worldClock == null)
					return;

				SerializedObject serializedObject = new SerializedObject(worldClock);
				SerializedProperty dontDestroyProp = serializedObject.FindProperty("dontDestroyOnLoad");
				if (dontDestroyProp == null || dontDestroyProp.boolValue == true)
					return;

				dontDestroyProp.boolValue = true;
				serializedObject.ApplyModifiedProperties();
				PrefabUtility.SaveAsPrefabAsset(prefabRoot, PREFAB_PATH);
				Debug.Log($"[WorldClockBootstrap] Updated {PREFAB_PATH} (dontDestroyOnLoad=true)");
			}
			finally
			{
				PrefabUtility.UnloadPrefabContents(prefabRoot);
			}
		}
	}
}
