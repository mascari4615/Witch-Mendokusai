using System;
using System.Collections.Generic;

namespace WitchMendokusai
{
	/// <summary>
	/// 사용자가 심은 entity 1개. EntityDataId (DataSO 영구 키) + 청크 로컬 좌표 + 심은 시각.
	/// 자연 spawn 은 결정적 RNG 로 재계산 → 저장 X. 본 struct 는 *사용자 심기* 만.
	/// </summary>
	[Serializable]
	public struct PlantedEntity
	{
		public int EntityDataId;
		public float LocalX;
		public float LocalY;
		public float LocalZ;
		public long PlantedUnixTime;
	}

	/// <summary>
	/// 단일 청크 데이터. ushort RuntimeId 1D 배열 + 좌표 + 사용자가 심은 entity 리스트.
	/// 인덱스 변환은 VoxelConstants.Index 사용.
	/// </summary>
	[Serializable]
	public class Chunk
	{
		public ChunkPosition Position;
		public ushort[] Blocks;
		public List<PlantedEntity> PlantedEntities;

		/// <summary>비동기 청크 생성/메시 굽기와 동시 SetBlock 사이의 race 방지용 lock root.</summary>
		[NonSerialized] public readonly object SyncRoot = new();

		public Chunk(ChunkPosition position)
		{
			Position = position;
			Blocks = new ushort[VoxelConstants.CHUNK_VOLUME];
			PlantedEntities = new List<PlantedEntity>();
		}

		public bool IsDirty { get; private set; }

		public void MarkClean()
		{
			IsDirty = false;
		}

		/// <summary>사용자가 심은 entity 1개 등록. 영구 보존 대상 — IsDirty 마킹.</summary>
		public void AddPlantedEntity(PlantedEntity entity)
		{
			PlantedEntities.Add(entity);
			IsDirty = true;
		}

		public ushort GetBlock(int x, int y, int z)
		{
			if (VoxelConstants.IsInBounds(x, y, z) == false)
				return VoxelConstants.AIR_RUNTIME_ID;
			return Blocks[VoxelConstants.Index(x, y, z)];
		}

		public void SetBlock(int x, int y, int z, ushort runtimeId)
		{
			if (VoxelConstants.IsInBounds(x, y, z) == false)
				return;
			Blocks[VoxelConstants.Index(x, y, z)] = runtimeId;
			IsDirty = true;
		}

		public void Fill(ushort runtimeId)
		{
			for (int i = 0; i < Blocks.Length; i++)
				Blocks[i] = runtimeId;
		}

		/// <summary>청크 로컬 좌표 → 월드 좌표</summary>
		public int LocalToWorldX(int localX) => Position.X * VoxelConstants.CHUNK_SIZE_X + localX;
		public int LocalToWorldZ(int localZ) => Position.Z * VoxelConstants.CHUNK_SIZE_Z + localZ;
	}
}
