using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace WitchMendokusai
{
	public class MonsterSpawner : MonoBehaviour
	{
		public class MonsterWaveInstance
		{
			public MonsterWave Data { get; set; }
			public float SpawnT { get; set; }

			public MonsterWaveInstance(MonsterWave wave, float spawnT)
			{
				Data = wave;
				SpawnT = spawnT;
			}
		}

		[field: Header("_" + nameof(MonsterSpawner))]
		[SerializeField] private GameObject spawnCirclePrefab;
		private readonly List<MonsterWaveInstance> waves = new();
		[SerializeField] private float spawnDelay = .2f;
		[SerializeField] private float spawnRange;

		public void InitWaves(Dungeon curDungeon)
		{
			// Debug.Log(nameof(InitWaves));
			waves.Clear();

			foreach (MonsterWave monsterWave in curDungeon.MonsterWaves)
				waves.Add(new MonsterWaveInstance(monsterWave, 0f));
		}

		public void UpdateWaves()
		{
			for (int i = waves.Count - 1; i >= 0; i--)
				UpdateWave(i);
		}

		private void UpdateWave(int waveIndex)
		{
			// waveInstance 대신 index를 전달받아 쓰는 이유

			// 이 함수 내에서 특정 조건이 충족하면 waves에서 waveInstance를 지워야 하는데,
			// 이때, 요소를 찾기 위해 리스트를 탐색하는 Remove보다 RemoveAt이 더 빠르기 때문.
			// (웨이브 수가 많지는 않을 것 같아서 효과는 미미하겠지만..)

			// 요소가 삭제되면 waves.Count가 줄어들기 때문에 UpdateWaves에서
			// 그냥 waves.Count를 기준으로 i++ for문을 돌리면 예외가 발생한다.
			// 그래서 waves.Count - 1부터 0까지 역순으로 for문을 돌린다.

			// 어차피 index만 알고 있으면 현재 단계에서 처리할 waveInstance에 접근할 수 있으므로
			// 굳이 처리할 waveInstance까지는 전달받지 않아도 된다.
			MonsterWaveInstance waveInstance = waves[waveIndex];

			TimeSpan dungeonTime = DungeonManager.Instance.Context.InitialDungeonTime - DungeonManager.Instance.Context.DungeonCurTime;
			DungeonDifficulty curDifficulty = DungeonManager.Instance.Context.CurDifficulty;

			if (dungeonTime < TimeSpan.FromSeconds(waveInstance.Data.StartTime))
				return;

			if (dungeonTime > TimeSpan.FromSeconds(waveInstance.Data.EndTime))
			{
				waves.RemoveAt(waveIndex);
				return;
			}

			waveInstance.SpawnT += DungeonContext.TimeUpdateInterval.Milliseconds / 1000f;
			if (waveInstance.SpawnT > waveInstance.Data.SpawnDelay)
			{
				StartCoroutine(SpawnMonster(waveInstance.Data.Monsters[Random.Range(0, waveInstance.Data.Monsters.Length)], spawnDelay, curDifficulty));
				waveInstance.SpawnT = 0;

				if (waveInstance.Data.Once)
					waves.RemoveAt(waveIndex);
			}
		}

		private IEnumerator SpawnMonster(Monster monster, float spawnDelay, DungeonDifficulty difficulty)
		{
			Vector3 randomOffset = Random.insideUnitCircle * spawnRange;
			randomOffset.z = randomOffset.y;
			randomOffset.y = 0;

			Vector3 spawnPos = transform.position + randomOffset;

			GameObject spawnCircle = ObjectPoolManager.Instance.Spawn(spawnCirclePrefab);
			spawnCircle.transform.position = spawnPos;
			spawnCircle.SetActive(true);
			ObjectBufferManager.AddObject(ObjectType.SpawnCircle, spawnCircle);

			yield return new WaitForSeconds(spawnDelay);

			GameObject monsterObject = ObjectPoolManager.Instance.Spawn(monster.Prefab);
			MonsterObject monsterObjectComponent = monsterObject.GetComponent<MonsterObject>();
			monsterObject.transform.position = spawnPos;
			monsterObjectComponent.Init(monster);
			monsterObject.SetActive(true);
		}

		public void StopWave()
		{
			Debug.Log(nameof(StopWave));
			StopAllCoroutines();
		}
	}
}