using UnityEngine;

namespace WitchMendokusai
{
	// 표식 (MarkOnHit) 이 붙은 적에게 피해를 늘림. 카드 스탯 (gateStat) 을 받아야 켜짐
	public class MarkAmplify : SkillComponent, IDamageModifier
	{
		[SerializeField] private UnitStatType gateStat = UnitStatType.NONE;
		[SerializeField] private int markKey;
		[SerializeField, Min(0)] private int bonusPercent = 20;

		private SkillContext context;

		public override void InitContext(SkillObject skillObject) => context = skillObject.Context;

		public DamageInfo Modify(DamagingObject source, UnitObject victim, DamageInfo damageInfo)
		{
			if (victim == null || SkillSpawn.Stacks(context, gateStat) <= 0 || HitMarks.IsMarked(markKey, victim) == false)
				return damageInfo;
			damageInfo.damage = Mathf.CeilToInt(damageInfo.damage * (1f + bonusPercent / 100f));
			return damageInfo;
		}
	}
}
