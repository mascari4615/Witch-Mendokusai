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
		/// chunk → mesh data. atlas 가 null 이거나 BlockData 의 tile name 이 atlas 에 없으면
		/// 해당 face UV 는 (-1,-1) 센티널 → I4 셰이더가 vertex color fallback 분기.
		/// 이 메서드는 background thread 에서 호출됨 — atlas 는 read-only 데이터 접근만 (Texture 객체는 만지지 않음).
		/// </summary>
		public static ChunkMeshData GenerateMeshData(Chunk chunk, BlockTextureAtlas atlas)
		{
			List<Vector3> vertices = new();
			List<int> triangles = new();
			List<Color> colors = new();
			List<Vector2> uvs = new();

			int vertexOffset = 0;

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
						Color color = blockData.Color;

						// 블록 간 경계를 쉽게 알아볼 수 있도록 로컬 좌표 기반 체커보드 패턴(밝기 차이) 적용
						if ((x + y + z) % 2 != 0)
						{
							color = new Color(color.r * 0.85f, color.g * 0.85f, color.b * 0.85f, color.a);
						}

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
								Vector3[] faceVerts = FaceVertices[d];
								vertices.Add(pos + faceVerts[0]);
								vertices.Add(pos + faceVerts[1]);
								vertices.Add(pos + faceVerts[2]);
								vertices.Add(pos + faceVerts[3]);

								colors.Add(color);
								colors.Add(color);
								colors.Add(color);
								colors.Add(color);

								// UV: 면 방향에 맞는 tile name → atlas rect. 못 찾으면 (-1,-1) 센티널 (I4 shader fallback).
								string tileName = GetTileNameForFace(blockData, d);
								int tileIndex = atlas != null ? atlas.FindIndexByName(tileName) : -1;
								if (tileIndex >= 0)
								{
									Rect rect = atlas.GetTileUVRect(tileIndex);
									uvs.Add(new Vector2(rect.xMin, rect.yMin));
									uvs.Add(new Vector2(rect.xMax, rect.yMin));
									uvs.Add(new Vector2(rect.xMax, rect.yMax));
									uvs.Add(new Vector2(rect.xMin, rect.yMax));
								}
								else
								{
									Vector2 sentinel = new(-1f, -1f);
									uvs.Add(sentinel);
									uvs.Add(sentinel);
									uvs.Add(sentinel);
									uvs.Add(sentinel);
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

			return new ChunkMeshData
			{
				Vertices = vertices.ToArray(),
				Triangles = triangles.ToArray(),
				Colors = colors.ToArray(),
				Uvs = uvs.ToArray()
			};
		}

		/// <summary>Dirs 인덱스 0=Up / 1=Down / 2~5=Side. BlockData 의 fallback getter 가 빈 문자열 처리.</summary>
		private static string GetTileNameForFace(BlockData block, int dirIndex)
		{
			if (dirIndex == 0)
				return block.TopTileName;
			if (dirIndex == 1)
				return block.BottomTileName;
			return block.SideTileName;
		}
	}
}
