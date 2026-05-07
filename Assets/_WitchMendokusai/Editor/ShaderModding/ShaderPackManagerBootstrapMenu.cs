using UnityEditor;
using UnityEngine;

namespace WitchMendokusai
{
	// ShaderPackManager.prefab 자동 생성 + BootstrapSettings.asset 에 자동 wire. (TASK-WM-055)
	public static class ShaderPackManagerBootstrapMenu
	{
		private const string PREFAB_PATH = "Assets/_WitchMendokusai/Core/Resources/Singletons/ShaderPackManager.prefab";
		private const string BOOTSTRAP_SETTINGS_PATH = "Assets/_WitchMendokusai/Core/Resources/BootstrapSettings.asset";
		private const string SHADER_PACK_MANAGER_PROPERTY = "<ShaderPackManagerPrefab>k__BackingField";

		[InitializeOnLoadMethod]
		private static void AutoBootstrapIfMissing()
		{
			GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH);
			BootstrapSettings settings = AssetDatabase.LoadAssetAtPath<BootstrapSettings>(BOOTSTRAP_SETTINGS_PATH);

			if (existingPrefab != null && settings != null && settings.ShaderPackManagerPrefab != null)
				return;

			CreateBootstrap();
		}

		[MenuItem("WM/Setup/Recreate ShaderPackManager Bootstrap")]
		private static void RecreateMenuItem() => CreateBootstrap();

		private static void CreateBootstrap()
		{
			GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH);
			if (prefab == null)
			{
				GameObject root = new GameObject(nameof(ShaderPackManager));
				root.AddComponent<ShaderPackManager>();
				prefab = PrefabUtility.SaveAsPrefabAsset(root, PREFAB_PATH);
				Object.DestroyImmediate(root);
				Debug.Log($"[ShaderPackManagerBootstrap] Created {PREFAB_PATH}");
			}

			BootstrapSettings settings = AssetDatabase.LoadAssetAtPath<BootstrapSettings>(BOOTSTRAP_SETTINGS_PATH);
			if (settings != null && prefab != null)
			{
				ShaderPackManager managerComponent = prefab.GetComponent<ShaderPackManager>();
				SerializedObject serialized = new SerializedObject(settings);
				SerializedProperty property = serialized.FindProperty(SHADER_PACK_MANAGER_PROPERTY);
				if (property != null && property.objectReferenceValue != managerComponent)
				{
					property.objectReferenceValue = managerComponent;
					serialized.ApplyModifiedProperties();
					Debug.Log($"[ShaderPackManagerBootstrap] Wired into {BOOTSTRAP_SETTINGS_PATH}");
				}
			}

			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();
		}
	}
}
