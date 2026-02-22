using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	public class DamagingObject : SkillComponent
	{
		[field: Header("_" + nameof(DamagingObject))]
		[SerializeField] private int damage;
		private int damageBonus = 0;

		[SerializeField] private bool isTrigger = true;

		[SerializeField] private bool useHitCount;
		[SerializeField] private int hitCount = 1;

		[SerializeField] private bool disableWhenInvalid;

		[SerializeField] private bool usedByPlayer = false;
		private bool valid = true;
		private int curHitCount;

		private SkillObject skillObject;

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

		private void TryDamage(GameObject other)
		{
			if (other.TryGetComponent(out IDamageable damageable))
			{
				switch (damageable)
				{
					case MonsterObject when usedByPlayer:
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
			DamageInfo damageInfo = new()
			{
				type = DamageType.Normal
			};

			int calcDamage = damage + damageBonus;

			if (usedByPlayer)
			{
				UnitStat unitStat = Player.Instance.UnitStat;

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