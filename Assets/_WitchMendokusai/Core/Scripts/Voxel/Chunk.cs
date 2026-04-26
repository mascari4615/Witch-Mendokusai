using System;

namespace WitchMendokusai
{
	/// <summary>
	/// 단일 청크 데이터. ushort RuntimeId 1D 배열 + 좌표.
	/// 인덱스 변환은 VoxelConstants.Index 사용.
	/// </summary>
	[Serializable]
	public class Chunk
	{
		public ChunkPosition Position;
		public ushort[] Blocks;

		/// <summary>비동기 청크 생성/메시 굽기와 동시 SetBlock 사이의 race 방지용 lock root.</summary>
		[NonSerialized] public readonly object SyncRoot = new();

		public Chunk(ChunkPosition position)
		{
			Position = position;
			Blocks = new ushort[VoxelConstants.CHUNK_VOLUME];
		}

		public bool IsDirty { get; private set; }

		public void MarkClean()
		{
			IsDirty = false;
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
