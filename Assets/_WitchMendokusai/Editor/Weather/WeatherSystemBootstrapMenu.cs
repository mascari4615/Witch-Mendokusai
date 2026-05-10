using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai
{
	// 자동 생성:
	// - Resources/Weather/{Clear,Cloudy,Rain,Storm,Snow,Fog,Magical}.asset (D1) + visual 노브 (E2-fix)
	// - Resources/Weather/Materials/{Rain,Snow,Fog,Storm}Mat.mat (E2-fix, URP Particle Unlit)
	// - Resources/Weather/Visuals/{Rain,Snow,Fog,Storm}Visual.prefab (E2 — empty ParticleSystem shell, SO 가 정본)
	// - Resources/Weather/WeatherTransitionTable.asset (D2 — 4 hour bucket × 7 weather × 4 season 모동숲 톤 default)
	// - Resources/Singletons/WeatherSystem.prefab + Table reference + dontDestroyOnLoad=true (D3)
	// - Resources/Singletons/WeatherDirector.prefab (E1)
	// (TASK-WM-054-D D1+D2+D3 + sub-E E1+E2+E2-fix)
	public static class WeatherSystemBootstrapMenu
	{
		private const string WEATHER_DIR = "Assets/_WitchMendokusai/Core/Resources/Weather";
		private const string VISUALS_DIR = "Assets/_WitchMendokusai/Core/Resources/Weather/Visuals";
		private const string MATERIALS_DIR = "Assets/_WitchMendokusai/Core/Resources/Weather/Materials";
		private const string TABLE_PATH = "Assets/_WitchMendokusai/Core/Resources/Weather/WeatherTransitionTable.asset";
		private const string PREFAB_PATH = "Assets/_WitchMendokusai/Core/Resources/Singletons/WeatherSystem.prefab";
		private const string DIRECTOR_PREFAB_PATH = "Assets/_WitchMendokusai/Core/Resources/Singletons/WeatherDirector.prefab";
		private const string SINGLETONS_DIR = "Assets/_WitchMendokusai/Core/Resources/Singletons";
		private const string URP_PARTICLE_SHADER = "Universal Render Pipeline/Particles/Unlit";

		[InitializeOnLoadMethod]
		private static void AutoBootstrapIfMissing()
		{
			CreateMissingMaterials(force: false);
			CreateMissingVisualPrefabs(force: false);
			EnsureVisualPrefabSchema();
			CreateMissingWeatherSOs(force: false);
			EnsureWeatherSOVisualKnobs();
			CreateMissingTransitionTable(force: false);
			EnsureSingletonPrefab();
			EnsureDirectorPrefab();
		}

		[MenuItem("WM/Setup/Recreate Weather SOs")]
		private static void RecreateSOsMenuItem() => CreateMissingWeatherSOs(force: true);

		[MenuItem("WM/Setup/Recreate Weather Transition Table")]
		private static void RecreateTableMenuItem() => CreateMissingTransitionTable(force: true);

		[MenuItem("WM/Setup/Recreate WeatherSystem Prefab")]
		private static void RecreatePrefabMenuItem() => EnsureSingletonPrefab(force: true);

		[MenuItem("WM/Setup/Recreate WeatherDirector Prefab")]
		private static void RecreateDirectorMenuItem() => EnsureDirectorPrefab(force: true);

		[MenuItem("WM/Setup/Recreate Weather Visual Prefabs")]
		private static void RecreateVisualPrefabsMenuItem() => CreateMissingVisualPrefabs(force: true);

		[MenuItem("WM/Setup/Recreate Weather Materials")]
		private static void RecreateMaterialsMenuItem() => CreateMissingMaterials(force: true);

		// ─── E2-fix: URP Particle Material 4 (Rain/Snow/Fog/Storm) ───

		private static void CreateMissingMaterials(bool force)
		{
			EnsureFolder(MATERIALS_DIR);

			Shader urpUnlit = Shader.Find(URP_PARTICLE_SHADER);
			if (urpUnlit == null)
			{
				Debug.LogWarning($"[WeatherSystemBootstrap] '{URP_PARTICLE_SHADER}' shader 미발견 — URP 패키지 import 안 됨? Material 생성 skip");
				return;
			}

			foreach (WeatherType type in VisualWeatherTypes)
			{
				string matPath = $"{MATERIALS_DIR}/{type}Mat.mat";
				Material existing = AssetDatabase.LoadAssetAtPath<Material>(matPath);

				if (existing != null && force == false)
					continue;

				Material mat = new Material(urpUnlit);

				if (existing == null)
				{
					AssetDatabase.CreateAsset(mat, matPath);
					Debug.Log($"[WeatherSystemBootstrap] Created {matPath}");
				}
				else
				{
					AssetDatabase.DeleteAsset(matPath);
					AssetDatabase.CreateAsset(mat, matPath);
					Debug.Log($"[WeatherSystemBootstrap] Recreated {matPath} (force)");
				}
			}

			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();
		}

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

			SerializedProperty visualProp = serializedObject.FindProperty("<VisualPrefab>k__BackingField");
			if (visualProp != null)
			{
				GameObject visualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{VISUALS_DIR}/{type}Visual.prefab");
				visualProp.objectReferenceValue = visualPrefab;
			}

			SerializedProperty idProp = serializedObject.FindProperty("<ID>k__BackingField");
			if (idProp != null)
				idProp.intValue = (int)type;

			ApplyVisualKnobDefaults(serializedObject, type);

			serializedObject.ApplyModifiedProperties();
		}

		// E2-fix: 시각 노브 default — 4 weather × visual 노브 11개. force / 신규 생성 시 모두 박기.
		private static void ApplyVisualKnobDefaults(SerializedObject serializedObject, WeatherType type)
		{
			Material mat = AssetDatabase.LoadAssetAtPath<Material>($"{MATERIALS_DIR}/{type}Mat.mat");
			SetObjectRef(serializedObject, "<ParticleMaterial>k__BackingField", mat);

			switch (type)
			{
				case WeatherType.Rain:
					SetColor(serializedObject, "<ParticleColor>k__BackingField", new Color(0.55f, 0.65f, 0.85f, 0.85f));
					SetFloat(serializedObject, "<ParticleStartSize>k__BackingField", 0.05f);
					SetFloat(serializedObject, "<ParticleStartSpeed>k__BackingField", 18f);
					SetFloat(serializedObject, "<ParticleStartLifetime>k__BackingField", 1.5f);
					SetFloat(serializedObject, "<ParticleEmissionRate>k__BackingField", 400f);
					SetFloat(serializedObject, "<ParticleGravityModifier>k__BackingField", 0.5f);
					SetEnum(serializedObject, "<ParticleShapeType>k__BackingField", (int)ParticleSystemShapeType.Box);
					SetVector3(serializedObject, "<ParticleShapeScale>k__BackingField", new Vector3(40f, 0.1f, 40f));
					SetVector3(serializedObject, "<ParticleShapePosition>k__BackingField", new Vector3(0f, 18f, 0f));
					SetVector3(serializedObject, "<ParticleShapeRotation>k__BackingField", Vector3.zero);
					SetFloat(serializedObject, "<ParticleShapeRadius>k__BackingField", 12f);
					break;

				case WeatherType.Snow:
					SetColor(serializedObject, "<ParticleColor>k__BackingField", new Color(1f, 1f, 1f, 0.9f));
					SetFloat(serializedObject, "<ParticleStartSize>k__BackingField", 0.12f);
					SetFloat(serializedObject, "<ParticleStartSpeed>k__BackingField", 1.5f);
					SetFloat(serializedObject, "<ParticleStartLifetime>k__BackingField", 6f);
					SetFloat(serializedObject, "<ParticleEmissionRate>k__BackingField", 200f);
					SetFloat(serializedObject, "<ParticleGravityModifier>k__BackingField", 0.05f);
					SetEnum(serializedObject, "<ParticleShapeType>k__BackingField", (int)ParticleSystemShapeType.Box);
					SetVector3(serializedObject, "<ParticleShapeScale>k__BackingField", new Vector3(40f, 0.1f, 40f));
					SetVector3(serializedObject, "<ParticleShapePosition>k__BackingField", new Vector3(0f, 18f, 0f));
					SetVector3(serializedObject, "<ParticleShapeRotation>k__BackingField", Vector3.zero);
					SetFloat(serializedObject, "<ParticleShapeRadius>k__BackingField", 12f);
					break;

				case WeatherType.Fog:
					SetColor(serializedObject, "<ParticleColor>k__BackingField", new Color(0.85f, 0.87f, 0.9f, 0.18f));
					SetFloat(serializedObject, "<ParticleStartSize>k__BackingField", 6f);
					SetFloat(serializedObject, "<ParticleStartSpeed>k__BackingField", 0.3f);
					SetFloat(serializedObject, "<ParticleStartLifetime>k__BackingField", 8f);
					SetFloat(serializedObject, "<ParticleEmissionRate>k__BackingField", 12f);
					SetFloat(serializedObject, "<ParticleGravityModifier>k__BackingField", 0f);
					SetEnum(serializedObject, "<ParticleShapeType>k__BackingField", (int)ParticleSystemShapeType.Sphere);
					SetVector3(serializedObject, "<ParticleShapePosition>k__BackingField", new Vector3(0f, 1.5f, 0f));
					SetVector3(serializedObject, "<ParticleShapeRotation>k__BackingField", Vector3.zero);
					SetFloat(serializedObject, "<ParticleShapeRadius>k__BackingField", 12f);
					break;

				case WeatherType.Storm:
					SetColor(serializedObject, "<ParticleColor>k__BackingField", new Color(0.4f, 0.5f, 0.7f, 0.95f));
					SetFloat(serializedObject, "<ParticleStartSize>k__BackingField", 0.07f);
					SetFloat(serializedObject, "<ParticleStartSpeed>k__BackingField", 24f);
					SetFloat(serializedObject, "<ParticleStartLifetime>k__BackingField", 1.2f);
					SetFloat(serializedObject, "<ParticleEmissionRate>k__BackingField", 700f);
					SetFloat(serializedObject, "<ParticleGravityModifier>k__BackingField", 0.7f);
					SetEnum(serializedObject, "<ParticleShapeType>k__BackingField", (int)ParticleSystemShapeType.Box);
					SetVector3(serializedObject, "<ParticleShapeScale>k__BackingField", new Vector3(50f, 0.1f, 50f));
					SetVector3(serializedObject, "<ParticleShapePosition>k__BackingField", new Vector3(0f, 20f, 0f));
					SetVector3(serializedObject, "<ParticleShapeRotation>k__BackingField", Vector3.zero);
					SetFloat(serializedObject, "<ParticleShapeRadius>k__BackingField", 12f);
					break;

				// Clear / Cloudy / Magical = visual 없음 (VisualPrefab null) → 노브 default 0 OK.
			}
		}

		// E2-fix: 기존 SO 의 visual 노브 schema migration. ParticleMaterial null + ParticleStartSize == 0
		// 둘 다 만족 시 = 첫 schema 진입 → type 별 default 박기. 사용자 인스펙터 tweak 보존 (이미 채워진 노브 건들지 X).
		private static void EnsureWeatherSOVisualKnobs()
		{
			foreach (WeatherType type in Enum.GetValues(typeof(WeatherType)))
			{
				string assetPath = $"{WEATHER_DIR}/{type}.asset";
				WeatherSO existing = AssetDatabase.LoadAssetAtPath<WeatherSO>(assetPath);
				if (existing == null)
					continue;

				SerializedObject serializedObject = new SerializedObject(existing);

				SerializedProperty matProp = serializedObject.FindProperty("<ParticleMaterial>k__BackingField");
				SerializedProperty sizeProp = serializedObject.FindProperty("<ParticleStartSize>k__BackingField");

				bool needsMigration = matProp != null
					&& matProp.objectReferenceValue == null
					&& sizeProp != null
					&& sizeProp.floatValue == 0f;

				if (needsMigration == false)
					continue;

				// VisualPrefab 도 null 일 수 있어 같이 채움.
				SerializedProperty visualProp = serializedObject.FindProperty("<VisualPrefab>k__BackingField");
				if (visualProp != null && visualProp.objectReferenceValue == null)
				{
					GameObject visualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{VISUALS_DIR}/{type}Visual.prefab");
					visualProp.objectReferenceValue = visualPrefab;
				}

				ApplyVisualKnobDefaults(serializedObject, type);
				serializedObject.ApplyModifiedProperties();
				EditorUtility.SetDirty(existing);
				Debug.Log($"[WeatherSystemBootstrap] Migrated visual knobs → {assetPath}");
			}
			AssetDatabase.SaveAssets();
		}

		// ─── D2: TransitionTable .asset (4 hour bucket × 7 weather, 모동숲 톤) ───

		private static void CreateMissingTransitionTable(bool force)
		{
			EnsureFolder(WEATHER_DIR);

			WeatherTransitionTableSO existing = AssetDatabase.LoadAssetAtPath<WeatherTransitionTableSO>(TABLE_PATH);

			if (existing != null && force == false)
			{
				// schema migration — Profiles 비어진 .asset (옛 schema 깨짐) 자동 재초기화
				if (existing.Profiles == null || existing.Profiles.Count == 0)
				{
					InitializeTable(existing);
					EditorUtility.SetDirty(existing);
					AssetDatabase.SaveAssets();
					Debug.Log($"[WeatherSystemBootstrap] Re-initialized {TABLE_PATH} (schema migration — 4 hour bucket → 16 season×hour profile)");
				}
				return;
			}

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

			// 모동숲/스타듀 톤 default — 계절 4 × hour bucket 4 = 16 profile.
			// Spring (0)
			AddProfile(profilesProp, 0, 0, SpringDawn());
			AddProfile(profilesProp, 0, 1, SpringMorning());
			AddProfile(profilesProp, 0, 2, SpringAfternoon());
			AddProfile(profilesProp, 0, 3, SpringNight());
			// Summer (1)
			AddProfile(profilesProp, 1, 0, SummerDawn());
			AddProfile(profilesProp, 1, 1, SummerMorning());
			AddProfile(profilesProp, 1, 2, SummerAfternoon());
			AddProfile(profilesProp, 1, 3, SummerNight());
			// Autumn (2)
			AddProfile(profilesProp, 2, 0, AutumnDawn());
			AddProfile(profilesProp, 2, 1, AutumnMorning());
			AddProfile(profilesProp, 2, 2, AutumnAfternoon());
			AddProfile(profilesProp, 2, 3, AutumnNight());
			// Winter (3)
			AddProfile(profilesProp, 3, 0, WinterDawn());
			AddProfile(profilesProp, 3, 1, WinterMorning());
			AddProfile(profilesProp, 3, 2, WinterAfternoon());
			AddProfile(profilesProp, 3, 3, WinterNight());

			serializedObject.ApplyModifiedProperties();
		}

		private static void AddProfile(SerializedProperty profilesProp, int season, int hourBucket, List<(WeatherType type, float weight)> weights)
		{
			int profileIndex = profilesProp.arraySize;
			profilesProp.InsertArrayElementAtIndex(profileIndex);

			SerializedProperty profileElement = profilesProp.GetArrayElementAtIndex(profileIndex);
			profileElement.FindPropertyRelative("Season").intValue = season;
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

		// ─── Spring (0) — 봄, 비/구름 잦음 ───
		private static List<(WeatherType type, float weight)> SpringDawn() => new()
		{
			(WeatherType.Cloudy, 0.40f), (WeatherType.Clear, 0.30f), (WeatherType.Fog, 0.20f), (WeatherType.Rain, 0.10f),
		};
		private static List<(WeatherType type, float weight)> SpringMorning() => new()
		{
			(WeatherType.Clear, 0.55f), (WeatherType.Cloudy, 0.30f), (WeatherType.Rain, 0.15f),
		};
		private static List<(WeatherType type, float weight)> SpringAfternoon() => new()
		{
			(WeatherType.Clear, 0.50f), (WeatherType.Cloudy, 0.30f), (WeatherType.Rain, 0.15f), (WeatherType.Storm, 0.05f),
		};
		private static List<(WeatherType type, float weight)> SpringNight() => new()
		{
			(WeatherType.Cloudy, 0.40f), (WeatherType.Clear, 0.35f), (WeatherType.Rain, 0.20f), (WeatherType.Fog, 0.05f),
		};

		// ─── Summer (1) — 여름, 맑음 + 강한 storm ───
		private static List<(WeatherType type, float weight)> SummerDawn() => new()
		{
			(WeatherType.Clear, 0.50f), (WeatherType.Cloudy, 0.30f), (WeatherType.Fog, 0.15f), (WeatherType.Storm, 0.05f),
		};
		private static List<(WeatherType type, float weight)> SummerMorning() => new()
		{
			(WeatherType.Clear, 0.70f), (WeatherType.Cloudy, 0.20f), (WeatherType.Storm, 0.10f),
		};
		private static List<(WeatherType type, float weight)> SummerAfternoon() => new()
		{
			(WeatherType.Clear, 0.60f), (WeatherType.Cloudy, 0.20f), (WeatherType.Storm, 0.20f),
		};
		private static List<(WeatherType type, float weight)> SummerNight() => new()
		{
			(WeatherType.Clear, 0.50f), (WeatherType.Cloudy, 0.30f), (WeatherType.Storm, 0.15f), (WeatherType.Rain, 0.05f),
		};

		// ─── Autumn (2) — 가을, 흐림/비/안개 ───
		private static List<(WeatherType type, float weight)> AutumnDawn() => new()
		{
			(WeatherType.Cloudy, 0.40f), (WeatherType.Fog, 0.30f), (WeatherType.Rain, 0.20f), (WeatherType.Clear, 0.10f),
		};
		private static List<(WeatherType type, float weight)> AutumnMorning() => new()
		{
			(WeatherType.Cloudy, 0.40f), (WeatherType.Clear, 0.30f), (WeatherType.Rain, 0.20f), (WeatherType.Fog, 0.10f),
		};
		private static List<(WeatherType type, float weight)> AutumnAfternoon() => new()
		{
			(WeatherType.Cloudy, 0.40f), (WeatherType.Rain, 0.30f), (WeatherType.Clear, 0.20f), (WeatherType.Fog, 0.10f),
		};
		private static List<(WeatherType type, float weight)> AutumnNight() => new()
		{
			(WeatherType.Cloudy, 0.40f), (WeatherType.Rain, 0.30f), (WeatherType.Fog, 0.20f), (WeatherType.Storm, 0.10f),
		};

		// ─── Winter (3) — 겨울, 눈 위주 ───
		private static List<(WeatherType type, float weight)> WinterDawn() => new()
		{
			(WeatherType.Snow, 0.50f), (WeatherType.Cloudy, 0.30f), (WeatherType.Clear, 0.20f),
		};
		private static List<(WeatherType type, float weight)> WinterMorning() => new()
		{
			(WeatherType.Snow, 0.40f), (WeatherType.Cloudy, 0.30f), (WeatherType.Clear, 0.30f),
		};
		private static List<(WeatherType type, float weight)> WinterAfternoon() => new()
		{
			(WeatherType.Snow, 0.35f), (WeatherType.Cloudy, 0.35f), (WeatherType.Clear, 0.30f),
		};
		private static List<(WeatherType type, float weight)> WinterNight() => new()
		{
			(WeatherType.Snow, 0.50f), (WeatherType.Cloudy, 0.30f), (WeatherType.Storm, 0.15f), (WeatherType.Clear, 0.05f),
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
			// WeatherHUD 제거 — TASK-WM-096 (DevWindow TimeWeatherMode 로 마이그)

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

				bool changed = false;

				// WeatherHUD ensure 제거 — TASK-WM-096 (DevWindow TimeWeatherMode 로 마이그)

				SerializedObject serializedObject = new SerializedObject(weatherSystem);

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

		// ─── E1: WeatherDirector prefab ───

		private static void EnsureDirectorPrefab(bool force = false)
		{
			EnsureFolder(SINGLETONS_DIR);

			GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(DIRECTOR_PREFAB_PATH);
			if (existing != null && force == false)
			{
				EnsureDirectorFlags();
				return;
			}

			GameObject root = new GameObject(nameof(WeatherDirector));
			WeatherDirector director = root.AddComponent<WeatherDirector>();

			SerializedObject serializedObject = new SerializedObject(director);
			SerializedProperty dontDestroyProp = serializedObject.FindProperty("dontDestroyOnLoad");
			if (dontDestroyProp != null)
				dontDestroyProp.boolValue = true;
			serializedObject.ApplyModifiedProperties();

			PrefabUtility.SaveAsPrefabAsset(root, DIRECTOR_PREFAB_PATH);
			UnityEngine.Object.DestroyImmediate(root);

			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();

			Debug.Log($"[WeatherSystemBootstrap] Created {DIRECTOR_PREFAB_PATH}");
		}

		private static void EnsureDirectorFlags()
		{
			GameObject prefabRoot = PrefabUtility.LoadPrefabContents(DIRECTOR_PREFAB_PATH);
			try
			{
				WeatherDirector director = prefabRoot.GetComponent<WeatherDirector>();
				if (director == null)
					return;

				SerializedObject serializedObject = new SerializedObject(director);
				SerializedProperty dontDestroyProp = serializedObject.FindProperty("dontDestroyOnLoad");
				if (dontDestroyProp == null || dontDestroyProp.boolValue == true)
					return;

				dontDestroyProp.boolValue = true;
				serializedObject.ApplyModifiedProperties();
				PrefabUtility.SaveAsPrefabAsset(prefabRoot, DIRECTOR_PREFAB_PATH);
				Debug.Log($"[WeatherSystemBootstrap] Updated {DIRECTOR_PREFAB_PATH} (dontDestroyOnLoad=true)");
			}
			finally
			{
				PrefabUtility.UnloadPrefabContents(prefabRoot);
			}
		}

		// ─── E2: Visual prefab 4 (Rain/Snow/Fog/Storm) — empty ParticleSystem shell ───
		// E2-fix: SO 가 visual 정본. prefab 은 main.loop=true / playOnAwake=true / simulationSpace=World 만 박은 shell.

		private static readonly WeatherType[] VisualWeatherTypes =
		{
			WeatherType.Rain,
			WeatherType.Snow,
			WeatherType.Fog,
			WeatherType.Storm,
		};

		private static void CreateMissingVisualPrefabs(bool force)
		{
			EnsureFolder(VISUALS_DIR);

			foreach (WeatherType type in VisualWeatherTypes)
			{
				string prefabPath = $"{VISUALS_DIR}/{type}Visual.prefab";
				GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

				if (existing != null && force == false)
					continue;

				GameObject root = new GameObject($"{type}Visual");
				ParticleSystem particleSystem = root.AddComponent<ParticleSystem>();
				ConfigureParticleEmpty(particleSystem);

				PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
				UnityEngine.Object.DestroyImmediate(root);

				Debug.Log($"[WeatherSystemBootstrap] {(force == true ? "Recreated" : "Created")} {prefabPath}");
			}

			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();
		}

		// E2-fix: empty ParticleSystem shell — main.loop / playOnAwake / simulationSpace 만 박고 나머지 모두 SO 가 set.
		private static void ConfigureParticleEmpty(ParticleSystem particleSystem)
		{
			ParticleSystem.MainModule main = particleSystem.main;
			main.loop = true;
			main.playOnAwake = true;
			main.simulationSpace = ParticleSystemSimulationSpace.World;
		}

		// E2-fix: 옛 schema (ConfigureParticle 박힌 prefab — shape.rotation == (90,0,0)) → empty shell migration.
		private static void EnsureVisualPrefabSchema()
		{
			foreach (WeatherType type in VisualWeatherTypes)
			{
				string prefabPath = $"{VISUALS_DIR}/{type}Visual.prefab";
				GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
				if (prefabRoot == null)
					continue;
				try
				{
					ParticleSystem particleSystem = prefabRoot.GetComponent<ParticleSystem>();
					if (particleSystem == null)
						continue;

					ParticleSystem.ShapeModule shape = particleSystem.shape;

					// rotation X 가 0.01 보다 크면 옛 schema (90도 회전 박힘) — empty shell 로 reset
					if (Mathf.Abs(shape.rotation.x) < 0.01f)
						continue;

					ConfigureParticleEmpty(particleSystem);

					// shape / emission / 나머지 모듈 모두 default reset — SO 가 spawn 시 set
					ParticleSystem.EmissionModule emission = particleSystem.emission;
					emission.rateOverTime = 0f;
					shape.rotation = Vector3.zero;
					shape.scale = Vector3.one;
					shape.position = Vector3.zero;
					shape.shapeType = ParticleSystemShapeType.Cone;

					ParticleSystem.MainModule main = particleSystem.main;
					main.startLifetime = 5f;
					main.startSpeed = 5f;
					main.startSize = 1f;
					main.startColor = Color.white;
					main.gravityModifier = 0f;

					PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
					Debug.Log($"[WeatherSystemBootstrap] Migrated {prefabPath} → empty shell (E2-fix schema)");
				}
				finally
				{
					PrefabUtility.UnloadPrefabContents(prefabRoot);
				}
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

		// ─── SerializedProperty setter helpers (E2-fix) ───

		private static void SetFloat(SerializedObject serializedObject, string path, float value)
		{
			SerializedProperty property = serializedObject.FindProperty(path);
			if (property != null)
				property.floatValue = value;
		}

		private static void SetColor(SerializedObject serializedObject, string path, Color value)
		{
			SerializedProperty property = serializedObject.FindProperty(path);
			if (property != null)
				property.colorValue = value;
		}

		private static void SetVector3(SerializedObject serializedObject, string path, Vector3 value)
		{
			SerializedProperty property = serializedObject.FindProperty(path);
			if (property != null)
				property.vector3Value = value;
		}

		private static void SetEnum(SerializedObject serializedObject, string path, int value)
		{
			SerializedProperty property = serializedObject.FindProperty(path);
			if (property == null)
			{
				Debug.LogError($"[WeatherSystemBootstrap] SerializedProperty 누락 — path='{path}'");
				return;
			}
			property.intValue = value;
		}

		private static void SetObjectRef(SerializedObject serializedObject, string path, UnityEngine.Object value)
		{
			SerializedProperty property = serializedObject.FindProperty(path);
			if (property != null)
				property.objectReferenceValue = value;
		}
	}
}
