using UnityEngine;

namespace WitchMendokusai
{
	// 맞은 적에 출혈 상태이상. 카드 스탯 (gateStat) 을 받아야 켜짐. 틱과 겹침은 UnitStatusConfig. 쓰는 곳: 알리사 단도 출혈
	public class BleedOnHit : SkillComponent, IHitEffect
	{
		[SerializeField] private UnitStatType gateStat = UnitStatType.NONE;
		[SerializeField, Min(0.1f)] private float seconds = 3f;

		private SkillContext context;

		public override void InitContext(SkillObject skillObject) => context = skillObject.Context;

		public void OnHit(DamagingObject source, UnitObject victim)
		{
			if (victim.IsAlive && SkillSpawn.Stacks(context, gateStat) > 0)
				UnitStatus.Apply(victim, UnitStatusType.Bleed, context.User, seconds);
		}
	}
}
