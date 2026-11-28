using System;
using UnityEngine;

namespace WitchMendokusai
{
	// 스킬 재료 자기 강화 모양 (SelfForm) 의 몸 효과. 회복과 보호막. 능력치 증가는 기존 부품 StatWhileActive 가 맡음. 꺼지는 시점은 같은 탄의 ObjectWithTime
	// - 회복: healEverySeconds 가 0 이면 켜지는 순간 한 번. 0 보다 크면 켜져 있는 동안 그 주기마다 (지속 재생). calmSeconds 가 있으면 마지막 피격 (UnitHealth.LastHitTime) 뒤 그만큼 지나야 회복
	//   피격 시각은 몸 쪽 값을 읽음. 탄 사본은 주기마다 새로 떠서 사본이 들고 있으면 직전 피격을 모름
	// - 보호막: 켜진 뒤 shieldSeconds 동안 (0 이면 켜져 있는 내내) 보호막 횟수만큼 피해를 막음
	// - healFx: 회복할 때마다 몸에서 재생하는 연출 (없으면 안 함)
	public class SkillMaterialSelfPulse : SkillComponent, IDamageAbsorber
	{
		[SerializeField, Min(0)] private int healAmount = 0;
		[SerializeField, Min(0f)] private float healEverySeconds = 0f;
		[SerializeField, Min(0f)] private float calmSeconds = 0f;
		[SerializeField] private CombatFx healFx;
		[SerializeField, Min(0f)] private float healFxRadius = 1f;
		[SerializeField, Min(0)] private int shieldCharges = 0;
		[SerializeField, Min(0f)] private float shieldSeconds = 0f;
		// 막은 직후 짧은 무적. 한 번에 여러 겹이 깨지지 않게
		[SerializeField, Min(0f)] private float breakInvincibleSeconds = 0.5f;

		private SkillContext context;
		private PlayerObject subscribedPlayer;
		private UnitHealth regenHealth;
		private int chargesLeft;
		private float enabledTime;
		private float nextHealTime;

		public override void InitContext(SkillObject skillObject) => context = skillObject.Context;

		private void OnEnable()
		{
			// 풀이 미리 만들 때는 맥락 없이 꺼진 채라 여기 안 옴. 맥락 없이 켜졌으면 아직 쏜 적 없는 사본
			if (context == null)
				return;
			enabledTime = Time.time;
			if (healAmount > 0)
			{
				if (healEverySeconds > 0f)
				{
					nextHealTime = Time.time + healEverySeconds;
					regenHealth = context.User.Health;
				}
				else
				{
					Heal();
				}
			}
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
			regenHealth = null;
			if (subscribedPlayer == null)
				return;
			subscribedPlayer.RemoveAbsorber(this);
			subscribedPlayer = null;
		}

		private void Update()
		{
			if (regenHealth == null || Time.time < nextHealTime)
				return;
			nextHealTime = Time.time + healEverySeconds;
			if (Time.time - regenHealth.LastHitTime < calmSeconds)
				return;
			Heal();
		}

		private void Heal()
		{
			context.User.Health.ReceiveHeal(healAmount);
			if (healFx != null)
				healFx.Play(new CombatFxContext { Position = context.User.transform.position, Origin = context.User.transform.position, Direction = Vector3.forward, Follow = context.User.transform, Radius = healFxRadius, Length = healFxRadius, Width = healFxRadius });
		}

		public bool TryAbsorb(DamageInfo damageInfo)
		{
			if (chargesLeft <= 0)
				return false;
			if (shieldSeconds > 0f && Time.time - enabledTime > shieldSeconds)
				return false;
			chargesLeft--;
			subscribedPlayer.GrantInvincibility(breakInvincibleSeconds);
			return true;
		}
	}
}
