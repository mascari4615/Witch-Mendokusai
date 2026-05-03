using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 블록 atlas 빌더. **모든 BlockData asset 을 스캔**해 그들의 Side/Top/Bottom Texture2D 를
	/// 단일 atlas Texture2D 로 패킹 + 각 BlockData 에 결과 UV rect 를 직접 박는다 (직렬화).
	/// 같은 Texture2D 는 dedup — 1 슬롯 공유 (예: dirt 가 wm:dirt side + wm:grass bottom).
	/// 결정성: BlockData identifier 알파벳 순 + face 순 (side, top, bottom) 으로 슬롯 할당.
	/// </summary>
	public static class BlockAtlasBuilder
	{
		public const int TILE_SIZE = 16;
		public const int TILES_PER_ROW = 16;
		public const string ATLAS_PNG_PATH = "Assets/_WitchMendokusai/Core/Scripts/Voxel/Resources/BlockAtlas.png";
		public const string VOXEL_MATERIAL_PATH = "Assets/_WitchMendokusai/Core/Scripts/Voxel/Resources/VoxelMaterial.mat";
		public const string MATERIAL_TEXTURE_PROPERTY = "_MainTex";

		[MenuItem("WitchMendokusai/Voxel/Build Block Atlas")]
		public static void BuildBlockAtlas()
		{
			BlockData[] blocks = LoadAllBlockDataSorted();
			if (blocks.Length == 0)
			{
				Debug.LogWarning("[BlockAtlasBuilder] BlockData asset 없음. VoxelBootstrap.GenerateDefaultBlocks 먼저 실행.");
				return;
			}

			// 각 BlockData 의 텍스쳐 face 들을 순회하며 dedup 슬롯 할당 + 픽셀 카피.
			Dictionary<Texture2D, int> textureToSlot = new();
			int atlasPixelSize = TILE_SIZE * TILES_PER_ROW;

			Texture2D atlasTexture = new(atlasPixelSize, atlasPixelSize, TextureFormat.RGBA32, false)
			{
				filterMode = FilterMode.Point,
				wrapMode = TextureWrapMode.Clamp,
				name = "BlockAtlas"
			};

			try
			{
				Color32[] clearPixels = new Color32[atlasPixelSize * atlasPixelSize];
				atlasTexture.SetPixels32(clearPixels);

				int totalSlots = TILES_PER_ROW * TILES_PER_ROW;

				foreach (BlockData block in blocks)
				{
					AssignFaceSlot(block, BlockFace.Side, textureToSlot, atlasTexture, totalSlots);
					AssignFaceSlot(block, BlockFace.Top, textureToSlot, atlasTexture, totalSlots);
					AssignFaceSlot(block, BlockFace.Bottom, textureToSlot, atlasTexture, totalSlots);
				}

				atlasTexture.Apply(updateMipmaps: false);

				byte[] pngBytes = atlasTexture.EncodeToPNG();
				File.WriteAllBytes(ATLAS_PNG_PATH, pngBytes);

				AssetDatabase.ImportAsset(ATLAS_PNG_PATH, ImportAssetOptions.ForceSynchronousImport);
				ConfigurePersistedAtlasImporter(ATLAS_PNG_PATH);

				Texture2D persistedAtlas = AssetDatabase.LoadAssetAtPath<Texture2D>(ATLAS_PNG_PATH);
				WireAtlasToMaterial(persistedAtlas);

				// 변경된 BlockData 들 디스크 저장
				foreach (BlockData block in blocks)
					EditorUtility.SetDirty(block);

				AssetDatabase.SaveAssets();

				int distinctTextures = textureToSlot.Count;
				Debug.Log($"[BlockAtlasBuilder] Atlas built: {blocks.Length} blocks, {distinctTextures} 고유 텍스쳐 → {ATLAS_PNG_PATH}");
			}
			finally
			{
				Object.DestroyImmediate(atlasTexture);
			}
		}

		private enum BlockFace { Side, Top, Bottom }

		private static BlockData[] LoadAllBlockDataSorted()
		{
			string[] guids = AssetDatabase.FindAssets($"t:{nameof(BlockData)}");
			return guids
				.Select(AssetDatabase.GUIDToAssetPath)
				.Select(AssetDatabase.LoadAssetAtPath<BlockData>)
				.Where(block => block != null)
				.OrderBy(block => block.Identifier)
				.ToArray();
		}

		private static void AssignFaceSlot(BlockData block, BlockFace face, Dictionary<Texture2D, int> textureToSlot, Texture2D atlasTexture, int totalSlots)
		{
			Texture2D faceTexture = GetFaceTextureRaw(block, face);
			if (faceTexture == null)
			{
				ClearUVRect(block, face);
				return;
			}

			if (textureToSlot.TryGetValue(faceTexture, out int existingSlot) == false)
			{
				int newSlot = textureToSlot.Count;
				if (newSlot >= totalSlots)
				{
					Debug.LogError($"[BlockAtlasBuilder] atlas {totalSlots} 슬롯 초과. TILES_PER_ROW 늘려야 함.");
					return;
				}

				EnsureReadablePixelArt(faceTexture);

				if (faceTexture.width != TILE_SIZE || faceTexture.height != TILE_SIZE)
				{
					Debug.LogError($"[BlockAtlasBuilder] {AssetDatabase.GetAssetPath(faceTexture)}: 크기 {faceTexture.width}x{faceTexture.height} ≠ TILE_SIZE {TILE_SIZE}. 슬롯 할당 안 함.");
					ClearUVRect(block, face);
					return;
				}

				int column = newSlot % TILES_PER_ROW;
				int rowIndex = newSlot / TILES_PER_ROW;
				Color[] sourcePixels = faceTexture.GetPixels();
				atlasTexture.SetPixels(column * TILE_SIZE, rowIndex * TILE_SIZE, TILE_SIZE, TILE_SIZE, sourcePixels);

				textureToSlot[faceTexture] = newSlot;
				existingSlot = newSlot;
			}

			Rect uvRect = SlotToUVRect(existingSlot);
			SetUVRect(block, face, uvRect);
		}

		/// <summary>fallback 적용 안 한 raw 필드 텍스쳐 — Builder 가 *명시 할당된* 면만 패킹하도록.</summary>
		private static Texture2D GetFaceTextureRaw(BlockData block, BlockFace face)
		{
			SerializedObject serialized = new(block);
			string fieldName = face switch
			{
				BlockFace.Side => "sideTexture",
				BlockFace.Top => "topTexture",
				BlockFace.Bottom => "bottomTexture",
				_ => null
			};
			if (fieldName == null)
				return null;
			SerializedProperty property = serialized.FindProperty(fieldName);
			return property?.objectReferenceValue as Texture2D;
		}

		private static void SetUVRect(BlockData block, BlockFace face, Rect rect)
		{
			switch (face)
			{
				case BlockFace.Side: block.SetSideUVRect(rect); break;
				case BlockFace.Top: block.SetTopUVRect(rect); break;
				case BlockFace.Bottom: block.SetBottomUVRect(rect); break;
			}
		}

		private static void ClearUVRect(BlockData block, BlockFace face)
		{
			Rect zero = new(0f, 0f, 0f, 0f);
			SetUVRect(block, face, zero);
		}

		private static Rect SlotToUVRect(int slot)
		{
			int column = slot % TILES_PER_ROW;
			int rowIndex = slot / TILES_PER_ROW;
			float size = 1f / TILES_PER_ROW;
			return new Rect(column * size, rowIndex * size, size, size);
		}

		/// <summary>VoxelMaterial 의 _MainTex 에 빌드된 atlas Texture2D 박기. 셰이더 (I4) 가 sample.</summary>
		public static void WireAtlasToMaterial(Texture2D atlasTexture)
		{
			Material material = AssetDatabase.LoadAssetAtPath<Material>(VOXEL_MATERIAL_PATH);
			if (material == null)
			{
				Debug.LogWarning($"[BlockAtlasBuilder] VoxelMaterial 못 찾음 ({VOXEL_MATERIAL_PATH}). VoxelBootstrap.GenerateDefaultMaterial 먼저 실행 필요.");
				return;
			}
			if (material.HasProperty(MATERIAL_TEXTURE_PROPERTY) == false)
			{
				Debug.LogWarning($"[BlockAtlasBuilder] VoxelMaterial 에 {MATERIAL_TEXTURE_PROPERTY} 프로퍼티 없음. 셰이더 (VoxelVertexColor) 가 atlas 안 받는 버전일 수 있음.");
				return;
			}
			material.SetTexture(MATERIAL_TEXTURE_PROPERTY, atlasTexture);
			EditorUtility.SetDirty(material);
		}

		private static void EnsureReadablePixelArt(Texture2D texture)
		{
			string path = AssetDatabase.GetAssetPath(texture);
			TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
			if (importer == null)
				return;

			List<string> changes = new();
			if (importer.isReadable == false)
			{
				importer.isReadable = true;
				changes.Add("isReadable=true");
			}
			if (importer.textureCompression != TextureImporterCompression.Uncompressed)
			{
				importer.textureCompression = TextureImporterCompression.Uncompressed;
				changes.Add("compression=Uncompressed");
			}
			if (importer.filterMode != FilterMode.Point)
			{
				importer.filterMode = FilterMode.Point;
				changes.Add("filter=Point");
			}
			if (importer.mipmapEnabled)
			{
				importer.mipmapEnabled = false;
				changes.Add("mipmaps=off");
			}

			if (changes.Count > 0)
			{
				Debug.Log($"[BlockAtlasBuilder] {path}: import 설정 변경 [{string.Join(", ", changes)}] (pixel art atlas 요건).");
				importer.SaveAndReimport();
			}
		}

		private static void ConfigurePersistedAtlasImporter(string path)
		{
			TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
			if (importer == null)
				return;
			importer.textureType = TextureImporterType.Default;
			importer.filterMode = FilterMode.Point;
			importer.wrapMode = TextureWrapMode.Clamp;
			importer.mipmapEnabled = false;
			importer.textureCompression = TextureImporterCompression.Uncompressed;
			importer.SaveAndReimport();
		}
	}
}
