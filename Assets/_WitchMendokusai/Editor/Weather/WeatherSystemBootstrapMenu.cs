using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai
{
	// 자동 생성:
	// - Resources/Weather/{Clear,Cloudy,Rain,Storm,Snow,Fog,Magical}.asset (D1)
	// - Resources/Weather/WeatherTransitionTable.asset (D2 — 4 hour bucket × 7 weather 모동숲 톤 default)
	// - Resources/Singletons/WeatherSystem.prefab + Table reference + dontDestroyOnLoad=true (D3)
	// (TASK-WM-054-D D1+D2+D3)
	public static class WeatherSystemBootstrapMenu
	{
		private const string WEATHER_DIR = "Assets/_WitchMendokusai/Core/Resources/Weather";
		private const string TABLE_PATH = "Assets/_WitchMendokusai/Core/Resources/Weather/WeatherTransitionTable.asset";
		private const string PREFAB_PATH = "Assets/_WitchMendokusai/Core/Resources/Singletons/WeatherSystem.prefab";
		private const string SINGLETONS_DIR = "Assets/_WitchMendokusai/Core/Resources/Singletons";

		[InitializeOnLoadMethod]
		private static void AutoBootstrapIfMissing()
		{
			CreateMissingWeatherSOs(force: false);
			CreateMissingTransitionTable(force: false);
			EnsureSingletonPrefab();
		}

		[MenuItem("WM/Setup/Recreate Weather SOs")]
		private static void RecreateSOsMenuItem() => CreateMissingWeatherSOs(force: true);

		[MenuItem("WM/Setup/Recreate Weather Transition Table")]
		private static void RecreateTableMenuItem() => CreateMissingTransitionTable(force: true);

		[MenuItem("WM/Setup/Recreate WeatherSystem Prefab")]
		private static void RecreatePrefabMenuItem() => EnsureSingletonPrefab(force: true);

		// ─── D1: WeatherSO 7 .asset ───

		private static void CreateMissingWeatherSOs(bool force)
		{
			EnsureFolder(WEATHER_DIR);

			foreach (WeatherType type in Enum.GetValues(typeof(WeatherType)))
			{
				string assetPath = $"{WEATHER_DIR}/{type}.asset";
				WeatherSO existing = AssetDatabase.LoadAssetAtPath<WeatherSO>(assetPath);

				if (existing != null && force == false)
					continue;

				if (existing == null)
				{
					WeatherSO weather = ScriptableObject.CreateInstance<WeatherSO>();
					InitializeSO(weather, type);
					AssetDatabase.CreateAsset(weather, assetPath);
					Debug.Log($"[WeatherSystemBootstrap] Created {assetPath}");
				}
				else if (force == true)
				{
					InitializeSO(existing, type);
					EditorUtility.SetDirty(existing);
					Debug.Log($"[WeatherSystemBootstrap] Recreated {assetPath} (force)");
				}
			}

			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();
		}

		private static void InitializeSO(WeatherSO weather, WeatherType type)
		{
			SerializedObject serializedObject = new SerializedObject(weather);

			SerializedProperty typeProp = serializedObject.FindProperty("<Type>k__BackingField");
			if (typeProp != null)
				typeProp.enumValueIndex = (int)type;

			SerializedProperty isWetProp = serializedObject.FindProperty("<IsWet>k__BackingField");
			if (isWetProp != null)
				isWetProp.boolValue = (type == WeatherType.Rain || type == WeatherType.Storm);

			SerializedProperty tintProp = serializedObject.FindProperty("<DebugTint>k__BackingField");
			if (tintProp != null)
				tintProp.colorValue = GetDefaultTint(type);

			SerializedProperty sfxProp = serializedObject.FindProperty("<SfxKey>k__BackingField");
			if (sfxProp != null)
				sfxProp.stringValue = $"weather_{type.ToString().ToLowerInvariant()}";

			SerializedProperty nameProp = serializedObject.FindProperty("<Name>k__BackingField");
			if (nameProp != null)
				nameProp.stringValue = type.ToString();

			SerializedProperty idProp = serializedObject.FindProperty("<ID>k__BackingField");
			if (idProp != null)
				idProp.intValue = (int)type;

			serializedObject.ApplyModifiedProperties();
		}

		// ─── D2: TransitionTable .asset (4 hour bucket × 7 weather, 모동숲 톤) ───

		private static void CreateMissingTransitionTable(bool force)
		{
			EnsureFolder(WEATHER_DIR);

			WeatherTransitionTableSO existing = AssetDatabase.LoadAssetAtPath<WeatherTransitionTableSO>(TABLE_PATH);

			if (existing != null && force == false)
				return;

			if (existing == null)
			{
				WeatherTransitionTableSO table = ScriptableObject.CreateInstance<WeatherTransitionTableSO>();
				InitializeTable(table);
				AssetDatabase.CreateAsset(table, TABLE_PATH);
				Debug.Log($"[WeatherSystemBootstrap] Created {TABLE_PATH}");
			}
			else
			{
				InitializeTable(existing);
				EditorUtility.SetDirty(existing);
				Debug.Log($"[WeatherSystemBootstrap] Recreated {TABLE_PATH} (force)");
			}

			AssetDatabase.SaveAssets();
		}

		private static void InitializeTable(WeatherTransitionTableSO table)
		{
			SerializedObject serializedObject = new SerializedObject(table);
			SerializedProperty profilesProp = serializedObject.FindProperty("<Profiles>k__BackingField");
			if (profilesProp == null)
				return;

			profilesProp.ClearArray();

			// 모동숲 톤 default — 시간대 만 (계절 축은 후속).
			AddProfile(profilesProp, 0, DawnDefault());        // Dawn (4-7)
			AddProfile(profilesProp, 1, MorningDefault());     // Morning (7-12)
			AddProfile(profilesProp, 2, AfternoonDefault());   // Afternoon (12-17)
			AddProfile(profilesProp, 3, NightDefault());       // Night (17-24+0-4)

			serializedObject.ApplyModifiedProperties();
		}

		private static void AddProfile(SerializedProperty profilesProp, int hourBucket, List<(WeatherType type, float weight)> weights)
		{
			int profileIndex = profilesProp.arraySize;
			profilesProp.InsertArrayElementAtIndex(profileIndex);

			SerializedProperty profileElement = profilesProp.GetArrayElementAtIndex(profileIndex);
			profileElement.FindPropertyRelative("HourBucket").intValue = hourBucket;

			SerializedProperty weightsProp = profileElement.FindPropertyRelative("Weights");
			weightsProp.ClearArray();

			for (int weightIndex = 0; weightIndex < weights.Count; weightIndex++)
			{
				weightsProp.InsertArrayElementAtIndex(weightIndex);
				SerializedProperty entry = weightsProp.GetArrayElementAtIndex(weightIndex);
				entry.FindPropertyRelative("Type").enumValueIndex = (int)weights[weightIndex].type;
				entry.FindPropertyRelative("Weight").floatValue = weights[weightIndex].weight;
			}
		}

		private static List<(WeatherType type, float weight)> DawnDefault() => new()
		{
			(WeatherType.Clear, 0.40f),
			(WeatherType.Cloudy, 0.30f),
			(WeatherType.Fog, 0.20f),
			(WeatherType.Rain, 0.10f),
		};

		private static List<(WeatherType type, float weight)> MorningDefault() => new()
		{
			(WeatherType.Clear, 0.60f),
			(WeatherType.Cloudy, 0.25f),
			(WeatherType.Rain, 0.10f),
			(WeatherType.Fog, 0.05f),
		};

		private static List<(WeatherType type, float weight)> AfternoonDefault() => new()
		{
			(WeatherType.Clear, 0.55f),
			(WeatherType.Cloudy, 0.30f),
			(WeatherType.Rain, 0.10f),
			(WeatherType.Storm, 0.05f),
		};

		private static List<(WeatherType type, float weight)> NightDefault() => new()
		{
			(WeatherType.Clear, 0.40f),
			(WeatherType.Cloudy, 0.35f),
			(WeatherType.Rain, 0.15f),
			(WeatherType.Snow, 0.05f),
			(WeatherType.Fog, 0.05f),
		};

		// ─── D3: WeatherSystem prefab ───

		private static void EnsureSingletonPrefab(bool force = false)
		{
			EnsureFolder(SINGLETONS_DIR);

			GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH);
			if (existing != null && force == false)
			{
				EnsurePrefabFlags();
				return;
			}

			GameObject root = new GameObject(nameof(WeatherSystem));
			WeatherSystem weatherSystem = root.AddComponent<WeatherSystem>();

			SerializedObject serializedObject = new SerializedObject(weatherSystem);

			SerializedProperty tableProp = serializedObject.FindProperty("<Table>k__BackingField");
			if (tableProp != null)
			{
				WeatherTransitionTableSO table = AssetDatabase.LoadAssetAtPath<WeatherTransitionTableSO>(TABLE_PATH);
				if (table != null)
					tableProp.objectReferenceValue = table;
			}

			SerializedProperty dontDestroyProp = serializedObject.FindProperty("dontDestroyOnLoad");
			if (dontDestroyProp != null)
				dontDestroyProp.boolValue = true;

			serializedObject.ApplyModifiedProperties();

			PrefabUtility.SaveAsPrefabAsset(root, PREFAB_PATH);
			UnityEngine.Object.DestroyImmediate(root);

			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();

			Debug.Log($"[WeatherSystemBootstrap] Created {PREFAB_PATH}");
		}

		private static void EnsurePrefabFlags()
		{
			GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PREFAB_PATH);
			try
			{
				WeatherSystem weatherSystem = prefabRoot.GetComponent<WeatherSystem>();
				if (weatherSystem == null)
					return;

				SerializedObject serializedObject = new SerializedObject(weatherSystem);
				bool changed = false;

				SerializedProperty dontDestroyProp = serializedObject.FindProperty("dontDestroyOnLoad");
				if (dontDestroyProp != null && dontDestroyProp.boolValue == false)
				{
					dontDestroyProp.boolValue = true;
					changed = true;
				}

				SerializedProperty tableProp = serializedObject.FindProperty("<Table>k__BackingField");
				if (tableProp != null && tableProp.objectReferenceValue == null)
				{
					WeatherTransitionTableSO table = AssetDatabase.LoadAssetAtPath<WeatherTransitionTableSO>(TABLE_PATH);
					if (table != null)
					{
						tableProp.objectReferenceValue = table;
						changed = true;
					}
				}

				if (changed == false)
					return;

				serializedObject.ApplyModifiedProperties();
				PrefabUtility.SaveAsPrefabAsset(prefabRoot, PREFAB_PATH);
				Debug.Log($"[WeatherSystemBootstrap] Updated {PREFAB_PATH}");
			}
			finally
			{
				PrefabUtility.UnloadPrefabContents(prefabRoot);
			}
		}

		// ─── helpers ───

		private static void EnsureFolder(string path)
		{
			if (AssetDatabase.IsValidFolder(path) == true)
				return;

			string parent = System.IO.Path.GetDirectoryName(path).Replace("\\", "/");
			string folder = System.IO.Path.GetFileName(path);
			EnsureFolder(parent);
			AssetDatabase.CreateFolder(parent, folder);
		}

		private static Color GetDefaultTint(WeatherType type)
		{
			switch (type)
			{
				case WeatherType.Clear: return new Color(1f, 0.95f, 0.7f);
				case WeatherType.Cloudy: return new Color(0.7f, 0.75f, 0.85f);
				case WeatherType.Rain: return new Color(0.4f, 0.55f, 0.75f);
				case WeatherType.Storm: return new Color(0.25f, 0.3f, 0.5f);
				case WeatherType.Snow: return new Color(0.9f, 0.95f, 1f);
				case WeatherType.Fog: return new Color(0.8f, 0.82f, 0.85f);
				case WeatherType.Magical: return new Color(0.7f, 0.5f, 1f);
				default: return Color.white;
			}
		}
	}
}
