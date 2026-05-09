using System;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai
{
	// Resources/Weather/{Clear,Cloudy,Rain,Storm,Snow,Fog,Magical}.asset 자동 생성.
	// D3 진입 시 Singletons/WeatherSystem.prefab + Resources/Weather/WeatherTransitionTable.asset
	// 도 같이 자동 생성 예정 (본 메뉴 확장).
	// (TASK-WM-054-D D1)
	public static class WeatherSystemBootstrapMenu
	{
		private const string WEATHER_DIR = "Assets/_WitchMendokusai/Core/Resources/Weather";

		[InitializeOnLoadMethod]
		private static void AutoBootstrapIfMissing()
		{
			CreateMissingWeatherSOs(force: false);
		}

		[MenuItem("WM/Setup/Recreate Weather SOs")]
		private static void RecreateMenuItem() => CreateMissingWeatherSOs(force: true);

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
