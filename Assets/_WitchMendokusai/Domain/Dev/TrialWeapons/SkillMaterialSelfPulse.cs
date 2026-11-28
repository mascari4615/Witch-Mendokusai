using System;
using UnityEngine;

namespace WitchMendokusai
{
	// 스킬 재료 자기 강화 모양 (SelfForm) 의 몸 효과. 켜지는 순간 쓴 유닛을 회복하고, 켜져 있는 동안 보호막 횟수만큼 피해를 막음
	// 능력치 증가는 기존 부품 StatWhileActive 가 맡고 여기는 회복과 보호막만. 꺼지는 시점은 같은 탄의 ObjectWithTime
	public class SkillMaterialSelfPulse : SkillComponent, IDamageAbsorber
	{
		[SerializeField, Min(0)] private int healAmount = 0;
		[SerializeField, Min(0)] private int shieldCharges = 0;
		// 막은 직후 짧은 무적. 한 번에 여러 겹이 깨지지 않게
		[SerializeField, Min(0f)] private float breakInvincibleSeconds = 0.5f;

		private SkillContext context;
		private PlayerObject subscribedPlayer;
		private int chargesLeft;

		public override void InitContext(SkillObject skillObject) => context = skillObject.Context;

		private void OnEnable()
		{
			// 풀이 미리 만들 때는 맥락 없이 꺼진 채라 여기 안 옴. 맥락 없이 켜졌으면 아직 쏜 적 없는 사본
			if (context == null)
				return;
			if (healAmount > 0)
				context.User.Health.ReceiveHeal(healAmount);
			if (shieldCharges > 0)
			{
				subscribedPlayer = context.User as PlayerObject;
				if (subscribedPlayer == null)
					throw new InvalidOperationException($"{name}: 보호막은 플레이어만 받음 ({context.User.name})");
				chargesLeft = shieldCharges;
				subscribedPlayer.AddAbsorber(this);
			}
		}

		private void OnDisable()
		{
			if (subscribedPlayer == null)
				return;
			subscribedPlayer.RemoveAbsorber(this);
			subscribedPlayer = null;
		}

		public bool TryAbsorb(DamageInfo damageInfo)
		{
			if (chargesLeft <= 0)
				return false;
			chargesLeft--;
			subscribedPlayer.GrantInvincibility(breakInvincibleSeconds);
			return true;
		}
	}
}
