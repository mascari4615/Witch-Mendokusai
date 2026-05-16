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

	// TASK-WM-107 Slice 4 — POCO Skill 이 static Bridge 안 알도록 app 서비스를 ctx 운반
	// (EffectContext/CriteriaContext 동형). PlayerProvider/ObjectPoolManager 는
	// VContainer 싱글턴 — *Bridge.Register(this) 동일 인스턴스라 behavior 무변경.
	public class SkillContext
	{
		public UnitObject User { get; private set; }
		public PlayerProvider PlayerProvider { get; private set; }
		public ObjectPoolManager ObjectPoolManager { get; private set; }
		public IItemData UsedEquipment { get; private set; }

		public SkillContext(UnitObject user, PlayerProvider playerProvider, ObjectPoolManager objectPoolManager, IItemData usedEquipment = null)
		{
			User = user;
			PlayerProvider = playerProvider;
			ObjectPoolManager = objectPoolManager;
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