using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// BlockTextureAtlas 빌더. <see cref="SOURCE_FOLDER"/> 의 PNG 들을 그리드로 패킹해
	/// 단일 atlas Texture2D 를 만든다. 결정성을 위해 파일명 알파벳 순으로 슬롯 배치.
	/// 입력 PNG 는 모두 atlas.TileSize × atlas.TileSize 크기여야 함 (다른 크기는 skip).
	/// 빌드 결과: <see cref="ATLAS_PNG_PATH"/> 에 PNG 저장 + Active atlas SO 갱신.
	/// </summary>
	public static class BlockAtlasBuilder
	{
		public const string SOURCE_FOLDER = "Assets/_WitchMendokusai/Content/Voxel/BlockTextures";
		public const string ATLAS_PNG_PATH = "Assets/_WitchMendokusai/Core/Scripts/Voxel/Resources/BlockAtlas.png";

		[MenuItem("WitchMendokusai/Voxel/Build Block Atlas")]
		public static void BuildBlockAtlas()
		{
			BlockTextureAtlas atlas = BlockAtlasBootstrap.EnsureActive();

			EnsureFolder(SOURCE_FOLDER);

			string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { SOURCE_FOLDER });
			if (guids.Length == 0)
			{
				Debug.LogWarning($"[BlockAtlasBuilder] No textures in {SOURCE_FOLDER}. PNG 를 거기 두고 메뉴 다시 실행.");
				return;
			}

			string[] paths = guids
				.Select(AssetDatabase.GUIDToAssetPath)
				.OrderBy(path => path)
				.ToArray();

			int tileSize = atlas.TileSize;
			int tilesPerRow = atlas.TilesPerRow;
			int totalSlots = tilesPerRow * tilesPerRow;
			if (paths.Length > totalSlots)
			{
				Debug.LogError($"[BlockAtlasBuilder] {paths.Length} 개 텍스쳐는 atlas {totalSlots} 슬롯 초과. tilesPerRow 늘려야 함.");
				return;
			}

			int atlasPixelSize = tileSize * tilesPerRow;
			Texture2D atlasTexture = new(atlasPixelSize, atlasPixelSize, TextureFormat.RGBA32, false)
			{
				filterMode = FilterMode.Point,
				wrapMode = TextureWrapMode.Clamp,
				name = "BlockAtlas"
			};

			// 투명 배경 초기화
			Color32[] clearPixels = new Color32[atlasPixelSize * atlasPixelSize];
			atlasTexture.SetPixels32(clearPixels);

			List<AtlasTileEntry> entries = new();

			for (int i = 0; i < paths.Length; i++)
			{
				string sourcePath = paths[i];
				EnsureReadablePixelArt(sourcePath);

				Texture2D source = AssetDatabase.LoadAssetAtPath<Texture2D>(sourcePath);
				if (source == null)
				{
					Debug.LogError($"[BlockAtlasBuilder] Failed to load {sourcePath}");
					continue;
				}
				if (source.width != tileSize || source.height != tileSize)
				{
					Debug.LogError($"[BlockAtlasBuilder] {sourcePath}: 크기 {source.width}x{source.height} ≠ tileSize {tileSize}. 건너뜀.");
					continue;
				}

				int column = i % tilesPerRow;
				int rowIndex = i / tilesPerRow;
				Color[] sourcePixels = source.GetPixels();
				atlasTexture.SetPixels(column * tileSize, rowIndex * tileSize, tileSize, tileSize, sourcePixels);

				string tileName = Path.GetFileNameWithoutExtension(sourcePath);
				entries.Add(new AtlasTileEntry(i, tileName));
			}

			atlasTexture.Apply(updateMipmaps: false);

			byte[] pngBytes = atlasTexture.EncodeToPNG();
			File.WriteAllBytes(ATLAS_PNG_PATH, pngBytes);
			Object.DestroyImmediate(atlasTexture);

			AssetDatabase.ImportAsset(ATLAS_PNG_PATH, ImportAssetOptions.ForceSynchronousImport);
			ConfigurePersistedAtlasImporter(ATLAS_PNG_PATH);

			Texture2D persistedAtlas = AssetDatabase.LoadAssetAtPath<Texture2D>(ATLAS_PNG_PATH);
			atlas.SetAtlas(persistedAtlas, entries);
			EditorUtility.SetDirty(atlas);
			AssetDatabase.SaveAssets();

			Debug.Log($"[BlockAtlasBuilder] Atlas built: {entries.Count} tiles → {ATLAS_PNG_PATH}");
		}

		private static void EnsureReadablePixelArt(string path)
		{
			TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
			if (importer == null)
				return;

			bool changed = false;
			if (importer.isReadable == false)
			{
				importer.isReadable = true;
				changed = true;
			}
			if (importer.textureCompression != TextureImporterCompression.Uncompressed)
			{
				importer.textureCompression = TextureImporterCompression.Uncompressed;
				changed = true;
			}
			if (importer.filterMode != FilterMode.Point)
			{
				importer.filterMode = FilterMode.Point;
				changed = true;
			}
			if (importer.mipmapEnabled)
			{
				importer.mipmapEnabled = false;
				changed = true;
			}

			if (changed)
				importer.SaveAndReimport();
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
