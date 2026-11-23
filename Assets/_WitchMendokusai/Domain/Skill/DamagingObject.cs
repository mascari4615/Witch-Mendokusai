using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	public class DamagingObject : SkillComponent, IKinematicCollisionReceiver
	{
		[field: Header("_" + nameof(DamagingObject))]
		[SerializeField] private int damage;
		private int damageBonus = 0;

		[SerializeField] private bool isTrigger = true;

		[SerializeField] private bool useHitCount;
		[SerializeField] private int hitCount = 1;

		[SerializeField] private bool disableWhenInvalid;

		// Knockback / Hit-stop 디폴트 톤 — 중간. caller가 동적 보정 가능.
		// 0 으로 두면 knockback / hit-stop 없음 (예: 환경 데미지, 약 hit).
		[Header("Hit Feedback")]
		[SerializeField] private float knockbackForce = 12f;
		[SerializeField] private float knockbackDuration = 0.15f;
		[SerializeField] private float hitstopDuration = 0.05f;

		// 적중 규칙. 같은 프레임 같은 대상은 항상 1회. 기본값은 기존 동작 그대로
		[Header("Hit Rule")]
		[SerializeField, Tooltip("한 판정이 같은 대상을 한 번만")] private bool hitOncePerTarget;
		[SerializeField, Tooltip("대상이 범위를 나갔다 다시 들어와야 재적중")] private bool hitReenterToRehit;
		[SerializeField, Tooltip("넉백 방향. 기본은 공격자 반대")] private KnockbackDirectionKind knockbackDirectionKind = KnockbackDirectionKind.AwayFromAttacker;

		[SerializeField] private bool usedByPlayer = false;
		private bool valid = true;
		private int curHitCount;

		private SkillObject skillObject = null;
		// 아레나 매치 중이면 공격자의 MatchCombatant(팀 판정용). 비-아레나면 null → 레거시 적아판정.
		private MatchCombatant ownerCombatant = null;
		private readonly HitLedger hitLedger = new();
		private IHitEffect[] hitEffects;
		private IDamageModifier[] damageModifiers;
		private BulletMovement hitMovement;
		private bool hitMovementSearched;

		private PlayerProvider playerProvider;

		[Inject]
		public void Construct(PlayerProvider playerProvider)
		{
			this.playerProvider = playerProvider;
		}

		private void OnDisable()
		{
			hitLedger.Clear();
		}

		// 판정 규칙. 서브클래스가 재적중 간격을 덮음
		protected virtual HitRule Rule => new(hitOncePerTarget, hitReenterToRehit, 0f);

		private static long EntityKey(GameObject target) => (long)EntityId.ToULong(target.GetEntityId());

		private void OnTriggerExit(Collider other) => hitLedger.Leave(EntityKey(other.gameObject));

		private void OnCollisionExit(Collision collision) => hitLedger.Leave(EntityKey(collision.gameObject));

		public void OnTriggerEnter(Collider other)
		{
			if (isTrigger == false || valid == false)
				return;

			TryDamage(other.gameObject);
		}

		private void OnCollisionEnter(Collision collision)
		{
			if (isTrigger || valid == false)
				return;

			TryDamage(collision.gameObject);
		}

		public void OnKinematicCollisionEnter(Collider other)
		{
			if (isTrigger || valid == false)
				return;

			TryDamage(other.gameObject);
		}

		private void TryDamage(GameObject other)
		{
			if (IsOwnerSuspended())
				return;

			other.TryGetComponent(out IDamageable damageable);

			// 피격 무적 중인 유닛은 적중으로 치지 않음. 한 번만 맞히는 판정이나 관통 횟수를 아끼려고 기록 전에 거름
			// 관통 횟수는 소모해 탄이 무적 보스를 그냥 통과하지 않게 함
			if (damageable is UnitHealth invincibleHealth && invincibleHealth.IsHitInvincible)
			{
				SpendHitCount();
				return;
			}

			// 같은 프레임 중복 제거, 대상당 1회, 나갔다 와야 재적중, 재적중 간격
			if (hitLedger.TryHit(EntityKey(other), Time.time, Time.frameCount, Rule) == false)
				return;

			if (damageable != null)
			{
				if (damageable is UnitHealth unitHealth)
				{
					VictimKind victimKind = unitHealth.Unit switch
					{
						MonsterObject => VictimKind.Monster,
						ResourceNodeObject => VictimKind.ResourceNode,
						PlayerObject => VictimKind.Player,
						_ => VictimKind.Other,
					};

					bool ownerInMatch = ownerCombatant != null;
					bool victimInMatch = other.TryGetComponent(out MatchCombatant victimCombatant);

					bool shouldDamage = CombatRules.ShouldDamage(
						ownerInMatch, ownerInMatch ? ownerCombatant.TeamId : -1,
						victimInMatch, victimInMatch ? victimCombatant.TeamId : -1,
						usedByPlayer, victimKind);

					// 이미 죽은 대상 (사라지기 전 판정이 남은 시체) 은 적중이 아님. 같은 프레임 여러 탄이 처치 효과를 겹쳐 내거나 관통을 쓰지 않게
					if (shouldDamage && unitHealth.Unit.IsAlive)
					{
						unitHealth.Unit.ReceiveDamage(ModifyDamage(unitHealth.Unit, CalcDamage(unitHealth.Unit)));
						NotifyHitEffects(unitHealth.Unit);
						SpendHitCount();
					}
				}
				// 부서지는 물건. 주인공 무기만, 관통 횟수는 소모하지 않음
				else if (damageable is BreakableObject breakable && usedByPlayer)
				{
					breakable.ReceiveDamage(CalcDamage(null));
				}
			}
		}

		public override void InitContext(SkillObject skillObject)
		{
			hitLedger.Clear();
			this.skillObject = skillObject;
			usedByPlayer = skillObject.UsedByPlayer;
			valid = true;
			curHitCount = hitCount;
			damageBonus = 0;

			// 아레나 경로 판정용 — 공격자(skill User)의 MatchCombatant 캐싱. 비-아레나면 null.
			UnitObject owner = skillObject.Context != null ? skillObject.Context.User : null;
			ownerCombatant = owner != null ? owner.GetComponent<MatchCombatant>() : null;
		}

		private bool IsOwnerSuspended()
		{
			UnitObject owner = skillObject != null && skillObject.Context != null ? skillObject.Context.User : null;
			if (owner == null)
				owner = GetComponentInParent<UnitObject>();
			return owner != null && owner.IsActionSuspended;
		}

		private void SpendHitCount()
		{
			if (useHitCount == false)
				return;
			if (--curHitCount <= 0)
			{
				valid = false;

				if (disableWhenInvalid)
					TurnOff();
			}
		}

		private void TurnOff()
		{
			valid = false;
			gameObject.SetActive(false);
		}

		private DamageInfo ModifyDamage(UnitObject victim, DamageInfo damageInfo)
		{
			if (damageModifiers == null)
				damageModifiers = GetComponents<IDamageModifier>();
			foreach (IDamageModifier damageModifier in damageModifiers)
				damageInfo = damageModifier.Modify(this, victim, damageInfo);
			return damageInfo;
		}

		private void NotifyHitEffects(UnitObject victim)
		{
			if (hitEffects == null)
				hitEffects = GetComponents<IHitEffect>();
			foreach (IHitEffect hitEffect in hitEffects)
				hitEffect.OnHit(this, victim);
		}

		public void DealDirectDamage(UnitObject victim)
		{
			victim.ReceiveDamage(CalcDamage(victim));
		}

		private DamageInfo CalcDamage(UnitObject victim)
		{
			// 스킬로 생성하는 경우도 있고, 몸박 데미지도 있고 - 2026-03-22. KarmoDDrine
			// equipmentData / skillData 는 DataID 매핑 (DamageInfo dep 분리 — DomainSDK Combat 격상, TASK-WM-089).
			int equipmentDataId = (skillObject != null && skillObject.Context.UsedEquipment != null)
				? skillObject.Context.UsedEquipment.ID
				: DamageInfo.NO_DATA_ID;

			DamageInfo damageInfo = new()
			{
				type = DamageType.Normal,
				damageSource = skillObject != null ? skillObject.Context.User : GetComponent<UnitObject>(),
				equipmentDataId = equipmentDataId,
				skillDataId = DamageInfo.NO_DATA_ID,
				knockbackForce = knockbackForce,
				knockbackDuration = knockbackDuration,
				hitstopDuration = hitstopDuration,
			};

			Vector3? knockbackDirection = ResolveKnockbackDirection(victim);
			if (knockbackDirection.HasValue)
				damageInfo.knockbackDirectionOverride = knockbackDirection.Value.ToSim();

			int calcDamage = damage + damageBonus;

			if (usedByPlayer || ownerCombatant != null)
			{
				// 아레나면 공격자 본인 스탯(플레이어 하드코딩 대체), 레거시면 기존 플레이어 스탯.
				UnitStat unitStat = ownerCombatant != null
					? ownerCombatant.UnitObject.UnitStat
					: playerProvider.Current.UnitStat;

				// 계산 규칙은 판정 층에 있다 (TASK-WM-215). 주사위는 여기서 굴려 넘긴다 —
				// 게임은 전역 난수, 서버는 판마다 씨앗 고정 난수를 쓸 수 있게 된다.
				// (옛 주석의 NONDETERMINISTIC 문제는 이 분리로 풀린다: 굴리는 쪽만 갈아끼우면 된다.)
				int roll = UnityEngine.Random.Range(0, DamageCalculation.ROLL_RANGE);
				DamageOutcome outcome = DamageCalculation.Resolve(damage, damageBonus, unitStat, roll);

				calcDamage = outcome.Damage;
				if (outcome.IsCritical)
					damageInfo.type = DamageType.Critical;
			}
			else
				calcDamage = ScaleEnemyDamage(calcDamage, damageInfo.damageSource as UnitObject);

			damageInfo.damage = calcDamage;
			return damageInfo;
		}

		// 넉백 방향 종류. 기본 (공격자 반대) 이거나 정할 수 없으면 null 로 두어 기존 계산을 씀
		private Vector3? ResolveKnockbackDirection(UnitObject victim)
		{
			if (knockbackDirectionKind == KnockbackDirectionKind.AwayFromAttacker || victim == null)
				return null;
			if (hitMovementSearched == false)
			{
				hitMovement = GetComponent<BulletMovement>();
				hitMovementSearched = true;
			}
			Vector3 center = transform.position;
			Vector3 target = victim.transform.position;
			Vector3 forward = transform.forward;
			Vector3 velocity = hitMovement != null ? hitMovement.MoveDirection : Vector3.zero;
			if (KnockbackDirections.TryResolve(knockbackDirectionKind, target.x, target.z, center.x, center.z, forward.x, forward.z, velocity.x, velocity.z, out float directionX, out float directionZ) == false)
				return null;
			return new Vector3(directionX, 0f, directionZ);
		}

		// 던전 판 안 적 피해. 몸 접촉, 탄, 일반 적 장판 (MonsterAttack) 이 같은 길. 던전 밖이면 그대로
		public static int ScaleEnemyDamage(int calcDamage, UnitObject attacker)
		{
			if (DungeonManager.TryGetExistingInstance(out DungeonManager dungeonManager) == false || dungeonManager.IsDungeon == false)
				return calcDamage;
			// 시간이 지날수록 적 피해 상승 (SurvivalRunRules), 그 위에 위협 배율
			calcDamage += dungeonManager.RunRules.EnemyDamageBonus(dungeonManager.Context.ElapsedTime.TotalMinutes);
			// 공격한 적의 피해 % (던전 제약 DC_2 등). 올림이라 작은 피해에도 체감
			if (attacker != null && attacker.UnitStat[UnitStatType.DAMAGE_BONUS] != 0)
				calcDamage = Mathf.CeilToInt(calcDamage * (1f + attacker.UnitStat[UnitStatType.DAMAGE_BONUS] / 100f));
			return dungeonManager.CurThreat.ScaleEnemyDamage(calcDamage);
		}

		public void SetDamageBonus(int damageBonus)
		{
			this.damageBonus = damageBonus;
		}

		// 관통. 판정 초기화 (InitContext) 뒤에 부름
		public void AddHitCount(int extraHits)
		{
			if (extraHits > 0)
				curHitCount += extraHits;
		}
	}
}
