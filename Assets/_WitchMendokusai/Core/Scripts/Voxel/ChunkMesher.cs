using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	public static class ChunkMesher
	{
		private static readonly Vector3Int[] Dirs = new Vector3Int[]
		{
			new(0, 1, 0),   // Up
			new(0, -1, 0),  // Down
			new(-1, 0, 0),  // Left
			new(1, 0, 0),   // Right
			new(0, 0, 1),   // Forward
			new(0, 0, -1)   // Back
		};

		// Vertices for each face, counter-clockwise ordering when looking at the face from outside
		private static readonly Vector3[][] FaceVertices = new Vector3[][]
		{
			new Vector3[] { new(0,1,1), new(1,1,1), new(1,1,0), new(0,1,0) }, // Up
			new Vector3[] { new(0,0,0), new(1,0,0), new(1,0,1), new(0,0,1) }, // Down
			new Vector3[] { new(0,0,1), new(0,1,1), new(0,1,0), new(0,0,0) }, // Left
			new Vector3[] { new(1,0,0), new(1,1,0), new(1,1,1), new(1,0,1) }, // Right
			new Vector3[] { new(1,0,1), new(1,1,1), new(0,1,1), new(0,0,1) }, // Forward
			new Vector3[] { new(0,0,0), new(0,1,0), new(1,1,0), new(1,0,0) }  // Back
		};

		/// <summary>
		/// chunk → mesh data. **Worldspace UV**: TEXCOORD0 = face 방향 worldUV (frac 으로 wrap), TEXCOORD1 = atlas tile rect + worldScale.
		/// 셰이더가 `frac(uv / worldScale) * atlasSize + atlasOrigin` 으로 atlas sample.
		/// 텍스쳐 미할당 (rect.width == 0) 이면 tileRect.z = 0 → 셰이더 vertex color path.
		/// `parameters` 가 있으면 column 별 biome 을 prefetch 해 `BlockData.AcceptsBiomeTint` 블록 면의
		/// vertex color 에 `biome.PreviewColor` 곱 (식물성 블록만 — atlas/sentinel 양쪽 적용).
		/// background thread 에서 호출됨 — BlockData/BiomeData read-only 데이터 접근만.
		/// </summary>
		public static ChunkMeshData GenerateMeshData(Chunk chunk, TerrainParameters parameters)
		{
			List<Vector3> vertices = new();
			List<int> triangles = new();
			List<Color> colors = new();
			List<Vector2> uvs = new();
			List<Vector4> tileRects = new();

			int vertexOffset = 0;
			int atlasFaceCount = 0;
			int sentinelFaceCount = 0;
			int biomeTintFaceCount = 0;

			// column 단위 biome prefetch — face emit 마다 sample 하지 않게.
			// parameters 가 null (test runner 등) 이면 모두 null → tint X.
			BiomeData[] biomeColumns = new BiomeData[VoxelConstants.CHUNK_SIZE_X * VoxelConstants.CHUNK_SIZE_Z];
			if (parameters != null)
			{
				for (int cz = 0; cz < VoxelConstants.CHUNK_SIZE_Z; cz++)
				{
					for (int cx = 0; cx < VoxelConstants.CHUNK_SIZE_X; cx++)
					{
						int worldX = chunk.LocalToWorldX(cx);
						int worldZ = chunk.LocalToWorldZ(cz);
						biomeColumns[cz * VoxelConstants.CHUNK_SIZE_X + cx] = TerrainGenerator.SampleBiome(parameters, worldX, worldZ);
					}
				}
			}

			for (int y = 0; y < VoxelConstants.CHUNK_SIZE_Y; y++)
			{
				for (int z = 0; z < VoxelConstants.CHUNK_SIZE_Z; z++)
				{
					for (int x = 0; x < VoxelConstants.CHUNK_SIZE_X; x++)
					{
						ushort blockId = chunk.GetBlock(x, y, z);
						if (blockId == VoxelConstants.AIR_RUNTIME_ID)
							continue;

						BlockData blockData = BlockRegistry.GetByRuntimeId(blockId);
						if (blockData == null)
							continue;

						Vector3 pos = new(x, y, z);

						for (int d = 0; d < 6; d++)
						{
							Vector3Int dir = Dirs[d];
							int nx = x + dir.x;
							int ny = y + dir.y;
							int nz = z + dir.z;

							bool generateFace = false;

							if (VoxelConstants.IsInBounds(nx, ny, nz) == false)
							{
								generateFace = true; // Chunk boundary
							}
							else
							{
								ushort neighborId = chunk.GetBlock(nx, ny, nz);
								if (neighborId == VoxelConstants.AIR_RUNTIME_ID)
								{
									generateFace = true;
								}
								else
								{
									BlockData neighborData = BlockRegistry.GetByRuntimeId(neighborId);
									if (neighborData == null || neighborData.IsOpaque == false)
										generateFace = true;
								}
							}

							if (generateFace)
							{
								Rect rect = GetUVRectForFace(blockData, d);
								bool hasAtlas = rect.width > 0f;

								// vertex color base: atlas 면 = white (atlas 색만 보임), sentinel 면 = block.Color × 체커보드.
								Color faceColor;
								if (hasAtlas)
								{
									faceColor = Color.white;
									atlasFaceCount++;
								}
								else
								{
									faceColor = blockData.Color;
									if ((x + y + z) % 2 != 0)
										faceColor = new Color(faceColor.r * 0.85f, faceColor.g * 0.85f, faceColor.b * 0.85f, faceColor.a);
									sentinelFaceCount++;
								}

								// Biome tint: 식물성 블록만. column biome.PreviewColor 곱 → atlas / sentinel 양쪽 적용.
								if (blockData.AcceptsBiomeTint)
								{
									BiomeData biome = biomeColumns[z * VoxelConstants.CHUNK_SIZE_X + x];
									if (biome != null)
									{
										Color tint = biome.PreviewColor;
										faceColor = new Color(faceColor.r * tint.r, faceColor.g * tint.g, faceColor.b * tint.b, faceColor.a);
										biomeTintFaceCount++;
									}
								}

								// TEXCOORD1 = (atlas xMin, yMin, atlasSize, worldScale). atlasSize=0 = sentinel (셰이더 vertex color path).
								// worldScale 은 sentinel 도 1f 안전값 (셰이더 frac(uv / 1) 안전).
								float worldScaleSafe = blockData.TextureWorldScale > 0f ? blockData.TextureWorldScale : 1f;
								Vector4 tileRect = new(rect.xMin, rect.yMin, rect.width, worldScaleSafe);

								Vector3[] faceVerts = FaceVertices[d];
								for (int v = 0; v < 4; v++)
								{
									Vector3 localOffset = faceVerts[v];
									vertices.Add(pos + localOffset);
									colors.Add(faceColor);
									Vector3 worldVertex = new(
										chunk.LocalToWorldX(x) + localOffset.x,
										y + localOffset.y,
										chunk.LocalToWorldZ(z) + localOffset.z
									);
									uvs.Add(GetWorldUV(d, worldVertex));
									tileRects.Add(tileRect);
								}

								triangles.Add(vertexOffset + 0);
								triangles.Add(vertexOffset + 1);
								triangles.Add(vertexOffset + 2);
								triangles.Add(vertexOffset + 0);
								triangles.Add(vertexOffset + 2);
								triangles.Add(vertexOffset + 3);

								vertexOffset += 4;
							}
						}
					}
				}
			}

			Debug.Log($"[ChunkMesher] chunk({chunk.Position.X},{chunk.Position.Z}): {vertexOffset / 4} faces, atlas={atlasFaceCount}, sentinel={sentinelFaceCount}, biomeTint={biomeTintFaceCount}");

			return new ChunkMeshData
			{
				Vertices = vertices.ToArray(),
				Triangles = triangles.ToArray(),
				Colors = colors.ToArray(),
				Uvs = uvs.ToArray(),
				TileRects = tileRects.ToArray()
			};
		}

		/// <summary>Dirs 인덱스 0=Up / 1=Down / 2~5=Side. BlockData 의 fallback getter 가 null texture 처리.</summary>
		private static Rect GetUVRectForFace(BlockData block, int dirIndex)
		{
			if (dirIndex == 0)
				return block.TopUVRect;
			if (dirIndex == 1)
				return block.BottomUVRect;
			return block.SideUVRect;
		}

		/// <summary>face 방향에 맞는 평면 좌표 — 셰이더가 frac wrap 후 atlas tile rect 매핑.
		/// Up/Down (d 0/1): XZ 평면. Left/Right (2/3): ZY 평면. Forward/Back (4/5): XY 평면.</summary>
		private static Vector2 GetWorldUV(int dirIndex, Vector3 worldPos)
		{
			if (dirIndex == 0 || dirIndex == 1)
				return new Vector2(worldPos.x, worldPos.z);
			if (dirIndex == 2 || dirIndex == 3)
				return new Vector2(worldPos.z, worldPos.y);
			return new Vector2(worldPos.x, worldPos.y);
		}
	}
}
