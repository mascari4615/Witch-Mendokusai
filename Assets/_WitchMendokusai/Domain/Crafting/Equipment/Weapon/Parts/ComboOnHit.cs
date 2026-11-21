using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 연속 적중 누적과 처치 시 추가 공격
	// - 이어진 적중마다 피해 증가. 타격 하나가 아무도 못 맞히고 끝나면 0으로
	// - 맞힌 대상이 죽으면 근처 다음 몬스터를 한 번 더 공격. 그 공격이 또 죽이면 이어지고 followUpLimit 이 끊음
	public class ComboOnHit : SkillComponent, IDamageModifier, IHitEffect
	{
		[SerializeField, Min(0f), Tooltip("이어진 적중 하나당 피해 증가 퍼센트")] private float bonusPercentPerHit = 10f;
		[SerializeField, Min(1), Tooltip("피해가 늘어나는 적중 수 상한")] private int maxCount = 10;
		[SerializeField, Min(0), Tooltip("처치 시 추가 공격 연쇄 상한. 0이면 추가 공격 없음")] private int followUpLimit = 3;
		[SerializeField, Min(0f), Tooltip("추가 공격 대상을 찾는 거리, 월드 단위")] private float followUpRange = 6f;

		private UnitObject user;
		private int weaponId;
		private bool armed;
		private int swingHits;
		private readonly List<GameObject> visited = new();

		public override void InitContext(SkillObject skillObject)
		{
			user = skillObject.Context != null ? skillObject.Context.User : null;
			weaponId = skillObject.Context != null && skillObject.Context.UsedEquipment != null ? skillObject.Context.UsedEquipment.ID : (int)EntityId.ToULong(GetEntityId());
			armed = user != null;
			swingHits = 0;
		}

		public DamageInfo Modify(DamagingObject source, UnitObject victim, DamageInfo damageInfo)
		{
			if (armed == false)
				return damageInfo;
			float multiplier = HitComboRules.DamageMultiplier(HitComboBook.Of(user).Count(weaponId), bonusPercentPerHit, maxCount);
			damageInfo.damage = HitComboRules.Apply(damageInfo.damage, multiplier);
			return damageInfo;
		}

		public void OnHit(DamagingObject source, UnitObject victim)
		{
			if (armed == false)
				return;
			swingHits++;
			HitComboBook.Of(user).Hit(weaponId, maxCount);
			FollowUp(source, victim);
		}

		private void FollowUp(DamagingObject source, UnitObject victim)
		{
			visited.Clear();
			visited.Add(victim.gameObject);
			UnitObject current = victim;
			int done = 0;
			while (current.IsAlive == false && HitComboRules.CanFollowUp(done, followUpLimit))
			{
				Transform next = SurvivalTargeting.Nearest(current.transform.position, followUpRange, visited);
				if (next == null || next.TryGetComponent(out UnitObject nextUnit) == false)
					break;
				visited.Add(next.gameObject);
				source.DealDirectDamage(nextUnit);
				current = nextUnit;
				done++;
			}
			visited.Clear();
		}

		// 타격이 끝났는데 아무도 못 맞혔으면 빗나감
		private void OnDisable()
		{
			if (armed && swingHits == 0 && user != null && user.isActiveAndEnabled)
				HitComboBook.Of(user).Miss(weaponId);
			armed = false;
		}
	}
}
