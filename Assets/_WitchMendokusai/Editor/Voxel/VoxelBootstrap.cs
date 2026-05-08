using System.IO;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// Voxel 부트스트랩 메뉴.
	/// "Generate Default Blocks" — 첫 7개 BlockData .asset을 Resources/Blocks/ 아래 생성 (이미 있으면 skip).
	/// "Reload Block Registry" — 모든 BlockData 자산을 다시 모아 BlockRegistry 재초기화.
	/// </summary>
	public static class VoxelBootstrap
	{
		private const string RESOURCES_FOLDER = "Assets/_WitchMendokusai/Domain/Voxel/Scripts/Resources";
		private const string BLOCKS_FOLDER = RESOURCES_FOLDER + "/Blocks";
		private const string MATERIAL_PATH = RESOURCES_FOLDER + "/VoxelMaterial.mat";
		private const string SHADER_NAME = "WM/VoxelVertexColor";

		[MenuItem("WM/Voxel/Generate Default Blocks")]
		public static void GenerateDefaultBlocks()
		{
			EnsureFolder(BLOCKS_FOLDER);

			EnsureBlock("wm:air", "Air", new Color(0f, 0f, 0f, 0f), false, false);
			EnsureBlock("wm:stone", "Stone", new Color(0.50f, 0.50f, 0.50f, 1f), true, true);
			EnsureBlock("wm:dirt", "Dirt", new Color(0.45f, 0.32f, 0.20f, 1f), true, true);
			EnsureBlock("wm:grass", "Grass", new Color(0.30f, 0.65f, 0.25f, 1f), true, true);
			EnsureBlock("wm:sand", "Sand", new Color(0.92f, 0.85f, 0.60f, 1f), true, true);
			EnsureBlock("wm:wood", "Wood", new Color(0.40f, 0.25f, 0.13f, 1f), true, true);
			EnsureBlock("wm:leaves", "Leaves", new Color(0.20f, 0.50f, 0.15f, 1f), true, false);

			EnsureDefaultMaterial();

			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();
			BlockBootstrap.Reload();
			Debug.Log($"[VoxelBootstrap] Default blocks ready. Registry count: {BlockRegistry.Count}");
		}

		[MenuItem("WM/Voxel/Generate Default Material")]
		public static void GenerateDefaultMaterialMenu()
		{
			EnsureFolder(RESOURCES_FOLDER);
			EnsureDefaultMaterial();
			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();
		}

		private static Material EnsureDefaultMaterial()
		{
			Material existing = AssetDatabase.LoadAssetAtPath<Material>(MATERIAL_PATH);
			if (existing != null)
				return existing;

			Shader shader = Shader.Find(SHADER_NAME);
			if (shader == null)
			{
				Debug.LogError($"[VoxelBootstrap] Shader '{SHADER_NAME}' not found. VoxelVertexColor.shader가 import되었는지 확인.");
				return null;
			}

			Material material = new(shader) { name = "VoxelMaterial" };
			AssetDatabase.CreateAsset(material, MATERIAL_PATH);
			Debug.Log($"[VoxelBootstrap] Default material created at {MATERIAL_PATH}");
			return material;
		}

		[MenuItem("WM/Voxel/Reload Block Registry")]
		public static void ReloadRegistry()
		{
			BlockBootstrap.Reload();
			Debug.Log($"[BlockRegistry] {BlockRegistry.Count} blocks loaded.");
		}

		private static BlockData EnsureBlock(string identifier, string name, Color color, bool solid, bool opaque)
		{
			string fileName = identifier.Replace(":", "_");
			string path = $"{BLOCKS_FOLDER}/{fileName}.asset";
			BlockData existing = AssetDatabase.LoadAssetAtPath<BlockData>(path);
			if (existing != null)
				return existing;

			BlockData block = ScriptableObject.CreateInstance<BlockData>();
			block.SetIdentifier(identifier);
			block.SetBlockName(name);
			block.SetColor(color);
			block.SetIsSolid(solid);
			block.SetIsOpaque(opaque);
			AssetDatabase.CreateAsset(block, path);
			return block;
		}

		private static void EnsureFolder(string path)
		{
			if (AssetDatabase.IsValidFolder(path))
				return;
			string parent = Path.GetDirectoryName(path).Replace("\\", "/");
			string folderName = Path.GetFileName(path);
			if (AssetDatabase.IsValidFolder(parent) == false)
				EnsureFolder(parent);
			AssetDatabase.CreateFolder(parent, folderName);
		}
	}
}
