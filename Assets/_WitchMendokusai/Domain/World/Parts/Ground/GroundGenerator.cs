using System.Collections.Generic;
using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	// 순간 이동 뒤 첫 모터 질의보다 먼저 발밑 지형 배치
	[DefaultExecutionOrder(-100)]
	public class GroundGenerator : MonoBehaviour
	{
		// 플레이어 주변 3 × 3 청크를 풀에서 배치
		[SerializeField] private GameObject groundPrefab;
		[SerializeField] private int chunkSize = 100;

		// 현재 청크
		private readonly Dictionary<Vector3Int, GameObject> chunks = new();
		private Vector3Int currentChunkCoord = Vector3Int.zero;
		private readonly List<Vector3Int> retiredChunks = new();

		private PlayerProvider playerProvider;
		private ObjectPoolManager objectPoolManager;

		[Inject]
		public void Construct(PlayerProvider playerProvider, ObjectPoolManager objectPoolManager)
		{
			this.playerProvider = playerProvider;
			this.objectPoolManager = objectPoolManager;
		}

		private void OnDisable()
		{
			foreach (GameObject chunk in chunks.Values)
				chunk.SetActive(false);
			chunks.Clear();
		}

		private void FixedUpdate()
		{
			// 플레이어 생성 이전에는 지형 중심 미확정
			if (playerProvider.CurrentObject == null)
				return;
			// 플레이어 위치를 Chunk 좌표로 변환 (현재 안에 있는 청크 좌표)
			Vector3 playerPos = playerProvider.CurrentObject.UnitMovement.Position;

			Vector3Int nextChunkCoord = new Vector3Int(
				Mathf.RoundToInt(playerPos.x / chunkSize),
				0,
				Mathf.RoundToInt(playerPos.z / chunkSize)
			) * chunkSize;
			if (chunks.Count > 0 && nextChunkCoord == currentChunkCoord)
				return;
			currentChunkCoord = nextChunkCoord;

			// 멀어진 청크는 비활성화
			retiredChunks.Clear();
			foreach (Vector3Int key in chunks.Keys)
			{
				if (Mathf.Abs(key.x - currentChunkCoord.x) > chunkSize || Mathf.Abs(key.z - currentChunkCoord.z) > chunkSize)
					retiredChunks.Add(key);
			}
			foreach (Vector3Int key in retiredChunks)
			{
				chunks[key].SetActive(false);
				chunks.Remove(key);
			}

			// 플레이어 주변으로 청크를 생성
			for (int x = -1; x <= 1; x++)
			{
				for (int z = -1; z <= 1; z++)
				{
					Vector3Int chunkPosition = currentChunkCoord + new Vector3Int(x, 0, z) * chunkSize;

					if (chunks.ContainsKey(chunkPosition) == false)
					{
						GameObject chunk = objectPoolManager.Spawn(groundPrefab);
						chunk.transform.position = chunkPosition;
						chunks[chunkPosition] = chunk;
						chunk.SetActive(true);
					}
				}
			}

			Physics.SyncTransforms();
		}
	}
}
