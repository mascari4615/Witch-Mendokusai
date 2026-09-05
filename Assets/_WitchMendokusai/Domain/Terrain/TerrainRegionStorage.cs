using System.IO;
using System.IO.Compression;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// RegionGridNode (Hydraulic/Thermal/Smooth 등) 의 *시뮬레이션 완료* 영역 배열을
	/// 디스크에 영속화 (TASK-WM-119). erosion 은 결정적 (같은 region+algoHash → 같은 결과)
	/// 이라 영역당 *평생 1회* 계산 후 이후 세션은 디스크 즉시 로드 — NonSerialized 캐시가
	/// 세션/도메인리로드마다 소실되어 매 플레이 ~3s/영역 재계산하던 것의 근본 해소.
	///
	/// ChunkStorage 와 동형 (gzip + persistentDataPath 하위). 키 = 노드타입+algoHash+
	/// regionSize+region 좌표 → 파라미터 변경 시 다른 파일 (옛 파일 orphan, 무해).
	/// background thread I/O 안전 (호출자 RegionGridNodeBase 가 per-node cacheLock 보유).
	/// </summary>
	public static class TerrainRegionStorage
	{
		private static string saveDirectory;

		/// <summary>main thread 에서 1회 호출 (Application.persistentDataPath = main-thread only).
		/// 미초기화 시 IsReady=false → 영속 skip, 메모리 캐시만 (동작 동일, 느릴 뿐).</summary>
		public static void Initialize(string basePath)
		{
			saveDirectory = Path.Combine(basePath, "Saves", "DefaultWorld", "TerrainRegions");
			if (Directory.Exists(saveDirectory) == false)
				Directory.CreateDirectory(saveDirectory);
		}

		public static bool IsReady => saveDirectory != null;

		private static string FilePath(string key) => Path.Combine(saveDirectory, key + ".dat");

		/// <summary>키 — 노드타입+algoHash+regionSize+region 좌표. 결정적·충돌 없음.</summary>
		public static string MakeKey(string nodeTypeName, int algorithmHash, int regionSize, int regionX, int regionZ)
			=> $"{nodeTypeName}_{(uint)algorithmHash:x8}_{regionSize}_{regionX}_{regionZ}";

		public static bool TryLoad(string key, int regionSize, out float[,] region)
		{
			region = null;
			if (IsReady == false)
				return false;
			string path = FilePath(key);
			if (File.Exists(path) == false)
				return false;

			try
			{
				int count = regionSize * regionSize;
				byte[] bytes = new byte[count * sizeof(float)];
				using (FileStream fs = new FileStream(path, FileMode.Open))
				using (GZipStream gz = new GZipStream(fs, CompressionMode.Decompress))
				{
					int read = 0;
					while (read < bytes.Length)
					{
						int n = gz.Read(bytes, read, bytes.Length - read);
						if (n == 0)
							break;
						read += n;
					}
					if (read != bytes.Length)
						return false; // 손상/부분 파일 → 재계산 fallback
				}
				float[,] result = new float[regionSize, regionSize];
				System.Buffer.BlockCopy(bytes, 0, result, 0, bytes.Length);
				region = result;
				return true;
			}
			catch (System.Exception e)
			{
				Debug.LogWarning($"[TerrainRegionStorage] load 실패 {key}: {e.Message} — 재계산 fallback");
				return false;
			}
		}

		public static void Save(string key, float[,] region, int regionSize)
		{
			if (IsReady == false)
				return;
			try
			{
				int byteLen = regionSize * regionSize * sizeof(float);
				byte[] bytes = new byte[byteLen];
				System.Buffer.BlockCopy(region, 0, bytes, 0, byteLen);
				string path = FilePath(key);
				using (FileStream fs = new FileStream(path, FileMode.Create))
				using (GZipStream gz = new GZipStream(fs, CompressionMode.Compress))
				{
					gz.Write(bytes, 0, bytes.Length);
				}
			}
			catch (System.Exception e)
			{
				Debug.LogWarning($"[TerrainRegionStorage] save 실패 {key}: {e.Message} — 메모리 캐시만");
			}
		}
	}
}
