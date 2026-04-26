using UnityEngine;

namespace WitchMendokusai
{
	public static class ChunkGenerator
	{
		public static void Generate(Chunk chunk, TerrainParameters parameters)
		{
			BlockData grass = BlockRegistry.GetByIdentifier("wm:grass");
			BlockData dirt = BlockRegistry.GetByIdentifier("wm:dirt");
			BlockData stone = BlockRegistry.GetByIdentifier("wm:stone");

			if (grass == null || dirt == null || stone == null)
			{
				Debug.LogError("[ChunkGenerator] Required blocks not registered (wm:grass / wm:dirt / wm:stone). Aborting generation.");
				return;
			}

			ushort grassId = grass.RuntimeId;
			ushort dirtId = dirt.RuntimeId;
			ushort stoneId = stone.RuntimeId;

			for (int z = 0; z < VoxelConstants.CHUNK_SIZE_Z; z++)
			{
				for (int x = 0; x < VoxelConstants.CHUNK_SIZE_X; x++)
				{
					int worldX = chunk.LocalToWorldX(x);
					int worldZ = chunk.LocalToWorldZ(z);

					// SampleHeight는 -Amplitude ~ +Amplitude. 음수일 때 평지가 깔리는 것을 막기 위해 청크 중간 높이를 베이스라인으로.
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
