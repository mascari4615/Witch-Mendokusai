using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai
{
	// WeatherSystemBootstrapMenu 의 프리팹 만들기 부분. 같은 클래스의 partial 조각이다. 상태(필드)는 WeatherSystemBootstrapMenu.cs 를 본다.
	public static partial class WeatherSystemBootstrapMenu
	{
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
	}
}
