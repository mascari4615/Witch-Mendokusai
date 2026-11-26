using UnityEngine;

namespace WitchMendokusai
{
	// 이 오브젝트가 켜져 있는 동안만 쓴 유닛의 스탯을 올림. 카드 스탯 (gateStat) 을 받아야 켜짐. 쓰는 곳: 알리사 도는 방패 (회전하는 동안 방어력)
	public class StatWhileActive : SkillComponent
	{
		[SerializeField] private UnitStatType gateStat = UnitStatType.NONE;
		[SerializeField] private UnitStatType targetStat = UnitStatType.ARMOR;
		[SerializeField] private int amount = 33;

		private UnitStat subscribedStat;
		private SkillContext context;

		public override void InitContext(SkillObject skillObject) => context = skillObject.Context;

		private void OnEnable()
		{
			if (context == null || SkillSpawn.Stacks(context, gateStat) <= 0)
				return;
			subscribedStat = context.User.UnitStat;
			subscribedStat[targetStat] += amount;
		}

		private void OnDisable()
		{
			if (subscribedStat == null)
				return;
			subscribedStat[targetStat] -= amount;
			subscribedStat = null;
		}
	}
}
