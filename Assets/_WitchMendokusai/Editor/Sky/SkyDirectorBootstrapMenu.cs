using UnityEditor;
using UnityEngine;

namespace WitchMendokusai
{
	// SkyPresetSO + SkyboxGradient material + SkyDirector prefab 자동 생성.
	// Domain Reload 시 missing 검사 → 자동 부트스트랩. (TASK-WM-054-C C1)
	public static class SkyDirectorBootstrapMenu
	{
		private const string PRESET_PATH = "Assets/_WitchMendokusai/Core/Resources/SkyPreset_AnimalCrossing.asset";
		private const string MATERIAL_PATH = "Assets/_WitchMendokusai/Core/Resources/SkyboxGradient.mat";
		private const string PREFAB_PATH = "Assets/_WitchMendokusai/Core/Resources/Singletons/SkyDirector.prefab";
		private const string SHADER_NAME = "WM/SkyboxGradient";

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

		[MenuItem("WM/Setup/Recreate SkyDirector Bootstrap")]
		private static void RecreateMenuItem() => CreateBootstrap();

		private static void CreateBootstrap()
		{
			Shader shader = Shader.Find(SHADER_NAME);
			if (shader == null)
			{
				Debug.LogWarning($"[SkyDirectorBootstrap] Shader '{SHADER_NAME}' 미발견. Shader 컴파일 후 재시도 (다음 Domain Reload).");
				return;
			}

			SkyPresetSO preset = AssetDatabase.LoadAssetAtPath<SkyPresetSO>(PRESET_PATH);
			if (preset == null)
			{
				preset = ScriptableObject.CreateInstance<SkyPresetSO>();
				AssetDatabase.CreateAsset(preset, PRESET_PATH);
				Debug.Log($"[SkyDirectorBootstrap] Created {PRESET_PATH}");
			}

			Material material = AssetDatabase.LoadAssetAtPath<Material>(MATERIAL_PATH);
			if (material == null)
			{
				material = new Material(shader);
				AssetDatabase.CreateAsset(material, MATERIAL_PATH);
				Debug.Log($"[SkyDirectorBootstrap] Created {MATERIAL_PATH}");
			}

			GameObject root = new GameObject(nameof(SkyDirector));
			SkyDirector skyDirector = root.AddComponent<SkyDirector>();

			SerializedObject serializedObject = new SerializedObject(skyDirector);

			SerializedProperty presetProp = serializedObject.FindProperty("<ActivePreset>k__BackingField");
			if (presetProp != null)
				presetProp.objectReferenceValue = preset;

			SerializedProperty materialProp = serializedObject.FindProperty("<SkyboxMaterial>k__BackingField");
			if (materialProp != null)
				materialProp.objectReferenceValue = material;

			SerializedProperty dontDestroyProp = serializedObject.FindProperty("dontDestroyOnLoad");
			if (dontDestroyProp != null)
				dontDestroyProp.boolValue = true;

			serializedObject.ApplyModifiedProperties();

			PrefabUtility.SaveAsPrefabAsset(root, PREFAB_PATH);
			Object.DestroyImmediate(root);

			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();

			Debug.Log($"[SkyDirectorBootstrap] Created {PREFAB_PATH}");
		}

		private static void EnsurePrefabFlags()
		{
			GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PREFAB_PATH);
			try
			{
				SkyDirector skyDirector = prefabRoot.GetComponent<SkyDirector>();
				if (skyDirector == null)
					return;

				SerializedObject serializedObject = new SerializedObject(skyDirector);
				SerializedProperty dontDestroyProp = serializedObject.FindProperty("dontDestroyOnLoad");
				if (dontDestroyProp == null || dontDestroyProp.boolValue == true)
					return;

				dontDestroyProp.boolValue = true;
				serializedObject.ApplyModifiedProperties();
				PrefabUtility.SaveAsPrefabAsset(prefabRoot, PREFAB_PATH);
				Debug.Log($"[SkyDirectorBootstrap] Updated {PREFAB_PATH} (dontDestroyOnLoad=true)");
			}
			finally
			{
				PrefabUtility.UnloadPrefabContents(prefabRoot);
			}
		}
	}
}
