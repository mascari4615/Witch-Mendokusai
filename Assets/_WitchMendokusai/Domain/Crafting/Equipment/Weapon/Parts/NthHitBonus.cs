using UnityEngine;

namespace WitchMendokusai
{
	// 같은 적을 n 번째 맞힐 때마다 고정 피해를 더함. 탄이 달라도 같은 무기면 이어 셈 (알리사 빛탄 3타)
	public class NthHitBonus : SkillComponent, IDamageModifier, IHitEffect
	{
		[SerializeField] private UnitStatType gateStat = UnitStatType.NONE;
		[SerializeField, Tooltip("세는 이름. 같은 수를 쓰는 탄끼리 이어 셈")] private int countKey;
		[SerializeField, Min(2)] private int everyNth = 3;
		[SerializeField, Min(0)] private int bonusDamage = 6;

		private SkillContext context;

		public override void InitContext(SkillObject skillObject) => context = skillObject.Context;

		private bool IsOn => SkillSpawn.Stacks(context, gateStat) > 0;

		public DamageInfo Modify(DamagingObject source, UnitObject victim, DamageInfo damageInfo)
		{
			if (victim == null || IsOn == false)
				return damageInfo;
			if ((HitMarks.HitCount(countKey, victim) + 1) % everyNth == 0)
				damageInfo.damage += bonusDamage;
			return damageInfo;
		}

		public void OnHit(DamagingObject source, UnitObject victim)
		{
			if (IsOn)
				HitMarks.AddHit(countKey, victim);
		}
	}
}
