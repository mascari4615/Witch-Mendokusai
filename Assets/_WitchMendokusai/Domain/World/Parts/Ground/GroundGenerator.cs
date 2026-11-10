using System.Collections.Generic;
using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	// 순간 이동 뒤 첫 모터 질의보다 먼저 발밑 지형 배치
	[DefaultExecutionOrder(-100)]
	public partial class GroundGenerator : MonoBehaviour
	{
		// 플레이어 주변 3 × 3 청크를 풀에서 배치
		[SerializeField] private GameObject groundPrefab;
		[SerializeField] private int chunkSize = 100;

		// 현재 청크
		private readonly Dictionary<Vector3Int, GameObject> chunks = new();
		private readonly List<Vector3Int> retiredChunks = new();
		private readonly HashSet<Vector3Int> currentCenters = new();
		private readonly HashSet<Vector3Int> nextCenters = new();
		private readonly HashSet<Vector3Int> requiredChunks = new();
		private static readonly HashSet<UnitMovement> anchors = new();

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetAnchors() => anchors.Clear();

		// 플레이어와 멀어져도 지면을 유지할 유닛의 활성 주기 등록
		public static void RegisterAnchor(UnitMovement anchor) => anchors.Add(anchor);
		public static void UnregisterAnchor(UnitMovement anchor) => anchors.Remove(anchor);

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
			ReleaseDecorations();
			foreach (GameObject chunk in chunks.Values)
				chunk.SetActive(false);
			chunks.Clear();
			currentCenters.Clear();
		}

		private Vector3Int ChunkCenter(Vector3 position) => new Vector3Int(
			Mathf.RoundToInt(position.x / chunkSize), 0,
			Mathf.RoundToInt(position.z / chunkSize)) * chunkSize;

		private void FixedUpdate()
		{
			// 플레이어 생성 이전에는 지형 중심 미확정
			if (playerProvider.CurrentObject == null)
				return;
			nextCenters.Clear();
			nextCenters.Add(ChunkCenter(playerProvider.CurrentObject.UnitMovement.Position));
			foreach (UnitMovement anchor in anchors)
				if (anchor != null && anchor.isActiveAndEnabled)
					nextCenters.Add(ChunkCenter(anchor.Position));
			if (chunks.Count > 0 && currentCenters.SetEquals(nextCenters))
				return;
			currentCenters.Clear();
			currentCenters.UnionWith(nextCenters);
			requiredChunks.Clear();
			foreach (Vector3Int center in currentCenters)
				for (int x = -1; x <= 1; x++)
					for (int z = -1; z <= 1; z++)
						requiredChunks.Add(center + new Vector3Int(x, 0, z) * chunkSize);

			// 멀어진 청크는 비활성화
			retiredChunks.Clear();
			foreach (Vector3Int key in chunks.Keys)
			{
				if (requiredChunks.Contains(key) == false)
					retiredChunks.Add(key);
			}
			foreach (Vector3Int key in retiredChunks)
			{
				chunks[key].SetActive(false);
				chunks.Remove(key);
			}

			// 중첩 영역은 공유하고 등록 유닛이 떠난 지면만 회수
			foreach (Vector3Int chunkPosition in requiredChunks)
			{
				if (chunks.ContainsKey(chunkPosition) == false)
				{
					GameObject chunk = objectPoolManager.Spawn(groundPrefab);
					chunk.transform.position = chunkPosition;
					chunks[chunkPosition] = chunk;
					chunk.SetActive(true);
				}
			}

			Physics.SyncTransforms();
		}
	}
}
