using System;

namespace WitchMendokusai
{
	// 방어력으로 줄어드는 피해. 엔진 무관
	// 방어력 100 이면 절반, 200 이면 3분의 1. 0 이하면 그대로, 피해가 있으면 최소 1
	public static class ArmorRules
	{
		// 피해가 절반이 되는 방어력
		private const double HALF_POINT = 100d;

		public static int Reduce(int damage, int armor)
		{
			if (damage <= 0 || armor <= 0)
				return damage;
			return Math.Max(1, (int)Math.Ceiling(damage * HALF_POINT / (HALF_POINT + armor)));
		}
	}
}
