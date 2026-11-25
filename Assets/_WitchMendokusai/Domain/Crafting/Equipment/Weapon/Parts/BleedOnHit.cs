using System.Collections;
using UnityEngine;

namespace WitchMendokusai
{
	// 맞은 적이 잠깐 지속 피해. 다시 맞으면 시간만 새로 (겹치지 않음). 카드 스탯 (gateStat) 을 받아야 켜짐. 쓰는 곳: 알리사 단도 출혈
	public class BleedOnHit : SkillComponent, IHitEffect
	{
		[SerializeField] private UnitStatType gateStat = UnitStatType.NONE;
		[SerializeField, Tooltip("표식 이름. 다른 표식과 겹치지 않게")] private int markKey;
		[SerializeField, Min(0)] private int damagePerTick = 3;
		[SerializeField, Min(0.05f)] private float tickSeconds = 1f;
		[SerializeField, Min(0.1f)] private float seconds = 3f;

		private SkillContext context;

		public override void InitContext(SkillObject skillObject) => context = skillObject.Context;

		public void OnHit(DamagingObject source, UnitObject victim)
		{
			if (victim.IsAlive == false || SkillSpawn.Stacks(context, gateStat) <= 0)
				return;
			bool bleeding = HitMarks.IsMarked(markKey, victim);
			HitMarks.Mark(markKey, victim, seconds);
			if (bleeding == false)
				CombatFxRunner.Instance.Run(Bleed(victim, context, markKey, damagePerTick, tickSeconds));
		}

		// 이 오브젝트는 풀로 돌아가므로 값을 넘겨 실행기에서 돎
		private static IEnumerator Bleed(UnitObject victim, SkillContext context, int markKey, int damage, float tickSeconds)
		{
			while (true)
			{
				yield return new WaitForSeconds(tickSeconds);
				if (victim == null || victim.IsAlive == false || HitMarks.IsMarked(markKey, victim) == false)
					yield break;
				victim.ReceiveDamage(new DamageInfo
				{
					damage = damage,
					type = DamageType.Normal,
					damageSource = context.User,
					equipmentDataId = context.UsedEquipment != null ? context.UsedEquipment.ID : DamageInfo.NO_DATA_ID,
					skillDataId = DamageInfo.NO_DATA_ID,
				});
			}
		}
	}
}
