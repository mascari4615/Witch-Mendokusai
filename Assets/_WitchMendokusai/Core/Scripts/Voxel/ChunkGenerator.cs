using UnityEngine;

namespace WitchMendokusai
{
	public static class ChunkGenerator
	{
		public static void Generate(Chunk chunk, TerrainParameters parameters)
		{
			ushort grassId = BlockRegistry.GetRuntimeIdOrAir("wm:grass");
			ushort dirtId = BlockRegistry.GetRuntimeIdOrAir("wm:dirt");
			ushort stoneId = BlockRegistry.GetRuntimeIdOrAir("wm:stone");

			for (int z = 0; z < VoxelConstants.CHUNK_SIZE_Z; z++)
			{
				for (int x = 0; x < VoxelConstants.CHUNK_SIZE_X; x++)
				{
					int worldX = chunk.LocalToWorldX(x);
					int worldZ = chunk.LocalToWorldZ(z);

					// TerrainGenerator.SampleHeight는 -Amplitude ~ +Amplitude 값을 반환하므로,
					// 그대로 쓰면 음수일 때 0으로 Clamp되어 광활한 평지(바닥)가 생겨버립니다.
					// 이를 방지하기 위해 청크의 중간 높이를 기본 베이스라인으로 더해줍니다.
					float baseHeight = VoxelConstants.CHUNK_SIZE_Y / 2f;
					float heightF = baseHeight + TerrainGenerator.SampleHeight(parameters, worldX, worldZ);
					
					int surfaceY = Mathf.FloorToInt(heightF);
					surfaceY = Mathf.Clamp(surfaceY, 0, VoxelConstants.CHUNK_SIZE_Y - 1);

					for (int y = 0; y <= surfaceY; y++)
					{
						if (y == surfaceY)
							chunk.SetBlock(x, y, z, grassId);
						else if (y >= surfaceY - 3)
							chunk.SetBlock(x, y, z, dirtId);
						else
							chunk.SetBlock(x, y, z, stoneId);
					}
				}
			}
		}
	}
}
