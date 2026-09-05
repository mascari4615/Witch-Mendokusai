using System;
using UnityEngine;
using WitchMendokusai.DomainSDK.Farming;

namespace WitchMendokusai
{
	// 복셀 청크를 밭의 땅으로 쓰는 어댑터 (TASK-WM-410).
	//
	// ★ 블록의 임시 번호(RuntimeId)는 이 안에서만 산다. 밖(규칙·밭)은 영구 이름만 안다 —
	//   번호는 부팅마다 달라지므로 밖으로 새면 부팅 순서가 밭을 바꾼다.
	// ★ 청크 매니저를 지연으로 받는 이유: 스테이지 스코프라 밭보다 늦게 설 수 있다(init-order 규약).
	public sealed class ChunkBlockGround : IBlockGround
	{
		private readonly Func<ChunkManager> chunkSource;

		public ChunkBlockGround(Func<ChunkManager> chunkSource)
		{
			this.chunkSource = chunkSource;
		}

		public string BlockNameAt(FarmCoord coord)
		{
			ChunkManager chunks = Chunks();
			if (chunks == null)
			{
				return null;
			}

			BlockData block = BlockRegistry.GetByRuntimeId(chunks.GetBlock(coord.X, coord.Y, coord.Z));
			return block == null ? null : block.Identifier;
		}

		public void SetBlock(FarmCoord coord, string identifier)
		{
			ChunkManager chunks = Chunks();
			BlockData block = BlockRegistry.GetByIdentifier(identifier);

			if (chunks == null || block == null)
			{
				Debug.LogError($"[ChunkBlockGround] 블록 교체 실패: {identifier} (등록 안 됨?).");
				return;
			}

			chunks.SetBlock(coord.X, coord.Y, coord.Z, block.RuntimeId);
		}

		public bool SpawnEntity(FarmCoord coord, EntityData entity)
		{
			ChunkManager chunks = Chunks();
			if (chunks == null || entity == null)
			{
				return false;
			}

			return chunks.PlantEntityAt(new Vector3(coord.X, coord.Y, coord.Z), entity);
		}

		private ChunkManager Chunks() => chunkSource == null ? null : chunkSource();
	}
}
