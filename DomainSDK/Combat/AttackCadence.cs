using System;

namespace WitchMendokusai
{
	/// <summary>
	/// 공격 주기 계산. 강화 수에 비례해 감소, 하한 비율 유지.
	/// </summary>
	public static class AttackCadence
	{
		public static float Interval(float baseInterval, int stacks, float reductionPerStack, float minRatio)
		{
			float ratio = 1f - Math.Max(stacks, 0) * reductionPerStack;
			return baseInterval * Math.Max(ratio, minRatio);
		}

		// 무기별 공격속도 스택에 전역 쿨타임 감소 퍼센트를 곱함, 하한 비율 유지
		public static float Interval(float baseInterval, int stacks, float reductionPerStack, float minRatio, int cooldownPercent)
		{
			float stackRatio = 1f - Math.Max(stacks, 0) * reductionPerStack;
			float cooldownRatio = 1f - Math.Max(cooldownPercent, 0) / 100f;
			return baseInterval * Math.Max(stackRatio * cooldownRatio, minRatio);
		}

		public static float ChargeRatio(float stillSeconds, float chargeSeconds)
		{
			if (chargeSeconds <= 0f)
				return 1f;
			return Math.Min(Math.Max(stillSeconds, 0f) / chargeSeconds, 1f);
		}
	}
}
