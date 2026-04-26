using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 지형 에디터 EditorWindow. 디자이너용 thin host — 실제 View는 Runtime에 있는 TerrainEditorView.
	/// 메뉴: WitchMendokusai/Terrain Editor
	/// </summary>
	public class TerrainEditorWindow : EditorWindow
	{
		private const string TERRAIN_FOLDER = "Assets/_WitchMendokusai/Core/Scripts/Terrain";
		private const string BIOMES_FOLDER = TERRAIN_FOLDER + "/Biomes";

		[MenuItem("WitchMendokusai/Terrain Editor")]
		public static void Open()
		{
			TerrainEditorWindow window = GetWindow<TerrainEditorWindow>();
			window.titleContent = new GUIContent("Terrain Editor");
			window.minSize = new Vector2(680, 360);
			window.Show();
		}

		private TerrainParameters parameters;

		private void CreateGUI()
		{
			rootVisualElement.style.flexGrow = 1;
			parameters = ResolveParameters();
			EnsureDefaultBiomes(parameters);
			rootVisualElement.Add(new TerrainEditorView(parameters, OnParameterChanged));
		}

		private void OnParameterChanged()
		{
			if (parameters != null)
				EditorUtility.SetDirty(parameters);
		}

		private static TerrainParameters ResolveParameters()
		{
			string[] guids = AssetDatabase.FindAssets($"t:{nameof(TerrainParameters)}");
			if (guids.Length > 0)
			{
				string path = AssetDatabase.GUIDToAssetPath(guids[0]);
				return AssetDatabase.LoadAssetAtPath<TerrainParameters>(path);
			}

			TerrainParameters created = CreateInstance<TerrainParameters>();
			string assetPath = $"{TERRAIN_FOLDER}/{nameof(TerrainParameters)}_Default.asset";
			AssetDatabase.CreateAsset(created, assetPath);
			AssetDatabase.SaveAssets();
			return created;
		}

		private static void EnsureDefaultBiomes(TerrainParameters parameters)
		{
			if (parameters.Biomes != null && parameters.Biomes.Count > 0)
				return;

			if (!AssetDatabase.IsValidFolder(BIOMES_FOLDER))
				AssetDatabase.CreateFolder(TERRAIN_FOLDER, "Biomes");

			BiomeData forest = LoadOrCreateBiome("Forest", new Color(0.20f, 0.55f, 0.20f, 1f));
			BiomeData plains = LoadOrCreateBiome("Plains", new Color(0.55f, 0.78f, 0.40f, 1f));
			BiomeData mountain = LoadOrCreateBiome("Mountain", new Color(0.55f, 0.55f, 0.55f, 1f));

			List<BiomeWeight> weights = new()
			{
				new BiomeWeight(forest, 0.5f),
				new BiomeWeight(plains, 0.3f),
				new BiomeWeight(mountain, 0.2f),
			};
			parameters.SetBiomes(weights);
			EditorUtility.SetDirty(parameters);
			AssetDatabase.SaveAssets();
		}

		private static BiomeData LoadOrCreateBiome(string biomeName, Color color)
		{
			string path = $"{BIOMES_FOLDER}/{biomeName}.asset";
			BiomeData existing = AssetDatabase.LoadAssetAtPath<BiomeData>(path);
			if (existing != null)
				return existing;

			BiomeData created = CreateInstance<BiomeData>();
			created.SetBiomeName(biomeName);
			created.SetPreviewColor(color);
			AssetDatabase.CreateAsset(created, path);
			return created;
		}
	}
}
