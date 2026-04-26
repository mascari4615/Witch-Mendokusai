using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 시야 거리에 맞춰 청크를 동적으로 비동기 로딩 및 풀링 관리하는 코어 시스템.
	/// </summary>
	[RequireComponent(typeof(ChunkPool))]
	public class ChunkManager : MonoBehaviour
	{
		[SerializeField] private Transform viewer;
		[SerializeField] private TerrainParameters terrainParameters;
		[SerializeField, Range(1, 10)] private int renderDistance = 2;

		private ChunkPool chunkPool;
		private ChunkPosition lastViewerPosition = new(int.MinValue, int.MinValue);

		private readonly Dictionary<ChunkPosition, GameObject> activeChunks = new();
		private readonly Dictionary<ChunkPosition, Chunk> activeChunkData = new();
		private readonly HashSet<ChunkPosition> generationQueue = new();

		// Task.Run 스레드에서 메인 스레드로 넘겨주기 위한 스레드 세이프 큐
		private readonly ConcurrentQueue<(ChunkPosition pos, ChunkMeshData meshData, Chunk chunkData)> completedTasks = new();

		private void Awake()
		{
			chunkPool = GetComponent<ChunkPool>();
			ChunkStorage.Initialize(Application.persistentDataPath);

			if (viewer == null)
				viewer = Camera.main?.transform;
		}

		private void Update()
		{
			if (viewer == null || terrainParameters == null)
				return;

			if (BlockRegistry.IsInitialized == false)
				return;

			ProcessCompletedTasks();
			UpdateViewerPosition();
		}

		private void ProcessCompletedTasks()
		{
			// 메인 스레드 병목(Mesh.SetVertices 등)을 방지하기 위해 프레임당 최대 2개만 적용
			int maxProcessedPerFrame = 2;
			while (maxProcessedPerFrame > 0 && completedTasks.TryDequeue(out var result))
			{
				generationQueue.Remove(result.pos);

				// 태스크가 완료되는 동안 플레이어가 빠르게 이동하여 시야 밖으로 나갔을 수 있으므로 재검사
				if (Mathf.Abs(result.pos.X - lastViewerPosition.X) <= renderDistance &&
					Mathf.Abs(result.pos.Z - lastViewerPosition.Z) <= renderDistance)
				{
					if (activeChunks.TryGetValue(result.pos, out GameObject existingChunk))
					{
						activeChunkData[result.pos] = result.chunkData;
						
						MeshFilter filter = existingChunk.GetComponent<MeshFilter>();
						result.meshData.ApplyToMesh(filter.sharedMesh);
						
						MeshCollider collider = existingChunk.GetComponent<MeshCollider>();
						if (collider != null)
						{
							collider.sharedMesh = null;
							collider.sharedMesh = filter.sharedMesh;
						}

						maxProcessedPerFrame--;
					}
					else
					{
						GameObject chunkGo = chunkPool.Get(result.pos);
						activeChunks[result.pos] = chunkGo;
						activeChunkData[result.pos] = result.chunkData;
						
						MeshFilter filter = chunkGo.GetComponent<MeshFilter>();
						result.meshData.ApplyToMesh(filter.sharedMesh);
						
						MeshCollider collider = chunkGo.GetComponent<MeshCollider>();
						if (collider != null)
						{
							collider.sharedMesh = null; // 강제 갱신을 위해 null 세팅
							collider.sharedMesh = filter.sharedMesh;
						}

						maxProcessedPerFrame--;
					}
				}
			}
		}

		private void UpdateViewerPosition()
		{
			// 플레이어의 실제 월드 좌표를 청크 좌표계로 변환
			int viewerChunkX = Mathf.FloorToInt(viewer.position.x / VoxelConstants.CHUNK_SIZE_X);
			int viewerChunkZ = Mathf.FloorToInt(viewer.position.z / VoxelConstants.CHUNK_SIZE_Z);
			ChunkPosition currentPosition = new(viewerChunkX, viewerChunkZ);

			if (currentPosition != lastViewerPosition)
			{
				lastViewerPosition = currentPosition;
				UpdateChunks(currentPosition);
			}
		}

		private void UpdateChunks(ChunkPosition centerPos)
		{
			// 1. 범위 밖 청크 제거 (풀로 반환)
			List<ChunkPosition> toRemove = new();
			foreach (var kvp in activeChunks)
			{
				ChunkPosition pos = kvp.Key;
				if (Mathf.Abs(pos.X - centerPos.X) > renderDistance || 
				    Mathf.Abs(pos.Z - centerPos.Z) > renderDistance)
				{
					toRemove.Add(pos);
					chunkPool.Release(kvp.Value);

					if (activeChunkData.TryGetValue(pos, out Chunk chunkData))
					{
						if (chunkData.IsDirty)
						{
							Task.Run(() => ChunkStorage.SaveChunk(chunkData));
						}
						activeChunkData.Remove(pos);
					}
				}
			}

			foreach (var pos in toRemove)
			{
				activeChunks.Remove(pos);
			}

			// 2. 범위 내 빈 청크 큐잉
			for (int x = -renderDistance; x <= renderDistance; x++)
			{
				for (int z = -renderDistance; z <= renderDistance; z++)
				{
					ChunkPosition pos = new(centerPos.X + x, centerPos.Z + z);
					if (activeChunks.ContainsKey(pos) == false && generationQueue.Contains(pos) == false)
					{
						generationQueue.Add(pos);
						EnqueueChunkGeneration(pos);
					}
				}
			}
		}

		private void EnqueueChunkGeneration(ChunkPosition pos)
		{
			// 백그라운드 스레드에서 청크 계산 진행 (메인 스레드 렉 제로)
			Task.Run(() =>
			{
				try
				{
					Chunk chunk = new(pos);
					
					// 세이브 파일이 있다면 불러오고, 없다면 노이즈로 생성
					if (!ChunkStorage.LoadChunk(chunk))
					{
						ChunkGenerator.Generate(chunk, terrainParameters);
						chunk.MarkClean(); // 갓 생성된 자연 상태는 Dirty가 아님
					}
					
					ChunkMeshData meshData = ChunkMesher.GenerateMeshData(chunk);
					
					completedTasks.Enqueue((pos, meshData, chunk));
				}
				catch (System.Exception e)
				{
					Debug.LogError($"[ChunkManager] 비동기 청크 생성 중 에러 발생: {e}");
				}
			});
		}

		/// <summary>
		/// 특정 월드 좌표의 블록 값을 반환합니다.
		/// 청크가 로드되어 있지 않다면 Air(0)를 반환합니다.
		/// </summary>
		public ushort GetBlock(int worldX, int worldY, int worldZ)
		{
			int cx = Mathf.FloorToInt((float)worldX / VoxelConstants.CHUNK_SIZE_X);
			int cz = Mathf.FloorToInt((float)worldZ / VoxelConstants.CHUNK_SIZE_Z);
			ChunkPosition pos = new(cx, cz);

			if (activeChunkData.TryGetValue(pos, out Chunk chunk))
			{
				int lx = worldX - (cx * VoxelConstants.CHUNK_SIZE_X);
				int lz = worldZ - (cz * VoxelConstants.CHUNK_SIZE_Z);
				return chunk.GetBlock(lx, worldY, lz);
			}
			return VoxelConstants.AIR_RUNTIME_ID;
		}

		/// <summary>
		/// 특정 월드 좌표의 블록을 수정하고, 해당 청크의 메쉬를 비동기로 다시 굽습니다.
		/// </summary>
		public void SetBlock(int worldX, int worldY, int worldZ, ushort runtimeId)
		{
			int cx = Mathf.FloorToInt((float)worldX / VoxelConstants.CHUNK_SIZE_X);
			int cz = Mathf.FloorToInt((float)worldZ / VoxelConstants.CHUNK_SIZE_Z);
			ChunkPosition pos = new(cx, cz);

			if (activeChunkData.TryGetValue(pos, out Chunk chunk))
			{
				int lx = worldX - (cx * VoxelConstants.CHUNK_SIZE_X);
				int lz = worldZ - (cz * VoxelConstants.CHUNK_SIZE_Z);
				
				// 같은 블록이면 무시
				if (chunk.GetBlock(lx, worldY, lz) == runtimeId) return;

				chunk.SetBlock(lx, worldY, lz, runtimeId);

				// 블록이 수정되었으므로 렌더링 갱신을 위해 큐에 다시 넣음
				// (이미 화면에 떠있으므로 existingMesh를 갈아끼우는 방식으로 동작함)
				Task.Run(() =>
				{
					ChunkMeshData meshData = ChunkMesher.GenerateMeshData(chunk);
					completedTasks.Enqueue((pos, meshData, chunk));
				});
			}
		}
	}
}
