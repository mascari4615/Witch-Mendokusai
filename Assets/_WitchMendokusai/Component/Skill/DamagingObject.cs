using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
		private Dictionary<GameObject, int> hitFrames = new();

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
					switch (unitHealth.Unit)
					{
						case MonsterObject when usedByPlayer:
						case ResourceNodeObject when usedByPlayer:
						case PlayerObject when !usedByPlayer:
							// Debug.Log(nameof(OnCollisionEnter));
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
							break;
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

			DamageInfo damageInfo = new()
			{
				type = DamageType.Normal,
				// 스킬로 생성하는 경우도 있고, 몸박 데미지도 있고 - 2026-03-22. KarmoDDrine
				damageSource = skillObject ? skillObject.Context.User : GetComponent<UnitObject>(),
				equipmentData = skillObject ? skillObject.Context.UsedEquipment : null,
				knockbackForce = knockbackForce,
				knockbackDuration = knockbackDuration,
				hitstopDuration = hitstopDuration,
			};

			int calcDamage = damage + damageBonus;

			if (usedByPlayer)
			{
				UnitStat unitStat = PlayerProvider.Instance.Current.UnitStat;

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