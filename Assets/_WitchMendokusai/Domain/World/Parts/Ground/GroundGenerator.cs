using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	public class GroundGenerator : MonoBehaviour
	{
		// 공간을 청크로 구분하고 (N * N 유닛의 공간)
		// 플레이어 주변 청크만 활성화

		// 일단 서바이벌-라이크를 구현하기 위해서
		// 플레이어가 있는 청크, 플레이어 앞뒤 양쪽 청크를 활성화
		// 총 5개의 청크가 활성화 됨

		// 이때, 매번 새로운 청크를 만들지 않고, 반대쪽 청크를 옮기는 식으로 구현

		[SerializeField] private GameObject groundPrefab;
		[SerializeField] private int chunkSize = 100;

		// 현재 청크
		private readonly Dictionary<Vector3Int, GameObject> chunks = new();
		private Vector3Int currentChunkCoord = Vector3Int.zero;

		private void OnEnable()
		{
			StartCoroutine(GenerateChunks());
		}

		private void OnDisable()
		{
			StopCoroutine(GenerateChunks());
			foreach (var chunk in chunks.Values)
				chunk.SetActive(false);
			chunks.Clear();
		}

		private IEnumerator GenerateChunks()
		{
			WaitForSeconds wait = new(1f);

			while (true)
			{
				// 플레이어 위치를 Chunk 좌표로 변환 (현재 안에 있는 청크 좌표)
				Vector3 playerPos = Player.Instance.transform.position;

				// 안에 있는지 확인하는 방법: x랑 y가 -(chunkSize / 2) ~ +(chunkSize / 2) 안에 있다면 청크 안에 있는거임
				// 청크사이즈 100이면, x -50 ~ 50, y -50 ~ 50은 (0, 0, 0)
				// currentChunkCoord = new Vector3Int(
				// 	Mathf.FloorToInt((playerPos.x - chunkSize / 2f) / chunkSize),
				// 	0,
				// 	Mathf.FloorToInt((playerPos.z - chunkSize / 2f) / chunkSize)
				// ) * chunkSize;
				currentChunkCoord = new Vector3Int(
					Mathf.RoundToInt(playerPos.x / chunkSize),
					0,
					Mathf.RoundToInt(playerPos.z / chunkSize)
				) * chunkSize;

				// 멀어진 청크는 비활성화
				foreach (Vector3Int key in new List<Vector3Int>(chunks.Keys))
				{
					if (Vector3Int.Distance(key, currentChunkCoord) > chunkSize)
					{
						chunks[key].SetActive(false);
						chunks.Remove(key);
					}
				}

				// 플레이어 주변으로 청크를 생성
				for (int x = -1; x <= 1; x++)
				{
					for (int z = -1; z <= 1; z++)
					{
						Vector3Int chunkPosition = currentChunkCoord + new Vector3Int(x, 0, z) * chunkSize;

						if (chunks.ContainsKey(chunkPosition) == false)
						{
							GameObject chunk = ObjectPoolManager.Instance.Spawn(groundPrefab);
							chunk.transform.position = chunkPosition;
							chunks[chunkPosition] = chunk;
							chunk.SetActive(true);
						}
					}
				}

				yield return wait;
			}
		}
	}
}
