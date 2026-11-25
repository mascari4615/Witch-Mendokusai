using System;

namespace WitchMendokusai
{
	// 알리사 일제 사격 게이지의 정본. 무기 탄이 적을 맞힐수록 차고, 다 차면 무기를 잔뜩 꺼내 바라보는 쪽에 포화
	// 정예와 보스는 더 채워 보스 릴레이에서도 돌게 함 (기획 '숙련 조건 원칙'). 밀도 높은 던전에서 상시 포화가 되지 않게 최소 간격
	public static class VolleyGaugeRules
	{
		// 탄 하나가 처음 맞힌 적 하나에 채우는 양. chargePercent 는 충전 카드 합 (장전 +25, 반복 +5)
		public static float Charge(bool isStrongFoe, float normalCharge, float strongCharge, int chargePercent)
		{
			float baseCharge = isStrongFoe ? strongCharge : normalCharge;
			return Math.Max(0f, baseCharge) * (1f + Math.Max(0, chargePercent) / 100f);
		}

		// 가득 찬 뒤로는 더 쌓지 않음
		public static float Add(float gauge, float amount, float size) => Math.Min(size, Math.Max(0f, gauge) + Math.Max(0f, amount));

		public static bool CanFire(float gauge, float size, float secondsSinceLastFire, float minIntervalSeconds) => gauge >= size && secondsSinceLastFire >= minIntervalSeconds;

		// 포화 한 번에 나가는 탄 수. 무기 수 x 무기당 발
		public static int ShotCount(int baseWeapons, int bonusWeapons, int shotsPerWeapon) => Math.Max(0, baseWeapons + bonusWeapons) * Math.Max(1, shotsPerWeapon);

		// 탄 하나의 피해 배율 퍼센트. 판 안 누적 (무기고 마무리), 나눠 쏘기 (연속 포화 60), 집중 포화 (140) 를 곱함
		public static int DamagePercent(int growthPercent, bool split, int splitPercent, bool focus, int focusPercent)
		{
			float percent = 100f + Math.Max(0, growthPercent);
			if (split)
				percent = percent * splitPercent / 100f;
			if (focus)
				percent = percent * focusPercent / 100f;
			return (int)Math.Round(percent);
		}

		// 무기 count 자루를 한 번씩만 쓰며 돌 때의 걸음. 옆 자루가 잇달아 쏘지 않게 1 보다 크고 count 와 서로소 (없으면 1)
		public static int SpreadStride(int count)
		{
			for (int stride = Math.Max(2, count / 3); stride < count; stride++)
			{
				if (GreatestCommonDivisor(stride, count) == 1)
					return stride;
			}
			return 1;
		}

		private static int GreatestCommonDivisor(int left, int right) => right == 0 ? left : GreatestCommonDivisor(right, left % right);

		// 탄 i 의 부채꼴 안 각도 (도). 고르게 나누고 가운데가 0
		public static float FanAngle(int index, int count, float fanDegrees)
		{
			if (count <= 1)
				return 0f;
			return -fanDegrees / 2f + fanDegrees * index / (count - 1);
		}
	}
}
