using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace WitchMendokusai
{
	public class ResourceNodeSpawner : MonoBehaviour
	{
		private class WaveInstance
		{
			public ResourceNodeWave Data;
			public int ActiveCount;
		}

		[field: Header("_" + nameof(ResourceNodeSpawner))]
		[SerializeField] private Vector2 spawnDistanceRange = new(5f, 12f);

		private readonly List<WaveInstance> waves = new();

		public void InitWaves(Dungeon dungeon)
		{
			StopAllCoroutines();
			waves.Clear();

			foreach (ResourceNodeWave wave in dungeon.ResourceNodeWaves)
			{
				WaveInstance waveInstance = new() { Data = wave, ActiveCount = 0 };
				waves.Add(waveInstance);

				for (int i = 0; i < wave.MaxNodeCount; i++)
					SpawnNode(waveInstance);
			}
		}

		private void SpawnNode(WaveInstance waveInstance)
		{
			ResourceNode data = waveInstance.Data.ResourceNodes[Random.Range(0, waveInstance.Data.ResourceNodes.Length)];
			Vector3 spawnPos = GetSpawnPosition();

			GameObject nodeObject = ObjectPoolManager.Instance.Spawn(data.Prefab);
			ResourceNodeObject nodeComponent = nodeObject.GetComponent<ResourceNodeObject>();
			nodeObject.transform.position = spawnPos;
			nodeComponent.Init(data);
			nodeObject.SetActive(true);

			waveInstance.ActiveCount++;
			nodeComponent.Health.OnDied += () => OnNodeDied(waveInstance);
		}

		private void OnNodeDied(WaveInstance waveInstance)
		{
			waveInstance.ActiveCount--;

			if (waveInstance.Data.RespawnDelay > 0)
				StartCoroutine(RespawnAfterDelay(waveInstance));
		}

		private IEnumerator RespawnAfterDelay(WaveInstance waveInstance)
		{
			yield return new WaitForSeconds(waveInstance.Data.RespawnDelay);

			if (waveInstance.ActiveCount < waveInstance.Data.MaxNodeCount)
				SpawnNode(waveInstance);
		}

		private Vector3 GetSpawnPosition()
		{
			Vector3 playerPos = Player.Instance.transform.position;
			Vector2 randomCircle = Random.insideUnitCircle.normalized;
			float randomDist = Random.Range(spawnDistanceRange.x, spawnDistanceRange.y);
			return playerPos + new Vector3(randomCircle.x, 0, randomCircle.y) * randomDist;
		}

		public void StopWave()
		{
			StopAllCoroutines();
			waves.Clear();
		}
	}
}
