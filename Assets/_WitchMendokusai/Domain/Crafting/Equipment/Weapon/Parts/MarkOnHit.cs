using UnityEngine;

namespace WitchMendokusai
{
	// 맞힌 적에 표식을 잠깐 붙임. 카드 스탯 (gateStat) 을 받아야 켜짐. 표식을 읽는 쪽은 MarkAmplify
	public class MarkOnHit : SkillComponent, IHitEffect
	{
		[SerializeField] private UnitStatType gateStat = UnitStatType.NONE;
		[SerializeField, Tooltip("표식 이름. 읽는 MarkAmplify 와 같은 수")] private int markKey;
		[SerializeField, Min(0f)] private float seconds = 2f;

		private SkillContext context;

		public override void InitContext(SkillObject skillObject) => context = skillObject.Context;

		public void OnHit(DamagingObject source, UnitObject victim)
		{
			if (SkillSpawn.Stacks(context, gateStat) <= 0)
				return;
			HitMarks.Mark(markKey, victim, seconds);
		}
	}
}
