using System;
using System.Collections.Generic;

namespace WitchMendokusai
{
	/// <summary>
	/// 인형 고유 패시브 셈. 피격 누적 강화와 레벨 구간 보너스.
	/// </summary>
	public static class DollPassiveRules
	{
		// 피격 수에 비례한 퍼센트 보너스. maxStacks 0 이하면 무제한
		public static int HitStackBonus(int hitCount, int percentPerHit, int maxStacks)
		{
			int stacks = Math.Max(hitCount, 0);
			if (maxStacks > 0)
				stacks = Math.Min(stacks, maxStacks);
			return stacks * percentPerHit;
		}

		// 레벨이 도달한 구간 수. 구간 값은 오름차순이 아니어도 됨
		public static int TiersReached(int level, IReadOnlyList<int> thresholds)
		{
			if (thresholds == null)
				return 0;
			int reached = 0;
			for (int i = 0; i < thresholds.Count; i++)
			{
				if (level >= thresholds[i])
					reached++;
			}
			return reached;
		}
	}

	/// <summary>
	/// 한 번 막는 방패. 막으면 한 겹 깨지고 재충전 쿨이 돎. 겹이 다 차 있으면 쿨 정지.
	/// </summary>
	public sealed class ShieldState
	{
		private int maxCharges;
		private float cooldown;
		private float remain;

		public ShieldState(int maxCharges, float cooldown)
		{
			this.maxCharges = Math.Max(maxCharges, 1);
			this.cooldown = Math.Max(cooldown, 0.01f);
			Charges = this.maxCharges;
		}

		public int Charges { get; private set; }
		public int MaxCharges => maxCharges;
		public bool HasShield => Charges > 0;
		public float Remain => remain;
		public float Cooldown => cooldown;

		// 다음 겹이 차기까지 진행 비율. 가득이면 1
		public float RechargeRatio => Charges >= maxCharges ? 1f : 1f - Math.Min(Math.Max(remain / cooldown, 0f), 1f);

		public void SetCooldown(float newCooldown)
		{
			float next = Math.Max(newCooldown, 0.01f);
			// 진행도 유지, 남은 시간은 새 쿨에 맞춰 비례 조정
			remain = cooldown > 0f ? remain * next / cooldown : remain;
			cooldown = next;
		}

		public void SetMax(int newMax)
		{
			maxCharges = Math.Max(newMax, 1);
			if (Charges > maxCharges)
				Charges = maxCharges;
			if (Charges < maxCharges && remain <= 0f)
				remain = cooldown;
		}

		public void Restore()
		{
			Charges = maxCharges;
			remain = 0f;
		}

		// 막을 수 있으면 한 겹 깨고 true. 가득 찬 상태에서 깨지면 그 순간 쿨 시작
		public bool TryAbsorb()
		{
			if (Charges <= 0)
				return false;
			bool wasFull = Charges >= maxCharges;
			Charges--;
			if (wasFull)
				remain = cooldown;
			return true;
		}

		public void Tick(float deltaTime) => Advance(deltaTime);

		// 피격 때 쿨 진행. 가득 찼으면 효과 없음
		public void Advance(float seconds)
		{
			if (seconds <= 0f || Charges >= maxCharges)
				return;
			remain -= seconds;
			while (remain <= 0f && Charges < maxCharges)
			{
				Charges++;
				remain = Charges < maxCharges ? remain + cooldown : 0f;
			}
		}
	}

	/// <summary>
	/// 시간제 소환수의 남은 시간.
	/// </summary>
	public sealed class TimedSummonState
	{
		private readonly float duration;

		public TimedSummonState(float duration)
		{
			this.duration = Math.Max(duration, 0.01f);
			Remain = this.duration;
		}

		public float Remain { get; private set; }
		public float Duration => duration;
		public float Ratio => Math.Min(Math.Max(Remain / duration, 0f), 1f);
		public bool IsExpired => Remain <= 0f;

		public void Tick(float deltaTime) => Remain = Math.Max(Remain - Math.Max(deltaTime, 0f), 0f);
	}

	/// <summary>
	/// 소환 쿨. 소환수가 상한이면 쿨을 멈춤.
	/// </summary>
	public static class SummonRules
	{
		public static bool CanSummon(int activeCount, int maxActive, float cooldownRemain)
			=> activeCount < maxActive && cooldownRemain <= 0f;
	}

	/// <summary>
	/// 레벨업 순간 발동 무기의 쿨. 쿨마다 발동하고, 레벨업이면 쿨과 무관하게 즉시 발동하고 쿨을 다시 시작.
	/// </summary>
	public sealed class LevelUpPulseState
	{
		private readonly float cooldown;
		private float remain;

		public LevelUpPulseState(float cooldown)
		{
			this.cooldown = Math.Max(cooldown, 0.01f);
			remain = this.cooldown;
		}

		public float Remain => remain;
		public float Ratio => 1f - Math.Min(Math.Max(remain / cooldown, 0f), 1f);

		// 쿨이 다 돌았으면 true 와 함께 쿨 재시작
		public bool Tick(float deltaTime)
		{
			remain -= Math.Max(deltaTime, 0f);
			if (remain > 0f)
				return false;
			remain = cooldown;
			return true;
		}

		// 레벨업이면 항상 발동, 쿨 재시작
		public bool OnLevelUp()
		{
			remain = cooldown;
			return true;
		}
	}

	public enum CompanionPhase
	{
		Follow = 0,
		Approach = 1,
		Dash = 2,
	}

	/// <summary>
	/// 상시 동료의 돌진 순서. 따라다니다 쿨이 차면 적 옆까지 달려가 돌진, 횟수만큼 반복하고 복귀.
	/// 이동은 호출자가 하고 이 클래스는 단계와 시간만 결정.
	/// </summary>
	public sealed class CompanionRushState
	{
		private readonly float cooldown;
		private readonly float dashDuration;
		private readonly float pullDistance;
		private float cooldownRemain;
		private float dashRemain;
		private int rushesLeft;

		public CompanionRushState(float cooldown, float dashDuration, float pullDistance)
		{
			this.cooldown = Math.Max(cooldown, 0.01f);
			this.dashDuration = Math.Max(dashDuration, 0.01f);
			this.pullDistance = Math.Max(pullDistance, 0.01f);
			cooldownRemain = this.cooldown;
		}

		public CompanionPhase Phase { get; private set; } = CompanionPhase.Follow;
		public int RushesLeft => rushesLeft;
		public float CooldownRemain => cooldownRemain;
		public float DashRemain => dashRemain;
		public bool IsReady => Phase == CompanionPhase.Follow && cooldownRemain <= 0f;

		public void SetCooldownRemain(float seconds) => cooldownRemain = Math.Max(seconds, 0f);

		// 따라다니는 중에만 쿨이 돎
		public void TickFollow(float deltaTime)
		{
			if (Phase == CompanionPhase.Follow && cooldownRemain > 0f)
				cooldownRemain -= Math.Max(deltaTime, 0f);
		}

		// 쿨이 찼을 때 시작. 횟수 0 이하는 시작 안 함
		public bool BeginRush(int count)
		{
			if (IsReady == false || count <= 0)
				return false;
			rushesLeft = count;
			Phase = CompanionPhase.Approach;
			rushesLeft--;
			return true;
		}

		// 적 옆에 닿음. 돌진 시작
		public void ArriveAtTarget()
		{
			if (Phase != CompanionPhase.Approach)
				return;
			Phase = CompanionPhase.Dash;
			dashRemain = dashDuration;
		}

		// 돌진 시간 진행. 끝나면 true
		public bool TickDash(float deltaTime)
		{
			if (Phase != CompanionPhase.Dash)
				return false;
			dashRemain -= Math.Max(deltaTime, 0f);
			return dashRemain <= 0f;
		}

		// 돌진이 끝났거나 목표가 사라짐. 남은 횟수가 있으면 다음 접근, 없으면 복귀하고 쿨 시작
		public bool NextRushOrFinish()
		{
			if (rushesLeft > 0)
			{
				rushesLeft--;
				Phase = CompanionPhase.Approach;
				return true;
			}
			Finish();
			return false;
		}

		// 끌려오거나 목표가 없을 때 중단. 쿨 다시 시작
		public void Abort() => Finish();

		public bool ShouldPull(float distanceFromOwner) => Phase != CompanionPhase.Follow && distanceFromOwner >= pullDistance;

		private void Finish()
		{
			Phase = CompanionPhase.Follow;
			rushesLeft = 0;
			dashRemain = 0f;
			cooldownRemain = cooldown;
		}
	}
}
