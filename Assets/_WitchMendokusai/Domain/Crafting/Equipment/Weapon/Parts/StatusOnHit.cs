using UnityEngine;

namespace WitchMendokusai
{
	public class StatusOnHit : SkillComponent, IHitEffect
	{
		[SerializeField, Range(0f, 1f)] private float slowFactor = 0.5f;
		[SerializeField, Min(0f)] private float slowSeconds = 1f;
		[SerializeField, Range(0f, 1f)] private float bossResistance = 0.5f;

		public override void InitContext(SkillObject skillObject)
		{

		}

		public void OnHit(DamagingObject source, UnitObject victim)
		{
			float factor = slowFactor;
			if (victim is MonsterObject monster && monster.UnitData.Type == MonsterType.Boss)
				factor *= 1f - bossResistance;
			UnitSlow.Apply(victim, factor, slowSeconds);
		}
	}
}
