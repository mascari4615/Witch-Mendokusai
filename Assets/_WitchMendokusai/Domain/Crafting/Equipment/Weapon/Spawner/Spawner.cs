using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random;
using VContainer;

namespace WitchMendokusai
{
	public enum SpawnPositionStrategy
	{
		Random = 0,
		NearestEnemy = 1,
		RandomEnemy = 2,
		Ring = 3,
		Player = 4,
	}

	public enum SpawnAimStrategy
	{
		None = 0,
		Aim = 1,
		NearestEnemy = 2,
		RandomEnemy = 3,
		Clock = 4,
		RandomDirection = 5,
	}

	public class Spawner : SkillComponent
	{
		[SerializeField] private GameObject prefabToSpawn;
		[SerializeField] private SpawnPositionStrategy spawnPositionStrategy = SpawnPositionStrategy.Random;
		[SerializeField] private SpawnAimStrategy spawnAimStrategy = SpawnAimStrategy.None;

		[field: SerializeField] public float RandomRange { get; set; } = 3f;
		[field: SerializeField] public int SpawnCount { get; set; } = 1;
		[field: SerializeField] public float SpawnDelay { get; set; } = 0.1f;

		[Header("Cadence")]
		[SerializeField, Min(0.01f)] private float baseInterval = 1f;
		[SerializeField] private UnitStatType attackSpeedBonusStatType = UnitStatType.NONE;
		[SerializeField, Range(0f, 1f)] private float attackSpeedReductionPerStack = 0.15f;
		[SerializeField, Range(0.01f, 1f)] private float minIntervalRatio = 0.25f;

		[Header("Target")]
		[SerializeField, Min(0f)] private float targetRange = 14f;
		[SerializeField] private float clockStepDegrees = 30f;
		[SerializeField] private float spreadDegrees = 0f;
		[SerializeField] private bool spawnOpposite = false;

		[Header("Scale")]
		[SerializeField] private UnitStatType scaleBonusStatType = UnitStatType.NONE;
		[SerializeField, Min(0f)] private float scalePerStack = 0.1f;

		[SerializeField] private UnitStatType damageBonusStatType = UnitStatType.NONE;

		private Coroutine spawnCoroutine;
		private int damageBonus = 0;
		private float clockAngle = 0f;

		private PlayerProvider playerProvider;
		private ObjectPoolManager objectPoolManager;

		[Inject]
		public void Construct(PlayerProvider playerProvider, ObjectPoolManager objectPoolManager)
		{
			this.playerProvider = playerProvider;
			this.objectPoolManager = objectPoolManager;
		}

		private UnitStat PlayerStat => playerProvider.Current.UnitStat;

		public float CurrentInterval
		{
			get
			{
				int stacks = attackSpeedBonusStatType != UnitStatType.NONE ? PlayerStat[attackSpeedBonusStatType] : 0;
				return AttackCadence.Interval(baseInterval, stacks, attackSpeedReductionPerStack, minIntervalRatio);
			}
		}

		private void OnEnable()
		{
			if (damageBonusStatType != UnitStatType.NONE)
			{
				PlayerStat.AddListener(damageBonusStatType, UpdateDamageBonus);
				UpdateDamageBonus(PlayerStat[damageBonusStatType]);
			}
			clockAngle = 0f;
			spawnCoroutine = StartCoroutine(Loop());
		}

		private void OnDisable()
		{
			if (spawnCoroutine != null)
				StopCoroutine(spawnCoroutine);
			if (damageBonusStatType != UnitStatType.NONE)
				PlayerStat.RemoveListener(damageBonusStatType, UpdateDamageBonus);
		}

		private IEnumerator Loop()
		{
			while (true)
			{
				yield return new WaitForSeconds(CurrentInterval);
				StartCoroutine(SpawnLoop());
			}
		}

		private IEnumerator SpawnLoop()
		{
			int count = SpawnCount;
			for (int i = 0; i < count; i++)
			{
				Spawn(i, count);
				if (SpawnDelay > 0f)
					yield return new WaitForSeconds(SpawnDelay);
			}
		}

		private void Spawn(int index, int count)
		{
			transform.position = playerProvider.Current.transform.position;

			if (TryGetSpawnPosition(index, count, out Vector3 position) == false)
				return;
			if (TryGetSpawnDirection(position, out Vector3 direction) == false)
				return;

			SpawnOne(position, direction);
			if (spawnOpposite && direction != Vector3.zero)
				SpawnOne(position, -direction);
		}

		private void SpawnOne(Vector3 position, Vector3 direction)
		{
			bool hasDirection = direction != Vector3.zero;
			Quaternion rotation = hasDirection ? Quaternion.LookRotation(direction, Vector3.up) : Quaternion.identity;
			GameObject g = objectPoolManager.Spawn(prefabToSpawn, position, rotation);

			// 풀 재사용이라 프리팹 크기로 되돌린 뒤 공격 범위 강화, 장비 크기 카드 순서로 곱함
			g.transform.localScale = prefabToSpawn.transform.localScale;

			if (g.TryGetComponent(out SkillObject skillObject))
				skillObject.InitAttack(new SkillContext(playerProvider.CurrentObject, playerProvider, objectPoolManager));

			g.transform.localScale *= ScaleMultiplier();

			if (hasDirection && g.TryGetComponent(out BulletMovement bulletMovement))
				bulletMovement.SetMoveDirection(direction);

			DamagingObject damagingObject = g.GetComponentInChildren<DamagingObject>(true);
			if (damagingObject != null)
				damagingObject.SetDamageBonus(damageBonus);
			else if (g.TryGetComponent(out ExplodeOnEnd explodeOnEnd))
				explodeOnEnd.SetDamageBonus(damageBonus);

			g.SetActive(true);
		}

		private float ScaleMultiplier()
		{
			if (scaleBonusStatType == UnitStatType.NONE)
				return 1f;
			return 1f + PlayerStat[scaleBonusStatType] * scalePerStack;
		}

		private bool TryGetSpawnPosition(int index, int count, out Vector3 position)
		{
			Vector3 origin = transform.position;
			switch (spawnPositionStrategy)
			{
				case SpawnPositionStrategy.NearestEnemy:
					return TryGetEnemyPosition(SurvivalTargeting.Nearest(origin, targetRange, null), out position);
				case SpawnPositionStrategy.RandomEnemy:
					return TryGetEnemyPosition(SurvivalTargeting.RandomInRange(origin, targetRange), out position);
				case SpawnPositionStrategy.Ring:
					float angle = count > 0 ? 360f * index / count : 0f;
					position = origin + Quaternion.Euler(0f, angle, 0f) * (Vector3.forward * RandomRange);
					return true;
				case SpawnPositionStrategy.Player:
					position = origin;
					return true;
				default:
					position = origin + new Vector3(Random.Range(-RandomRange, RandomRange), 0f, Random.Range(-RandomRange, RandomRange));
					return true;
			}
		}

		private static bool TryGetEnemyPosition(Transform enemy, out Vector3 position)
		{
			position = enemy != null ? enemy.position : Vector3.zero;
			return enemy != null;
		}

		private bool TryGetSpawnDirection(Vector3 position, out Vector3 direction)
		{
			direction = Vector3.zero;
			switch (spawnAimStrategy)
			{
				case SpawnAimStrategy.Aim:
					direction = playerProvider.Current.AimDirection;
					break;
				case SpawnAimStrategy.NearestEnemy:
					Transform nearest = SurvivalTargeting.Nearest(position, targetRange, null);
					if (nearest == null)
						return false;
					direction = nearest.position - position;
					break;
				case SpawnAimStrategy.RandomEnemy:
					Transform randomEnemy = SurvivalTargeting.RandomInRange(position, targetRange);
					if (randomEnemy == null)
						return false;
					direction = randomEnemy.position - position;
					break;
				case SpawnAimStrategy.Clock:
					direction = Quaternion.Euler(0f, clockAngle, 0f) * Vector3.forward;
					clockAngle = Mathf.Repeat(clockAngle + clockStepDegrees, 360f);
					break;
				case SpawnAimStrategy.RandomDirection:
					direction = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
					break;
				default:
					return true;
			}

			direction.y = 0f;
			if (direction.sqrMagnitude < 0.0001f)
				direction = playerProvider.CurrentObject.transform.forward;
			direction.y = 0f;
			direction = direction.normalized;
			if (spreadDegrees > 0f)
				direction = Quaternion.Euler(0f, Random.Range(-spreadDegrees, spreadDegrees), 0f) * direction;
			return true;
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
