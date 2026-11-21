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
		// 적이 가장 몰린 지점 (DensityTargeting)
		DenseCluster = 5,
		// 마지막 이동 방향의 반대쪽, 플레이어 뒤 (자취)
		BehindPlayer = 6,
	}

	public enum SpawnAimStrategy
	{
		None = 0,
		Aim = 1,
		NearestEnemy = 2,
		RandomEnemy = 3,
		Clock = 4,
		RandomDirection = 5,
		// 마지막 이동 방향. 입력이 멈춰도 유지, 방향이 바뀌면 부드럽게 따라 돎
		LastMoveDirection = 6,
		// 적이 가장 몰린 지점 쪽
		DenseCluster = 7,
	}

	public partial class Spawner : SkillComponent
	{
		[SerializeField] private GameObject prefabToSpawn;
		[SerializeField, Tooltip("내보낼 때 공용 던지기 효과음 (CombatSfx WeaponThrow)")] private bool playThrowSfx = true;
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
		private IItemData usedEquipment;

		private PlayerProvider playerProvider;
		private ObjectPoolManager objectPoolManager;

		[Inject]
		public void Construct(PlayerProvider playerProvider, ObjectPoolManager objectPoolManager)
		{
			this.playerProvider = playerProvider;
			this.objectPoolManager = objectPoolManager;
		}

		private UnitStat PlayerStat => playerProvider.Current.UnitStat;
		// 구독한 대상에서 해제. 플레이어가 먼저 사라져도 OnDisable 이 안전
		private UnitStat subscribedStat;

		public float CurrentInterval
		{
			get
			{
				int stacks = attackSpeedBonusStatType != UnitStatType.NONE ? PlayerStat[attackSpeedBonusStatType] : 0;
				// 공용 공격속도 카드와 쿨타임 감소는 주기형 무기 전부가 같은 식으로 받음
				return AttackCadence.WeaponInterval(baseInterval, stacks, PlayerStat[UnitStatType.ATTACK_SPEED_BONUS], attackSpeedReductionPerStack, minIntervalRatio, PlayerStat[UnitStatType.COOLTIME_BONUS]);
			}
		}

		private void OnEnable()
		{
			if (damageBonusStatType != UnitStatType.NONE)
			{
				subscribedStat = PlayerStat;
				PlayerStat.AddListener(damageBonusStatType, UpdateDamageBonus);
				UpdateDamageBonus(PlayerStat[damageBonusStatType]);
			}
			clockAngle = 0f;
			angleCursor = 0f;
			EnablePreview();
			spawnCoroutine = StartCoroutine(Loop());
		}

		private void OnDisable()
		{
			if (spawnCoroutine != null)
				StopCoroutine(spawnCoroutine);
			if (damageBonusStatType != UnitStatType.NONE)
				subscribedStat?.RemoveListener(damageBonusStatType, UpdateDamageBonus);
			subscribedStat = null;
			DisablePreview();
		}

		private IEnumerator Loop()
		{
			while (true)
			{
				float interval = CurrentInterval;
				if (HasNextSpotTelegraph == false)
				{
					yield return new WaitForSeconds(interval);
					StartCoroutine(SpawnLoop(null));
					continue;
				}

				// 다음 자리를 미리 정해 바닥에 보이고, 예고가 끝나는 순간 그 자리에 냄
				float lead = Mathf.Min(telegraphLeadSeconds, interval);
				yield return new WaitForSeconds(interval - lead);
				ShotPlan[] plan = PlanVolley();
				ShowNextSpotTelegraph(plan, lead);
				yield return new WaitForSeconds(lead);
				StartCoroutine(SpawnLoop(plan));
			}
		}

		// plan 이 null 이면 발마다 그 순간 자리를 정함
		private IEnumerator SpawnLoop(ShotPlan[] plan)
		{
			int count = plan != null ? plan.Length : SpawnCount;
			float baseAngle = angleCursor;
			if (plan == null)
				AdvanceAngle(count);
			for (int i = 0; i < count; i++)
			{
				if (plan != null)
					Spawn(i, count, plan[i].Angle, true, plan[i].HasPosition, plan[i].Position);
				else
					Spawn(i, count, baseAngle + shotStepDegrees * i, false, false, Vector3.zero);
				if (SpawnDelay > 0f)
					yield return new WaitForSeconds(SpawnDelay);
			}
		}

		private void Spawn(int index, int count, float angle, bool planned, bool hasPlannedPosition, Vector3 plannedPosition)
		{
			transform.position = playerProvider.Current.transform.position;

			Vector3 position = plannedPosition;
			// 예고한 자리가 없던 발은 예고 없이 내지 않고 건너뜀
			if (planned)
			{
				if (hasPlannedPosition == false)
					return;
			}
			else if (TryGetSpawnPosition(index, count, angle, out position) == false)
				return;
			if (TryGetSpawnDirection(position, angle, out Vector3 direction) == false)
				return;

			SpawnOne(position, direction, index, count);
			if (spawnOpposite && direction != Vector3.zero)
				SpawnOne(position, -direction, index, count);
		}

		private void SpawnOne(Vector3 position, Vector3 direction, int index, int count)
		{
			bool hasDirection = direction != Vector3.zero;
			Quaternion rotation = hasDirection ? Quaternion.LookRotation(direction, Vector3.up) : Quaternion.identity;
			GameObject g = objectPoolManager.Spawn(prefabToSpawn, position, rotation);
			if (playThrowSfx)
				CombatSfx.Play(CombatSfxEvent.WeaponThrow, position);

			// 풀 재사용이라 프리팹 크기로 되돌린 뒤 공격 범위 강화, 장비 크기 카드 순서로 곱함
			g.transform.localScale = prefabToSpawn.transform.localScale;

			if (g.TryGetComponent(out SkillObject skillObject))
				skillObject.InitAttack(new SkillContext(playerProvider.CurrentObject, playerProvider, objectPoolManager, usedEquipment));

			g.transform.localScale *= ScaleMultiplier();

			if (hasDirection && g.TryGetComponent(out BulletMovement bulletMovement))
				bulletMovement.SetMoveDirection(direction);

			DamagingObject damagingObject = g.GetComponentInChildren<DamagingObject>(true);
			if (damagingObject != null)
				damagingObject.SetDamageBonus(damageBonus);
			else if (g.TryGetComponent(out ExplodeOnEnd explodeOnEnd))
				explodeOnEnd.SetDamageBonus(damageBonus);

			ShotInfo shot = new(index, count, direction);
			g.GetComponents(shotReceiverBuffer);
			foreach (IShotReceiver receiver in shotReceiverBuffer)
				receiver.OnShot(shot);
			shotReceiverBuffer.Clear();

			g.SetActive(true);
		}

		private float ScaleMultiplier()
		{
			if (scaleBonusStatType == UnitStatType.NONE)
				return 1f;
			return 1f + PlayerStat[scaleBonusStatType] * scalePerStack;
		}

		private bool TryGetSpawnPosition(int index, int count, float angle, out Vector3 position)
		{
			Vector3 origin = transform.position;
			switch (spawnPositionStrategy)
			{
				case SpawnPositionStrategy.NearestEnemy:
					return TryGetEnemyPosition(SurvivalTargeting.Nearest(origin, targetRange, null), out position);
				case SpawnPositionStrategy.RandomEnemy:
					return TryGetEnemyPosition(SurvivalTargeting.RandomInRange(origin, targetRange), out position);
				case SpawnPositionStrategy.Ring:
					// 고리는 발 사이를 고르게 나누고, 번마다 도는 시작각만 더함 (발 간격 누적분은 뺌)
					float ringAngle = (count > 0 ? 360f * index / count : 0f) + angle - shotStepDegrees * index;
					position = origin + Quaternion.Euler(0f, ringAngle, 0f) * (Vector3.forward * RandomRange);
					return true;
				case SpawnPositionStrategy.Player:
					position = origin;
					return true;
				case SpawnPositionStrategy.DenseCluster:
					return TryGetDensePoint(origin, out position);
				case SpawnPositionStrategy.BehindPlayer:
					position = origin - moveAim.Current * behindDistance;
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

		private bool TryGetSpawnDirection(Vector3 position, float angle, out Vector3 direction)
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
				case SpawnAimStrategy.LastMoveDirection:
					direction = reverseAim ? -moveAim.Current : moveAim.Current;
					break;
				case SpawnAimStrategy.DenseCluster:
					if (TryGetDensePoint(position, out Vector3 densePoint) == false)
						return false;
					direction = densePoint - position;
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
			if (angle != 0f)
				direction = Quaternion.Euler(0f, angle, 0f) * direction;
			return true;
		}

		private void UpdateDamageBonus(int damageBonus)
		{
			this.damageBonus = damageBonus;
		}

		public override void InitContext(SkillObject skillObject)
		{
			usedEquipment = skillObject.Context.UsedEquipment;
		}
	}
}
