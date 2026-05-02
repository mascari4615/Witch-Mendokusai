using System;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 블록 6면 텍스쳐를 단일 atlas Texture2D 로 묶어 관리하는 SO.
	/// 메시 1 draw call 유지 + I3 mesher 가 tile index → UV rect 매핑에 사용.
	/// 직접 atlas 를 만들지 않고 BlockAtlasBuilder (Editor 메뉴) 가 sprite 폴더에서 빌드.
	/// </summary>
	[CreateAssetMenu(fileName = nameof(BlockTextureAtlas), menuName = "WM/Voxel/" + nameof(BlockTextureAtlas))]
	public class BlockTextureAtlas : ScriptableObject
	{
		[SerializeField] private Texture2D atlasTexture;
		[SerializeField, Min(1)] private int tileSize = 16;
		[SerializeField, Min(1)] private int tilesPerRow = 16;
		[SerializeField] private List<AtlasTileEntry> tiles = new();

		public Texture2D AtlasTexture => atlasTexture;
		public int TileSize => tileSize;
		public int TilesPerRow => tilesPerRow;
		public IReadOnlyList<AtlasTileEntry> Tiles => tiles;

		/// <summary>tile index → UV rect (0~1 정규화). 그리드 row 0 = 아래.</summary>
		public Rect GetTileUVRect(int tileIndex)
		{
			if (tileIndex < 0 || tilesPerRow <= 0)
				return new Rect(0f, 0f, 0f, 0f);
			// 슬롯 범위 (tilesPerRow²) 초과는 atlas 그리드 밖 → 빈 rect 반환 (UV (0,0) sentinel 효과).
			if (tileIndex >= tilesPerRow * tilesPerRow)
				return new Rect(0f, 0f, 0f, 0f);
			int row = tileIndex / tilesPerRow;
			int col = tileIndex % tilesPerRow;
			float size = 1f / tilesPerRow;
			return new Rect(col * size, row * size, size, size);
		}

		/// <summary>이름 → tile index. 못 찾으면 -1. BlockData 가 이름으로 참조 (안정성).</summary>
		public int FindIndexByName(string tileName)
		{
			for (int i = 0; i < tiles.Count; i++)
			{
				if (tiles[i].Name == tileName)
					return tiles[i].Index;
			}
			return -1;
		}

		/// <summary>BlockAtlasBuilder 전용. 빌드 결과 반영.</summary>
		public void SetAtlas(Texture2D texture, List<AtlasTileEntry> entries)
		{
			atlasTexture = texture;
			tiles = entries;
		}
	}

	[Serializable]
	public class AtlasTileEntry
	{
		[SerializeField] private int index;
		[SerializeField] private string tileName;

		public int Index => index;
		public string Name => tileName;

		public AtlasTileEntry() { }
		public AtlasTileEntry(int index, string tileName)
		{
			this.index = index;
			this.tileName = tileName;
		}
	}
}
