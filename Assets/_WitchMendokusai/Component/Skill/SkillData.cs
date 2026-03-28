using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	public enum SkillPlayMode
	{
		Manual = 0,
		Auto = 1,
		AutoWhenDungeon = 2,
	}

	public class SkillContext
	{
		public UnitObject User { get; private set; }
		public EquipmentData UsedEquipment { get; private set; }

		public SkillContext(UnitObject user, EquipmentData usedEquipment = null)
		{
			User = user;
			UsedEquipment = usedEquipment;
		}
	}

	public abstract class SkillData : DataSO
	{
		[field: SerializeField] public SkillPlayMode PlayMode { get; set; }
		[field: SerializeField] public float Cooltime { get; set; }
		[field: SerializeField] public float PrevDelay { get; set; } = 0;
		[field: SerializeField] public float AfterDelay { get; set; } = 0;

		public void Use(SkillContext context)
		{
			context.User.StartCoroutine(SkillCoroutine(context));
		}

		public IEnumerator SkillCoroutine(SkillContext context)
		{
			yield return null;

			UnitObject unitObject = context.User;

			if (PrevDelay > 0)
			{
				unitObject.UnitStat[UnitStatType.CASTING_SKILL]++;
				yield return new WaitForSeconds(PrevDelay);
				unitObject.UnitStat[UnitStatType.CASTING_SKILL]--;
			}

			ActualUse(context);

			if (AfterDelay > 0)
			{
				unitObject.UnitStat[UnitStatType.CASTING_SKILL]++;
				yield return new WaitForSeconds(AfterDelay);
				unitObject.UnitStat[UnitStatType.CASTING_SKILL]--;
			}
		}

		public abstract void ActualUse(SkillContext context);
	}
}