using System.IO;
using System.IO.Compression;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 청크의 디스크 I/O를 담당합니다.
	/// GZip으로 압축하여 저장 용량을 대폭 절약합니다.
	/// </summary>
	public static class ChunkStorage
	{
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
			
			byte[] bytes = new byte[chunk.Blocks.Length * sizeof(ushort)];
			System.Buffer.BlockCopy(chunk.Blocks, 0, bytes, 0, bytes.Length);

			using (FileStream fs = new FileStream(path, FileMode.Create))
			using (GZipStream gz = new GZipStream(fs, CompressionMode.Compress))
			{
				gz.Write(bytes, 0, bytes.Length);
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
				byte[] bytes = new byte[VoxelConstants.CHUNK_VOLUME * sizeof(ushort)];
				using (FileStream fs = new FileStream(path, FileMode.Open))
				using (GZipStream gz = new GZipStream(fs, CompressionMode.Decompress))
				{
					int bytesRead = 0;
					while (bytesRead < bytes.Length)
					{
						int read = gz.Read(bytes, bytesRead, bytes.Length - bytesRead);
						if (read == 0) break;
						bytesRead += read;
					}
				}

				System.Buffer.BlockCopy(bytes, 0, chunk.Blocks, 0, bytes.Length);
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
