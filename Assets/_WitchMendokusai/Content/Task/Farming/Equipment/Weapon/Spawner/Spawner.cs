using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace WitchMendokusai
{
	public enum SpawnPositionStrategy
	{
		Random,
		Target
	}

	public class Spawner : SkillComponent
	{
		[SerializeField] private GameObject prefabToSpawn;
		[SerializeField] private SpawnPositionStrategy spawnPositionStrategy = SpawnPositionStrategy.Random;

		[field: SerializeField] public float RandomRange { get; set; } = 3f;
		[field: SerializeField] public int SpawnCount { get; set; } = 1;
		[field: SerializeField] public float SpawnDelay { get; set; } = 0.1f;

		private Coroutine spawnCoroutine;

		[SerializeField] private UnitStatType damageBonusStatType = UnitStatType.NONE;
		private UnitStat PlayerStat => Player.Instance.UnitStat;
		private int damageBonus = 0;

		private void Start()
		{
			if (damageBonusStatType != UnitStatType.NONE)
				PlayerStat.AddListener(damageBonusStatType, UpdateDamageBonus);
		}

		private void OnEnable()
		{
			spawnCoroutine = StartCoroutine(Loop());
		}

		private void OnDisable()
		{
			if (spawnCoroutine != null)
				StopCoroutine(spawnCoroutine);
		}

		private IEnumerator Loop()
		{
			while (true)
			{
				yield return new WaitForSeconds(1f);
				StartCoroutine(SpawnLoop());
			}
		}

		private IEnumerator SpawnLoop()
		{
			for (int i = 0; i < SpawnCount; i++)
			{
				Spawn();
				yield return new WaitForSeconds(SpawnDelay);
			}

			void Spawn()
			{
				// TODO: 위치도 옵션으로
				transform.position = Player.Instance.transform.position;
				Vector3 spawnPosition = GetSpawnPosition();
				GameObject g = ObjectPoolManager.Instance.Spawn(prefabToSpawn, transform.position + spawnPosition, Quaternion.identity);
				if (g.TryGetComponent(out SkillObject skillObject))
					skillObject.InitContext(Player.Instance.Object);

				if (g.GetComponentInChildren<DamagingObject>() is DamagingObject damagingObject)
					damagingObject.SetDamageBonus(damageBonus);

				g.SetActive(true);
			}
		}

		private Vector3 GetSpawnPosition() => spawnPositionStrategy switch
		{
			SpawnPositionStrategy.Random => GetRandomPosition(),
			SpawnPositionStrategy.Target => GetTargetPosition(),
			_ => Vector3.zero,
		};

		private Vector3 GetRandomPosition()
		{
			float x = Random.Range(-RandomRange, RandomRange);
			float z = Random.Range(-RandomRange, RandomRange);
			return new Vector3(x, 0f, z);
		}

		private Vector3 GetTargetPosition()
		{
			// Implement logic to get target position
			return Vector3.zero;
		}

		private void UpdateDamageBonus(int damageBonus)
		{
			this.damageBonus = damageBonus;
		}

		public override void InitContext(SkillObject skillObject)
		{

		}
	}
}