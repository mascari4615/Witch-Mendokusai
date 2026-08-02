using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 청크 GameObject 활성화 시 biome spawn rule에 따라 자연 entity 인스턴스화 + 사용자 심기 복원.
	/// 자연 spawn = 결정적 RNG (parameters.Seed XOR chunk 좌표) — 같은 청크는 항상 같은 spawn 결과. 직렬화 X.
	/// 사용자 심기 = chunk.PlantedEntities 영구 보존 → 복원 시 인스턴스화.
	/// 인스턴스는 chunkGo의 자식. ChunkPool.Release 가 자식을 정리하므로 별도 추적 불필요.
	/// </summary>
	public static class ChunkEntitySpawner
	{
		// XOR 스크램블에 쓰는 큰 prime — chunk 좌표별 hash 분산용
		private const int PRIME_X = 73856093;
		private const int PRIME_Z = 19349663;

		public static void SpawnEntitiesForChunk(Chunk chunk, GameObject chunkGo, TerrainParameters parameters)
		{
			if (chunk == null || chunkGo == null || parameters == null)
				return;

			int seed = parameters.Seed ^ (chunk.Position.X * PRIME_X) ^ (chunk.Position.Z * PRIME_Z);
			System.Random rng = new(seed);

			for (int lz = 0; lz < VoxelConstants.CHUNK_SIZE_Z; lz++)
			{
				for (int lx = 0; lx < VoxelConstants.CHUNK_SIZE_X; lx++)
				{
					int surfaceY = FindSurfaceY(chunk, lx, lz);
					if (surfaceY < 0)
						continue;

					int worldX = chunk.LocalToWorldX(lx);
					int worldZ = chunk.LocalToWorldZ(lz);
					BiomeData biome = TerrainGenerator.SampleBiome(parameters, worldX, worldZ);
					if (biome == null)
						continue;

					EvaluateRulesForCell(biome, rng, chunkGo, lx, surfaceY, lz);
				}
			}
		}

		/// <summary>청크 활성 시 사용자가 심어둔 entity 복원. SpawnEntitiesForChunk 직후 호출.</summary>
		public static void RestorePlantedEntities(Chunk chunk, GameObject chunkGo)
		{
			if (chunk == null || chunkGo == null || chunk.PlantedEntities == null)
				return;

			for (int i = 0; i < chunk.PlantedEntities.Count; i++)
			{
				PlantedEntity planted = chunk.PlantedEntities[i];
				EntityData entity = SOHelper.Get<EntityData>(planted.EntityDataId);
				if (entity == null || entity.Prefab == null)
				{
					Debug.LogError($"[ChunkEntitySpawner] 사용자 심기 복원 실패: EntityDataId={planted.EntityDataId} (chunk {chunk.Position}).");
					continue;
				}

				GameObject instance = Object.Instantiate(entity.Prefab, chunkGo.transform);
				instance.transform.localPosition = new Vector3(planted.LocalX, planted.LocalY, planted.LocalZ);
			}
		}

		/// <summary>사용자가 청크 로컬 (lx,ly,lz) 위치에 entity 한 그루 심음. PlantedEntities 등록 + 인스턴스화.</summary>
		public static void PlantUserEntity(Chunk chunk, GameObject chunkGo, EntityData entity, float localX, float localY, float localZ)
		{
			if (chunk == null || chunkGo == null || entity == null || entity.Prefab == null)
			{
				Debug.LogError($"[ChunkEntitySpawner] PlantUserEntity: 인자 null (entity={entity}).");
				return;
			}

			PlantedEntity record = new()
			{
				EntityDataId = entity.ID,
				LocalX = localX,
				LocalY = localY,
				LocalZ = localZ,
				PlantedUnixTime = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
			};
			chunk.AddPlantedEntity(record);

			GameObject instance = Object.Instantiate(entity.Prefab, chunkGo.transform);
			instance.transform.localPosition = new Vector3(localX, localY, localZ);
		}

		private static int FindSurfaceY(Chunk chunk, int lx, int lz)
		{
			for (int y = VoxelConstants.CHUNK_SIZE_Y - 1; y >= 0; y--)
			{
				if (chunk.GetBlock(lx, y, lz) != VoxelConstants.AIR_RUNTIME_ID)
					return y;
			}
			return -1;
		}

		private static void EvaluateRulesForCell(BiomeData biome, System.Random rng, GameObject chunkGo, int lx, int surfaceY, int lz)
		{
			if (biome.EntitySpawns == null)
				return;

			for (int i = 0; i < biome.EntitySpawns.Count; i++)
			{
				BiomeEntitySpawnRule rule = biome.EntitySpawns[i];
				if (rule == null || rule.Entity == null || rule.Entity.Prefab == null)
					continue;
				if (rng.NextDouble() >= rule.Density)
					continue;

				GameObject instance = Object.Instantiate(rule.Entity.Prefab, chunkGo.transform);
				instance.transform.localPosition = new Vector3(lx + 0.5f, surfaceY + 1f, lz + 0.5f);
			}
		}
	}
}
