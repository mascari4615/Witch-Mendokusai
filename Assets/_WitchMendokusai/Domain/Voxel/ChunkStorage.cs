using System.IO;
using System.IO.Compression;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 청크의 디스크 I/O를 담당합니다.
	/// GZip으로 압축하여 저장 용량을 대폭 절약합니다.
	/// 포맷 v2: [version: ushort][blocks: CHUNK_VOLUME × ushort][plantedCount: int][PlantedEntity × N].
	/// v1 (legacy: 헤더 없는 raw blocks) 은 deprecated — dev only 환경이라 호환 X.
	/// </summary>
	public static class ChunkStorage
	{
		/// <summary>현재 포맷 버전. 변경 시 LoadChunk 분기 추가.</summary>
		private const ushort FORMAT_VERSION = 2;

		private static string saveDirectory;

		public static void Initialize(string basePath)
		{
			saveDirectory = Path.Combine(basePath, "Saves", "DefaultWorld", "Chunks");
			if (Directory.Exists(saveDirectory) == false)
			{
				Directory.CreateDirectory(saveDirectory);
			}
		}

		private static string GetSaveDirectory()
		{
			return saveDirectory;
		}

		private static string GetFilePath(ChunkPosition pos)
		{
			return Path.Combine(GetSaveDirectory(), $"chunk_{pos.X}_{pos.Z}.dat");
		}

		public static void SaveChunk(Chunk chunk)
		{
			if (chunk == null || chunk.IsDirty == false)
				return;

			string path = GetFilePath(chunk.Position);

			using (FileStream fs = new FileStream(path, FileMode.Create))
			using (GZipStream gz = new GZipStream(fs, CompressionMode.Compress))
			using (BinaryWriter writer = new BinaryWriter(gz))
			{
				writer.Write(FORMAT_VERSION);

				byte[] blockBytes = new byte[chunk.Blocks.Length * sizeof(ushort)];
				System.Buffer.BlockCopy(chunk.Blocks, 0, blockBytes, 0, blockBytes.Length);
				writer.Write(blockBytes);

				int plantedCount = chunk.PlantedEntities == null ? 0 : chunk.PlantedEntities.Count;
				writer.Write(plantedCount);
				for (int i = 0; i < plantedCount; i++)
				{
					PlantedEntity entity = chunk.PlantedEntities[i];
					writer.Write(entity.EntityDataId);
					writer.Write(entity.LocalX);
					writer.Write(entity.LocalY);
					writer.Write(entity.LocalZ);
					writer.Write(entity.PlantedUnixTime);
				}
			}

			chunk.MarkClean();
		}

		public static bool LoadChunk(Chunk chunk)
		{
			string path = GetFilePath(chunk.Position);
			if (File.Exists(path) == false)
				return false;

			try
			{
				using (FileStream fs = new FileStream(path, FileMode.Open))
				using (GZipStream gz = new GZipStream(fs, CompressionMode.Decompress))
				using (BinaryReader reader = new BinaryReader(gz))
				{
					ushort version = reader.ReadUInt16();
					if (version != FORMAT_VERSION)
					{
						Debug.LogError($"[ChunkStorage] {chunk.Position}: 미지원 포맷 v{version} (현재 v{FORMAT_VERSION}). 신규 생성.");
						return false;
					}

					byte[] blockBytes = reader.ReadBytes(VoxelConstants.CHUNK_VOLUME * sizeof(ushort));
					if (blockBytes.Length != VoxelConstants.CHUNK_VOLUME * sizeof(ushort))
					{
						Debug.LogError($"[ChunkStorage] {chunk.Position}: block byte 길이 불일치.");
						return false;
					}
					System.Buffer.BlockCopy(blockBytes, 0, chunk.Blocks, 0, blockBytes.Length);

					int plantedCount = reader.ReadInt32();
					if (chunk.PlantedEntities == null)
						chunk.PlantedEntities = new System.Collections.Generic.List<PlantedEntity>(plantedCount);
					else
						chunk.PlantedEntities.Clear();

					for (int i = 0; i < plantedCount; i++)
					{
						PlantedEntity entity = new()
						{
							EntityDataId = reader.ReadInt32(),
							LocalX = reader.ReadSingle(),
							LocalY = reader.ReadSingle(),
							LocalZ = reader.ReadSingle(),
							PlantedUnixTime = reader.ReadInt64(),
						};
						chunk.PlantedEntities.Add(entity);
					}
				}

				chunk.MarkClean();
				return true;
			}
			catch (System.Exception e)
			{
				Debug.LogError($"[ChunkStorage] Failed to load chunk at {chunk.Position}: {e}");
				return false;
			}
		}
	}
}
