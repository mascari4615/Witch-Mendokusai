using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace WitchMendokusai
{
	public class ResourceNodeSpawner : MonoBehaviour
	{
		public class ResourceNodeWaveInstance
		{
			public ResourceNodeWave Data { get; set; }
			public float SpawnT { get; set; }
			public int ActiveCount { get; set; }

			public ResourceNodeWaveInstance(ResourceNodeWave wave)
			{
				Data = wave;
				SpawnT = 0f;
				ActiveCount = 0;
			}
		}

		[field: Header("_" + nameof(ResourceNodeSpawner))]
		[SerializeField] private Vector2 spawnDistanceRange = new(5f, 12f);

		private readonly List<ResourceNodeWaveInstance> waves = new();

		public void InitWaves(Dungeon dungeon)
		{
			StopAllCoroutines();
			waves.Clear();

			foreach (ResourceNodeWave wave in dungeon.ResourceNodeWaves)
				waves.Add(new ResourceNodeWaveInstance(wave));
		}

		public void UpdateWaves()
		{
			for (int i = waves.Count - 1; i >= 0; i--)
				UpdateWave(i);
		}

		private void UpdateWave(int waveIndex)
		{
			ResourceNodeWaveInstance waveInstance = waves[waveIndex];

			TimeSpan dungeonTime = DungeonManager.Instance.Context.InitialDungeonTime - DungeonManager.Instance.Context.DungeonCurTime;

			if (dungeonTime < TimeSpan.FromSeconds(waveInstance.Data.StartTime))
				return;

			if (dungeonTime > TimeSpan.FromSeconds(waveInstance.Data.EndTime))
			{
				waves.RemoveAt(waveIndex);
				return;
			}

			if (waveInstance.ActiveCount >= waveInstance.Data.MaxNodeCount)
				return;

			waveInstance.SpawnT += DungeonContext.TimeUpdateInterval.Milliseconds / 1000f;
			if (waveInstance.SpawnT >= waveInstance.Data.SpawnInterval)
			{
				SpawnNode(waveInstance);
				waveInstance.SpawnT = 0f;
			}
		}

		private void SpawnNode(ResourceNodeWaveInstance waveInstance)
		{
			ResourceNode data = waveInstance.Data.ResourceNodes[Random.Range(0, waveInstance.Data.ResourceNodes.Length)];
			Vector3 spawnPos = GetSpawnPosition();

			GameObject nodeObject = ObjectPoolManager.Instance.Spawn(data.Prefab);
			ResourceNodeObject nodeComponent = nodeObject.GetComponent<ResourceNodeObject>();
			nodeObject.transform.position = spawnPos;
			nodeComponent.Init(data);
			nodeObject.SetActive(true);

			waveInstance.ActiveCount++;
			nodeComponent.Health.OnDied += () => OnNodeDied(waveInstance, data);
		}

		private void OnNodeDied(ResourceNodeWaveInstance waveInstance, ResourceNode data)
		{
			waveInstance.ActiveCount--;

			if (waveInstance.Data.RespawnDelay > 0)
				StartCoroutine(RespawnAfterDelay(waveInstance, data));
		}

		private IEnumerator RespawnAfterDelay(ResourceNodeWaveInstance waveInstance, ResourceNode data)
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
