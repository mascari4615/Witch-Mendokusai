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
					// 사용자가 심은 entity 복원 (영구 보존 list).
					ChunkEntitySpawner.RestorePlantedEntities(result.chunkData, chunkGo);

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

			// 2. 범위 내 빈 청크 큐잉 — **가까운 곳부터**.
			//
			// ★ 사용자 실증: "처음에 빈공간 보이다가 차근차근 월드가 생성되는게 보입니다 …
			//   지금은 한쪽부터 생성되는듯. 마인크래프트처럼 플레이어가 나오는 곳부터 주위를
			//   차근차근 생성하는 방식이여야 할 것 같고".
			//   원인은 이 두 겹 반복이 -거리 → +거리 로 훑는 것이었다 — *구석에서 시작*하니
			//   내가 선 자리가 마지막에 채워진다. 만들 순서를 거리순으로 세우면 발밑부터 퍼진다.
			pendingOrder.Clear();
			for (int x = -renderDistance; x <= renderDistance; x++)
			{
				for (int z = -renderDistance; z <= renderDistance; z++)
				{
					ChunkPosition pos = new(centerPos.X + x, centerPos.Z + z);
					if (activeChunks.ContainsKey(pos) || generationQueue.Contains(pos))
						continue;
					pendingOrder.Add(pos);
				}
			}

			pendingOrder.Sort((left, right) =>
			{
				int leftDistance = (left.X - centerPos.X) * (left.X - centerPos.X) + (left.Z - centerPos.Z) * (left.Z - centerPos.Z);
				int rightDistance = (right.X - centerPos.X) * (right.X - centerPos.X) + (right.Z - centerPos.Z) * (right.Z - centerPos.Z);
				return leftDistance.CompareTo(rightDistance);
			});

			foreach (ChunkPosition pos in pendingOrder)
			{
				generationQueue.Add(pos);
				EnqueueChunkGeneration(pos);
			}
		}

		/// <summary>
		/// 처음 들어선 자리 주변이 다 채워졌나 — 「로딩 완료」가 이걸 기다려야 한다.
		///
		/// ★ 사용자 실증: "애초에 생성이 다 된 시점에 로딩이 완료되거나, 마인크래프트처럼 월드
		///   로딩할때 그 생성되는 UI 같은 걸 만들어야 할 것 같아요." 지금은 씬만 뜨면 로딩이 끝났다고
		///   말하고, 땅은 그 뒤에 채워진다 — 그래서 빈 공간에 떨어지는 그림이 보인다.
		/// 「기다릴 만큼」만 본다(반경 전체가 아니라 발밑 한 겹) — 멀리까지 기다리면 로딩이 하염없어진다.
		/// </summary>
		public bool IsInitialAreaReady
		{
			get
			{
				if (activeChunks.Count == 0)
					return false;

				for (int x = -1; x <= 1; x++)
				{
					for (int z = -1; z <= 1; z++)
					{
						if (activeChunks.ContainsKey(new ChunkPosition(lastViewerPosition.X + x, lastViewerPosition.Z + z)) == false)
							return false;
					}
				}
				return true;
			}
		}

		// 이번 갱신에서 만들 청크를 거리순으로 세우는 임시 목록 — 매 프레임 새로 할당하지 않는다.
		private readonly List<ChunkPosition> pendingOrder = new();

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

		/// <summary>사용자가 월드 좌표에 entity 한 그루 심음. 영구 보존 + 즉시 인스턴스화. 청크 미활성 = false.</summary>
		public bool PlantEntityAt(Vector3 worldPosition, EntityData entity)
		{
			int cx = Mathf.FloorToInt(worldPosition.x / VoxelConstants.CHUNK_SIZE_X);
			int cz = Mathf.FloorToInt(worldPosition.z / VoxelConstants.CHUNK_SIZE_Z);
			ChunkPosition pos = new(cx, cz);

			if (activeChunkData.TryGetValue(pos, out Chunk chunk) == false)
				return false;
			if (activeChunks.TryGetValue(pos, out GameObject chunkGo) == false)
				return false;

			float localX = worldPosition.x - (cx * VoxelConstants.CHUNK_SIZE_X);
			float localZ = worldPosition.z - (cz * VoxelConstants.CHUNK_SIZE_Z);
			// chunkGo.transform.position.y = -(CHUNK_SIZE_Y/2). 그래서 local y = world y + Y/2.
			float localY = worldPosition.y + VoxelConstants.CHUNK_SIZE_Y / 2f;

			ChunkEntitySpawner.PlantUserEntity(chunk, chunkGo, entity, localX, localY, localZ);
			return true;
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
