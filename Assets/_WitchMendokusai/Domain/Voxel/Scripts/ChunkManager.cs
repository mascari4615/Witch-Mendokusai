using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	/// <summary>
	/// 시야 거리에 맞춰 청크를 동적으로 비동기 로딩 및 풀링 관리하는 코어 시스템.
	/// </summary>
	[RequireComponent(typeof(ChunkPool))]
	public class ChunkManager : MonoBehaviour
	{
		[SerializeField] private Transform viewer;
		[SerializeField, Range(1, 10)] private int renderDistance = 2;

		private TerrainParameters terrainParameters;
		private ChunkPool chunkPool;
		private ChunkPosition lastViewerPosition = new(int.MinValue, int.MinValue);

		private readonly Dictionary<ChunkPosition, GameObject> activeChunks = new();
		private readonly Dictionary<ChunkPosition, Chunk> activeChunkData = new();
		private readonly HashSet<ChunkPosition> generationQueue = new();

		// Task.Run 스레드에서 메인 스레드로 넘겨주기 위한 스레드 세이프 큐
		private readonly ConcurrentQueue<(ChunkPosition pos, ChunkMeshData meshData, Chunk chunkData)> completedTasks = new();

		private PlayerProvider playerProvider;

		[Inject]
		public void Construct(PlayerProvider playerProvider)
		{
			this.playerProvider = playerProvider;
		}

		private void Awake()
		{
			chunkPool = GetComponent<ChunkPool>();
			ChunkStorage.Initialize(Application.persistentDataPath);
			TerrainRegionStorage.Initialize(Application.persistentDataPath); // TASK-WM-119: erosion 영역 영속 (main thread 1회)

			terrainParameters = TerrainParametersService.Active;
			if (terrainParameters == null)
				Debug.LogError($"[ChunkManager] Active TerrainParameters를 찾지 못함. Resources/{TerrainParametersService.ACTIVE_RESOURCE_PATH} 확인.");
			else
				terrainParameters.EnsureHeightmapCache(); // main thread 에서 1회 캐시 — background chunk gen 안전

			// Inspector 미할당 시 — 플레이어 위치 우선, 그것도 없으면 Camera.main 안전망.
			// 3인칭 게임 (카메라 ≠ 플레이어 위치) 에서도 청크 LOD 가 플레이어 따라가도록.
			if (viewer == null)
			{
				if (playerProvider.Current != null)
					viewer = playerProvider.Current.transform;
				else
					viewer = Camera.main?.transform;
			}
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
			while (maxProcessedPerFrame > 0 && completedTasks.TryDequeue(out (ChunkPosition pos, ChunkMeshData meshData, Chunk chunkData) result))
			{
				generationQueue.Remove(result.pos);

				// 태스크가 완료되는 동안 플레이어가 빠르게 이동하여 시야 밖으로 나갔을 수 있으므로 재검사
				bool inRange = Mathf.Abs(result.pos.X - lastViewerPosition.X) <= renderDistance
					&& Mathf.Abs(result.pos.Z - lastViewerPosition.Z) <= renderDistance;
				if (inRange == false)
					continue;

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
						collider.sharedMesh = null;
						collider.sharedMesh = filter.sharedMesh;
					}

					// 자연 entity 인스턴스화 (결정적 RNG). 청크 자식으로 박힘 — Pool.Release가 정리.
					ChunkEntitySpawner.SpawnEntitiesForChunk(result.chunkData, chunkGo, terrainParameters);

					maxProcessedPerFrame--;
				}
			}
		}

		private void UpdateViewerPosition()
		{
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
			foreach (KeyValuePair<ChunkPosition, GameObject> kvp in activeChunks)
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
							Task.Run(() =>
							{
								lock (chunkData.SyncRoot)
								{
									ChunkStorage.SaveChunk(chunkData);
								}
							});
						}
						activeChunkData.Remove(pos);
					}
				}
			}

			foreach (ChunkPosition pos in toRemove)
				activeChunks.Remove(pos);

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
			Task.Run(() =>
			{
				try
				{
					Chunk chunk = new(pos);

					// 새 청크라 외부 참조 없음 — lock 불필요
					if (ChunkStorage.LoadChunk(chunk) == false)
					{
						ChunkGenerator.Generate(chunk, terrainParameters);
						chunk.MarkClean();
					}

					ChunkMeshData meshData = ChunkMesher.GenerateMeshData(chunk, terrainParameters);

					completedTasks.Enqueue((pos, meshData, chunk));
				}
				catch (System.Exception e)
				{
					Debug.LogError($"[ChunkManager] 비동기 청크 생성 중 에러 발생: {e}");
				}
			});
		}

		/// <summary>특정 월드 좌표의 블록 값을 반환. 청크가 로드되어 있지 않으면 Air 반환.</summary>
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

		/// <summary>특정 월드 좌표의 블록을 수정하고, 해당 청크의 메쉬를 비동기로 다시 굽는다.</summary>
		public void SetBlock(int worldX, int worldY, int worldZ, ushort runtimeId)
		{
			int cx = Mathf.FloorToInt((float)worldX / VoxelConstants.CHUNK_SIZE_X);
			int cz = Mathf.FloorToInt((float)worldZ / VoxelConstants.CHUNK_SIZE_Z);
			ChunkPosition pos = new(cx, cz);

			if (activeChunkData.TryGetValue(pos, out Chunk chunk) == false)
				return;

			int lx = worldX - (cx * VoxelConstants.CHUNK_SIZE_X);
			int lz = worldZ - (cz * VoxelConstants.CHUNK_SIZE_Z);

			bool changed = false;
			lock (chunk.SyncRoot)
			{
				if (chunk.GetBlock(lx, worldY, lz) != runtimeId)
				{
					chunk.SetBlock(lx, worldY, lz, runtimeId);
					changed = true;
				}
			}

			if (changed == false)
				return;

			// 메시 굽기는 백그라운드 + 같은 chunk에 대한 GenerateMeshData 동안 SetBlock과 race 안 나도록 lock
			Task.Run(() =>
			{
				ChunkMeshData meshData;
				lock (chunk.SyncRoot)
				{
					meshData = ChunkMesher.GenerateMeshData(chunk, terrainParameters);
				}
				completedTasks.Enqueue((pos, meshData, chunk));
			});
		}
	}
}
