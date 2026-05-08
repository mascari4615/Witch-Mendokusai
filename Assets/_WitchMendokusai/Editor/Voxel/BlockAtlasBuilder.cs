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
		// 32 px/tile = 마크 HD / 인디 표준. 픽셀 미감 + 적당한 디테일.
		// 작은 입력 (16) 은 Point upscale, 큰 입력 (64+) 은 nearest-neighbor down — pixel art 미감 보존.
		public const int TILE_SIZE = 32;
		public const int TILES_PER_ROW = 16;
		public const string ATLAS_PNG_PATH = "Assets/_WitchMendokusai/Domain/Voxel/Scripts/Resources/BlockAtlas.png";
		public const string VOXEL_SHADER_NAME = "WM/VoxelVertexColor";
		public const string MATERIAL_TEXTURE_PROPERTY = "_MainTex";

		[MenuItem("WM/Voxel/Build Block Atlas")]
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
				WireAtlasToAllVoxelMaterials(persistedAtlas);

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

				int column = newSlot % TILES_PER_ROW;
				int rowIndex = newSlot / TILES_PER_ROW;
				Color[] tilePixels = ResampleToTileSize(faceTexture);
				atlasTexture.SetPixels(column * TILE_SIZE, rowIndex * TILE_SIZE, TILE_SIZE, TILE_SIZE, tilePixels);

				textureToSlot[faceTexture] = newSlot;
				existingSlot = newSlot;

				Debug.Log($"[BlockAtlasBuilder] slot {newSlot} (col={column}, row={rowIndex}) ← {AssetDatabase.GetAssetPath(faceTexture)} ({faceTexture.width}×{faceTexture.height} → 16×16)");
			}

			Rect uvRect = SlotToUVRect(existingSlot);
			SetUVRect(block, face, uvRect);
			Debug.Log($"[BlockAtlasBuilder] {block.Identifier}.{face} → slot {existingSlot}, UV rect {uvRect}");
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

		/// <summary>입력 텍스쳐를 TILE_SIZE×TILE_SIZE 로 변환. 같은 크기면 그대로, 아니면 nearest-neighbor 리샘플 (pixel art 스타일 보존, 결정적).</summary>
		private static Color[] ResampleToTileSize(Texture2D source)
		{
			Color[] sourcePixels = source.GetPixels();
			if (source.width == TILE_SIZE && source.height == TILE_SIZE)
				return sourcePixels;

			Color[] tilePixels = new Color[TILE_SIZE * TILE_SIZE];
			for (int dy = 0; dy < TILE_SIZE; dy++)
			{
				int sy = dy * source.height / TILE_SIZE;
				for (int dx = 0; dx < TILE_SIZE; dx++)
				{
					int sx = dx * source.width / TILE_SIZE;
					tilePixels[dy * TILE_SIZE + dx] = sourcePixels[sy * source.width + sx];
				}
			}
			return tilePixels;
		}

		private static Rect SlotToUVRect(int slot)
		{
			int column = slot % TILES_PER_ROW;
			int rowIndex = slot / TILES_PER_ROW;
			float size = 1f / TILES_PER_ROW;
			return new Rect(column * size, rowIndex * size, size, size);
		}

		/// <summary>
		/// 프로젝트 내 *모든* `WM/VoxelVertexColor` 셰이더 사용 material 의 `_MainTex` 에 빌드된 atlas 박기.
		/// 단일 정본 material 외에도 씬·프리팹에서 직접 만든 material 이 있을 수 있어 (과거 함정 사례)
		/// 셰이더 사용처를 자동 wire — atlas 빌드 1회로 모든 voxel material 동기화.
		/// </summary>
		public static void WireAtlasToAllVoxelMaterials(Texture2D atlasTexture)
		{
			string[] guids = AssetDatabase.FindAssets("t:Material");
			int wired = 0;
			int skippedNoProperty = 0;
			foreach (string guid in guids)
			{
				string path = AssetDatabase.GUIDToAssetPath(guid);
				Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
				if (material == null || material.shader == null)
					continue;
				if (material.shader.name != VOXEL_SHADER_NAME)
					continue;
				if (material.HasProperty(MATERIAL_TEXTURE_PROPERTY) == false)
				{
					skippedNoProperty++;
					continue;
				}
				material.SetTexture(MATERIAL_TEXTURE_PROPERTY, atlasTexture);
				EditorUtility.SetDirty(material);
				wired++;
				Debug.Log($"[BlockAtlasBuilder] {path} ← atlas wire");
			}

			if (wired == 0)
				Debug.LogWarning($"[BlockAtlasBuilder] '{VOXEL_SHADER_NAME}' 셰이더 사용 material 없음 — VoxelBootstrap.GenerateDefaultMaterial 먼저 실행.");
			else
				Debug.Log($"[BlockAtlasBuilder] {wired} material atlas wire 완료 (no-{MATERIAL_TEXTURE_PROPERTY}: {skippedNoProperty}).");
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
