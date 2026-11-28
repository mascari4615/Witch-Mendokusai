using UnityEngine;

namespace WitchMendokusai
{
	// 처형. 맞은 대상이 잃은 체력의 percent 만큼 피해를 더함. 체력이 많이 깎인 적일수록 아픔. 쓰는 곳: 스킬 재료 하늘 검 낙하
	public class MissingHealthDamage : SkillComponent, IDamageModifier
	{
		[SerializeField, Min(0)] private int percent = 30;

		public override void InitContext(SkillObject skillObject) { }

		public DamageInfo Modify(DamagingObject source, UnitObject victim, DamageInfo damageInfo)
		{
			int missing = Mathf.Max(0, victim.UnitStat[UnitStatType.HP_MAX] - victim.UnitStat[UnitStatType.HP_CUR]);
			damageInfo.damage += Mathf.RoundToInt(missing * percent / 100f);
			return damageInfo;
		}
	}
}
