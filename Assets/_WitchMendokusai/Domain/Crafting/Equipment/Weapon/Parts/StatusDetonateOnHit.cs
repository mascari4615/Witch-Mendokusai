using UnityEngine;

namespace WitchMendokusai
{
	// 맞은 유닛에 걸린 상태를 소모해 이 타격 피해를 키움 (상태 폭발)
	// - 다른 무기가 StatusOnHit 으로 건 상태를 이 무기가 터뜨리는 엇갈림
	// - 터뜨릴 수 있는 상태는 이 부품이 정하고, 우선순위와 배율은 UnitStatusConfig
	// - 한 타격에 상태 하나만 소모
	public class StatusDetonateOnHit : SkillComponent, IDamageModifier
	{
		[SerializeField, Tooltip("이 무기가 터뜨릴 수 있는 상태. 걸려 있는 것 중 우선순위가 높은 하나만 소모")] private UnitStatusType[] detonates = { UnitStatusType.Burn };

		public override void InitContext(SkillObject skillObject)
		{
		}

		public DamageInfo Modify(DamagingObject source, UnitObject victim, DamageInfo damageInfo)
		{
			UnitStatusConfig config = ResourceManager.Instance.UnitStatus;
			if (config == null)
				return damageInfo;
			StatusDetonation detonation = StatusDetonationRules.Resolve(
				damageInfo.damage,
				damageInfo.type == DamageType.Critical,
				config.DetonationPriority,
				UnitStatus.ActiveMask(victim),
				StatusDetonationRules.Mask(detonates),
				config.DetonationMultiplier,
				config.DetonationCriticalMultiplier);
			if (detonation.Triggered == false)
				return damageInfo;
			UnitStatus.Consume(victim, detonation.Status);
			damageInfo.damage = detonation.Damage;
			damageInfo.statusDetonated = true;
			damageInfo.detonatedStatus = detonation.Status;
			return damageInfo;
		}
	}
}
