using UnityEngine;

namespace WitchMendokusai
{
	// 낸 쪽이 탄마다 정하는 피해 배율 (퍼센트). 풀 재사용이라 판정 초기화 때 100 으로 되돌림
	// 쓰는 곳: 일제 사격 포화 탄 (판 안 누적, 나눠 쏘기, 집중)
	public class ScaledDamage : SkillComponent, IDamageModifier
	{
		private const int FULL_PERCENT = 100;
		private int percent = FULL_PERCENT;

		public override void InitContext(SkillObject skillObject) => percent = FULL_PERCENT;

		public void SetPercent(int value) => percent = value;

		public DamageInfo Modify(DamagingObject source, UnitObject victim, DamageInfo damageInfo)
		{
			if (percent != FULL_PERCENT)
				damageInfo.damage = Mathf.Max(1, Mathf.RoundToInt(damageInfo.damage * percent / 100f));
			return damageInfo;
		}
	}
}
