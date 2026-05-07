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

			Debug.Log($"[ResourceNodeSpawner] InitWaves: {dungeon.ResourceNodeWaves?.Count ?? 0} waves in {dungeon.Name}");

			foreach (ResourceNodeWave wave in dungeon.ResourceNodeWaves)
			{
				WaveInstance waveInstance = new() { Data = wave, ActiveCount = 0 };
				waves.Add(waveInstance);

				int nodeTypeCount = wave.ResourceNodes?.Length ?? 0;
				Debug.Log($"[ResourceNodeSpawner] Wave: MaxNodeCount={wave.MaxNodeCount}, NodeTypes={nodeTypeCount}");

				for (int i = 0; i < wave.MaxNodeCount; i++)
					SpawnNode(waveInstance);
			}
		}

		private void SpawnNode(WaveInstance waveInstance)
		{
			if (waveInstance.Data.ResourceNodes == null || waveInstance.Data.ResourceNodes.Length == 0)
			{
				Debug.LogWarning("[ResourceNodeSpawner] Wave has no ResourceNodes — skip spawn");
				return;
			}

			ResourceNode data = waveInstance.Data.ResourceNodes[Random.Range(0, waveInstance.Data.ResourceNodes.Length)];

			if (data == null)
			{
				Debug.LogWarning("[ResourceNodeSpawner] ResourceNode is null — skip spawn");
				return;
			}

			if (data.Prefab == null)
			{
				Debug.LogWarning($"[ResourceNodeSpawner] {data.Name}.Prefab is null — skip spawn");
				return;
			}

			Vector3 spawnPos = GetSpawnPosition();

			GameObject nodeObject = ObjectPoolManager.Instance.Spawn(data.Prefab);
			if (nodeObject == null)
			{
				Debug.LogWarning($"[ResourceNodeSpawner] ObjectPoolManager returned null for prefab {data.Prefab.name}");
				return;
			}

			ResourceNodeObject nodeComponent = nodeObject.GetComponent<ResourceNodeObject>();
			if (nodeComponent == null)
			{
				Debug.LogWarning($"[ResourceNodeSpawner] {data.Prefab.name} has no ResourceNodeObject component");
				return;
			}

			nodeObject.transform.position = spawnPos;
			nodeComponent.Init(data);
			nodeObject.SetActive(true);

			Debug.Log($"[ResourceNodeSpawner] Spawned {data.Name} at {spawnPos} using {data.Prefab.name}");

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
