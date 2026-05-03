using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 청크 데이터를 노이즈 + 바이옴 spawn rule로 채운다.
	/// 표면 = 바이옴 SurfaceBlock, 그 아래 N블록 = SubsurfaceBlock, 더 아래 = stone (fallback).
	/// 바이옴/블록이 비어있으면 stone fallback (FastFail 메시지 한 번만).
	/// </summary>
	public static class ChunkGenerator
	{
		public static void Generate(Chunk chunk, TerrainParameters parameters)
		{
			BlockData stone = BlockRegistry.GetByIdentifier("wm:stone");
			if (stone == null)
			{
				Debug.LogError("[ChunkGenerator] wm:stone not registered. Aborting generation.");
				return;
			}
			ushort stoneId = stone.RuntimeId;

			for (int z = 0; z < VoxelConstants.CHUNK_SIZE_Z; z++)
			{
				for (int x = 0; x < VoxelConstants.CHUNK_SIZE_X; x++)
				{
					int worldX = chunk.LocalToWorldX(x);
					int worldZ = chunk.LocalToWorldZ(z);

					// SampleHeight는 -Amplitude ~ +Amplitude. 청크 중간 높이를 베이스라인으로.
					float baseHeight = VoxelConstants.CHUNK_SIZE_Y / 2f;
					float heightF = baseHeight + TerrainGenerator.SampleHeight(parameters, worldX, worldZ);

					int surfaceY = Mathf.FloorToInt(heightF);
					surfaceY = Mathf.Clamp(surfaceY, 0, VoxelConstants.CHUNK_SIZE_Y - 1);

					BiomeData biome = TerrainGenerator.SampleBiome(parameters, worldX, worldZ);

					ushort surfaceId = stoneId;
					ushort subsurfaceId = stoneId;
					int subsurfaceDepth = 3;

					if (biome != null)
					{
						if (biome.SurfaceBlock != null)
							surfaceId = biome.SurfaceBlock.RuntimeId;
						if (biome.SubsurfaceBlock != null)
							subsurfaceId = biome.SubsurfaceBlock.RuntimeId;
						subsurfaceDepth = biome.SubsurfaceDepth;
					}

					for (int y = 0; y <= surfaceY; y++)
					{
						if (y == surfaceY)
							chunk.SetBlock(x, y, z, surfaceId);
						else if (y >= surfaceY - subsurfaceDepth)
							chunk.SetBlock(x, y, z, subsurfaceId);
						else
							chunk.SetBlock(x, y, z, stoneId);
					}
				}
			}
		}
	}
}
