using UnityEngine;

namespace WitchMendokusai
{
	// 카드 스탯을 받으면 이 탄의 피해를 퍼센트로 바꿈. 득실 묶음의 손해 쪽 (알리사 빛탄 3갈래: 탄당 70%)
	public class GatedDamageScale : SkillComponent, IDamageModifier
	{
		[SerializeField] private UnitStatType gateStat = UnitStatType.NONE;
		[SerializeField, Min(0)] private int percent = 70;

		private SkillContext context;

		public override void InitContext(SkillObject skillObject) => context = skillObject.Context;

		public DamageInfo Modify(DamagingObject source, UnitObject victim, DamageInfo damageInfo)
		{
			if (SkillSpawn.Stacks(context, gateStat) <= 0)
				return damageInfo;
			damageInfo.damage = Mathf.Max(1, Mathf.RoundToInt(damageInfo.damage * percent / 100f));
			return damageInfo;
		}
	}
}
