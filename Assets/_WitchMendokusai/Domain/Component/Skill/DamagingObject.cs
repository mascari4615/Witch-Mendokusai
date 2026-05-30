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

		[SerializeField] private bool usedByPlayer = false;
		private bool valid = true;
		private int curHitCount;

		private SkillObject skillObject = null;
		// 아레나 매치 중이면 공격자의 ArenaCombatant(팀 판정용). 비-아레나면 null → 레거시 적아판정.
		private ArenaCombatant ownerCombatant = null;
		private Dictionary<GameObject, int> hitFrames = new();

		private PlayerProvider playerProvider;

		[Inject]
		public void Construct(PlayerProvider playerProvider)
		{
			this.playerProvider = playerProvider;
		}

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
			if (hitFrames.TryGetValue(other, out int frame) && frame == Time.frameCount)
				return;
			hitFrames[other] = Time.frameCount;

			if (other.TryGetComponent(out IDamageable damageable))
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

					bool ownerInArena = ownerCombatant != null;
					bool victimInArena = other.TryGetComponent(out ArenaCombatant victimCombatant);

					bool shouldDamage = ArenaCombatRules.ShouldDamage(
						ownerInArena, ownerInArena ? ownerCombatant.TeamId : -1,
						victimInArena, victimInArena ? victimCombatant.TeamId : -1,
						usedByPlayer, victimKind);

					if (shouldDamage)
					{
						damageable.ReceiveDamage(CalcDamage());
						if (useHitCount)
						{
							if (--curHitCount <= 0)
							{
								valid = false;

								if (disableWhenInvalid)
									TurnOff();
							}
						}
					}
				}
			}
		}

		public override void InitContext(SkillObject skillObject)
		{
			this.skillObject = skillObject;
			usedByPlayer = skillObject.UsedByPlayer;
			valid = true;
			curHitCount = hitCount;
			damageBonus = 0;

			// 아레나 경로 판정용 — 공격자(skill User)의 ArenaCombatant 캐싱. 비-아레나면 null.
			UnitObject owner = skillObject.Context != null ? skillObject.Context.User : null;
			ownerCombatant = owner != null ? owner.GetComponent<ArenaCombatant>() : null;
		}

		private void TurnOff()
		{
			valid = false;
			gameObject.SetActive(false);
		}

		private DamageInfo CalcDamage()
		{
			if (skillObject)
			{
				Debug.Log($"SkillObject {skillObject}");
				Debug.Log($"SkillContext {skillObject.Context}");
				Debug.Log($"SkillContext User {skillObject.Context.User}");
				Debug.Log($"SkillContext UsedEquipment {skillObject.Context.UsedEquipment}");
			}
			else
			{
				Debug.Log("No SkillObject");
			}

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

			int calcDamage = damage + damageBonus;

			if (usedByPlayer || ownerCombatant != null)
			{
				// 아레나면 공격자 본인 스탯(플레이어 하드코딩 대체), 레거시면 기존 플레이어 스탯.
				UnitStat unitStat = ownerCombatant != null
					? ownerCombatant.UnitObject.UnitStat
					: playerProvider.Current.UnitStat;

				calcDamage = (int)(calcDamage * (1 + (unitStat[UnitStatType.DAMAGE_BONUS] / 100f)));

				if (unitStat[UnitStatType.CRITICAL_CHANCE] > 0)
				{
					if (UnityEngine.Random.Range(0, 100) < unitStat[UnitStatType.CRITICAL_CHANCE])
					{
						calcDamage = (int)(calcDamage * (1 + (unitStat[UnitStatType.CRITICAL_DAMAGE] / 100f)));
						damageInfo.type = DamageType.Critical;
					}
				}
			}

			damageInfo.damage = calcDamage;
			return damageInfo;
		}

		public void SetDamageBonus(int damageBonus)
		{
			this.damageBonus = damageBonus;
		}
	}
}
