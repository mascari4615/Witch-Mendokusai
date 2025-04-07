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

		[SerializeField] private bool useHitCount;
		[SerializeField] private int hitCount = 1;

		[SerializeField] private bool disableWhenInvaild;

		[SerializeField] private bool usedByPlayer = false;
		private bool vaild = true;
		private int curHitCount;

		private SkillObject skillObject;

		public void OnTriggerEnter(Collider other)
		{
			if (vaild == false)
				return;

			if (other.TryGetComponent(out IHitable hitable))
			{
				switch (hitable)
				{
					case MonsterObject when usedByPlayer:
					case PlayerObject when !usedByPlayer:
						// Debug.Log(nameof(OnTriggerEnter));
						hitable.ReceiveDamage(CalcDamage());
						if (useHitCount)
						{
							if (--curHitCount <= 0)
							{
								vaild = false;

								if (disableWhenInvaild)
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
			vaild = true;
			curHitCount = hitCount;
			damageBonus = 0;
		}

		private void TurnOff()
		{
			vaild = false;
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