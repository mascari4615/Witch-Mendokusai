using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	// 상시 동료. 평소엔 곁을 따라다니다 쿨마다 가까운 적 옆으로 달려가 돌진, 멀어지면 끌려옴
	public class EquipmentYarnBallCompanion : SkillComponent
	{
		[SerializeField] private GameObject bodyPrefab;
		[SerializeField] private GameObject hitPrefab;
		[SerializeField, Min(0.1f)] private float cooldown = 8f;
		[SerializeField, Min(1)] private int rushesPerCooldown = 2;
		[SerializeField, Min(0.1f)] private float searchRadius = 8f;
		[SerializeField, Min(0.1f)] private float stopDistance = 1.5f;
		[SerializeField, Min(0.1f)] private float dashDuration = 0.6f;
		[SerializeField, Min(0.1f)] private float pullDistance = 10f;
		[SerializeField, Min(0.1f)] private float followSpeed = 4f;
		[SerializeField, Min(1f)] private float farFollowMultiplier = 5f;
		[SerializeField, Min(0.1f)] private float approachSpeed = 14f;
		[SerializeField, Min(0.1f)] private float dashSpeed = 12f;
		[SerializeField, Min(0.1f)] private float followRadius = 2.5f;
		[SerializeField, Min(0.1f)] private float farFollowDistance = 4f;
		[SerializeField, Min(0.05f)] private float hitInterval = 0.12f;
		[SerializeField, Min(0.1f)] private float hitScale = 0.5f;
		[SerializeField, Min(0.1f)] private float wanderInterval = 1.5f;
		[SerializeField, Range(0f, 1f)] private float cooldownReductionPerStack = 0.15f;
		[SerializeField, Range(0.01f, 1f)] private float minCooldownRatio = 0.25f;
		[SerializeField] private UnitStatType damageBonusStat = UnitStatType.NONE;

		private SkillObject owner;
		private CompanionRushState state;
		private GameObject body;
		private GameObject target;
		private Vector3 wanderAnchor;
		private float wanderRemain;
		private float hitRemain;
		private Vector3 dashDirection;

		private PlayerProvider playerProvider;
		private ObjectPoolManager objectPoolManager;

		public CompanionRushState State => state;
		public Transform Body => body != null ? body.transform : null;

		[Inject]
		public void Construct(PlayerProvider playerProvider, ObjectPoolManager objectPoolManager)
		{
			this.playerProvider = playerProvider;
			this.objectPoolManager = objectPoolManager;
		}

		public override void InitContext(SkillObject skillObject)
		{
			owner = skillObject;
			state = new CompanionRushState(cooldown, dashDuration, pullDistance);
			ReleaseBody();
		}

		private void OnEnable()
		{
			state ??= new CompanionRushState(cooldown, dashDuration, pullDistance);
			body = objectPoolManager.Spawn(bodyPrefab);
			body.transform.position = CompanionTargeting.Flat(playerProvider.Current.transform.position);
			body.SetActive(true);
		}

		private void OnDisable() => ReleaseBody();

		private void Update()
		{
			if (body == null)
				return;
			Vector3 playerPosition = playerProvider.Current.transform.position;
			transform.position = playerPosition;

			Vector3 position = body.transform.position;
			float ownerDistance = Vector3.Distance(CompanionTargeting.Flat(playerPosition), position);
			if (state.ShouldPull(ownerDistance))
			{
				body.transform.position = CompanionTargeting.Flat(playerPosition);
				state.Abort();
				target = null;
				return;
			}

			switch (state.Phase)
			{
				case CompanionPhase.Follow:
					UpdateFollow(playerPosition, ownerDistance);
					break;
				case CompanionPhase.Approach:
					UpdateApproach();
					break;
				case CompanionPhase.Dash:
					UpdateDash();
					break;
			}
		}

		private void UpdateFollow(Vector3 playerPosition, float ownerDistance)
		{
			state.TickFollow(Time.deltaTime * CompanionTargeting.CooldownRate(playerProvider.Current.UnitStat, cooldownReductionPerStack, minCooldownRatio));

			wanderRemain -= Time.deltaTime;
			if (wanderRemain <= 0f)
			{
				Vector2 circle = Random.insideUnitCircle * followRadius;
				wanderAnchor = new Vector3(circle.x, 0f, circle.y);
				wanderRemain = wanderInterval;
			}

			float speed = ownerDistance > farFollowDistance ? followSpeed * farFollowMultiplier : followSpeed;
			Vector3 goal = CompanionTargeting.Flat(playerPosition + wanderAnchor);
			body.transform.position = Vector3.MoveTowards(body.transform.position, goal, speed * Time.deltaTime);

			if (state.IsReady == false)
				return;
			target = CompanionTargeting.NearestMonster(playerPosition, searchRadius);
			if (target != null)
				state.BeginRush(rushesPerCooldown);
		}

		private void UpdateApproach()
		{
			if (target == null || target.activeInHierarchy == false)
			{
				// 닿기 전에 목표가 사라지면 횟수를 쓰지 않고 새 목표, 없으면 중단
				target = CompanionTargeting.NearestMonster(body.transform.position, searchRadius);
				if (target == null)
					state.Abort();
				return;
			}

			Vector3 position = body.transform.position;
			Vector3 targetPosition = CompanionTargeting.Flat(target.transform.position);
			Vector3 fromTarget = position - targetPosition;
			Vector3 side = fromTarget.sqrMagnitude > 0.0001f ? fromTarget.normalized : Vector3.right;
			Vector3 goal = targetPosition + side * stopDistance;
			body.transform.position = Vector3.MoveTowards(position, goal, approachSpeed * Time.deltaTime);

			if ((goal - body.transform.position).sqrMagnitude < 0.01f)
			{
				dashDirection = (targetPosition - body.transform.position).normalized;
				hitRemain = 0f;
				state.ArriveAtTarget();
			}
		}

		private void UpdateDash()
		{
			body.transform.position += dashSpeed * Time.deltaTime * dashDirection;

			hitRemain -= Time.deltaTime;
			if (hitRemain <= 0f)
			{
				hitRemain = hitInterval;
				SpawnHit(body.transform.position);
			}

			if (state.TickDash(Time.deltaTime))
				RetargetOrFinish();
		}

		private void RetargetOrFinish()
		{
			target = CompanionTargeting.NearestMonster(body.transform.position, searchRadius);
			if (target == null)
			{
				state.Abort();
				return;
			}
			if (state.NextRushOrFinish() == false)
				target = null;
		}

		private void SpawnHit(Vector3 position)
		{
			GameObject hit = objectPoolManager.Spawn(hitPrefab, CompanionTargeting.Flat(position), Quaternion.identity);
			if (hit.TryGetComponent(out SkillObject skillObject))
				skillObject.InitAttack(owner.Context);
			hit.transform.localScale *= hitScale;
			DamagingObject damagingObject = hit.GetComponentInChildren<DamagingObject>(true);
			if (damagingObject != null)
				damagingObject.SetDamageBonus(playerProvider.Current.UnitStat[damageBonusStat]);
			hit.SetActive(true);
		}

		private void ReleaseBody()
		{
			if (body != null && ObjectPoolManager.TryGetExistingInstance(out _))
				objectPoolManager.Despawn(body);
			body = null;
			target = null;
		}
	}
}
