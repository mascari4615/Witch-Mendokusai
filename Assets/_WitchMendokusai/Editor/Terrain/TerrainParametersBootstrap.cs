using UnityEditor;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 활성 TerrainParameters .asset이 Resources에 항상 존재하도록 보장.
	/// 에디터 시작 시 자동 검사 + Terrain Editor가 직접 호출 가능.
	/// </summary>
	[InitializeOnLoad]
	public static class TerrainParametersBootstrap
	{
		public const string ACTIVE_FOLDER_PARENT = "Assets/_WitchMendokusai/Core/Resources";
		public const string ACTIVE_FOLDER = ACTIVE_FOLDER_PARENT + "/Terrain";
		public const string ACTIVE_PATH = ACTIVE_FOLDER + "/TerrainParameters_Active.asset";

		static TerrainParametersBootstrap()
		{
			EditorApplication.delayCall += () => EnsureActive();
		}

		public static TerrainParameters EnsureActive()
		{
			TerrainParameters existing = AssetDatabase.LoadAssetAtPath<TerrainParameters>(ACTIVE_PATH);
			if (existing != null)
				return existing;

			if (AssetDatabase.IsValidFolder(ACTIVE_FOLDER_PARENT) == false)
				AssetDatabase.CreateFolder("Assets/_WitchMendokusai/Core", "Resources");
			if (AssetDatabase.IsValidFolder(ACTIVE_FOLDER) == false)
				AssetDatabase.CreateFolder(ACTIVE_FOLDER_PARENT, "Terrain");

			TerrainParameters created = ScriptableObject.CreateInstance<TerrainParameters>();

			TerrainParameters seed = FindSeedPreset();
			if (seed != null)
			{
				EditorUtility.CopySerialized(seed, created);
				created.name = "TerrainParameters_Active";
			}

			AssetDatabase.CreateAsset(created, ACTIVE_PATH);
			AssetDatabase.SaveAssets();
			TerrainParametersService.ClearCache();
			return created;
		}

		private static TerrainParameters FindSeedPreset()
		{
			string[] guids = AssetDatabase.FindAssets($"t:{nameof(TerrainParameters)}");
			foreach (string guid in guids)
			{
				string path = AssetDatabase.GUIDToAssetPath(guid);
				if (path == ACTIVE_PATH)
					continue;
				TerrainParameters preset = AssetDatabase.LoadAssetAtPath<TerrainParameters>(path);
				if (preset != null)
					return preset;
			}
			return null;
		}
	}
}
