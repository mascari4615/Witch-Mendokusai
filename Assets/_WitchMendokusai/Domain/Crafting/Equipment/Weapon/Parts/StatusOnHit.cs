using UnityEngine;
using UnityEngine.Serialization;

namespace WitchMendokusai
{
	// 맞은 유닛에 상태이상. 둔화는 비율과 보스 저항을 여기서, 나머지 수치는 UnitStatusConfig
	public class StatusOnHit : SkillComponent, IHitEffect
	{
		[SerializeField] private UnitStatusType statusType = UnitStatusType.Slow;
		[SerializeField, FormerlySerializedAs("slowSeconds"), Min(0f), Tooltip("지속 초")] private float statusSeconds = 1f;
		[SerializeField, Range(0f, 1f), Tooltip("둔화만. 이동 속도를 줄이는 비율")] private float slowFactor = 0.5f;
		[SerializeField, Range(0f, 1f), Tooltip("둔화만. 보스에게 줄어드는 둔화 비율")] private float bossResistance = 0.5f;

		private UnitObject user;

		public override void InitContext(SkillObject skillObject)
		{
			user = skillObject.Context != null ? skillObject.Context.User : null;
		}

		public void OnHit(DamagingObject source, UnitObject victim)
		{
			if (statusType != UnitStatusType.Slow)
			{
				UnitStatus.Apply(victim, statusType, user, statusSeconds);
				return;
			}
			float factor = slowFactor;
			if (victim is MonsterObject monster && monster.UnitData.Type == MonsterType.Boss)
				factor *= 1f - bossResistance;
			UnitStatus.ApplySlow(victim, user, statusSeconds, factor);
		}
	}
}
