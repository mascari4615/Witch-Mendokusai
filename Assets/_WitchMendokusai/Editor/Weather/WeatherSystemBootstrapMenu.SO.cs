using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai
{
	// WeatherSystemBootstrapMenu 의 자산 만들기 부분. 같은 클래스의 partial 조각이다. 상태(필드)는 WeatherSystemBootstrapMenu.cs 를 본다.
	public static partial class WeatherSystemBootstrapMenu
	{
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
	}
}
