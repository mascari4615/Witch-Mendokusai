using System;
using System.Collections.Generic;

namespace WitchMendokusai
{
	// 레벨업 필요 경험치 표의 한 구간. 값은 SurvivalRunRules 자산에 있음
	[Serializable]
	public class ExpCurveBand
	{
		public int FromLevel = 1;
		public int Increment = 30;
		public int BarrierExtra = 0;
	}

	// 구간 곡선의 정본. level은 현재 레벨, Required는 그 레벨에서 다음 레벨까지 필요한 경험치
	public static class ExpCurveRules
	{
		public static int Required(IReadOnlyList<ExpCurveBand> bands, int baseRequired, int level)
		{
			int requirement = baseRequired;
			if (bands == null)
				return requirement;

			for (int curLevel = 1; curLevel <= level; curLevel++)
				requirement += BandAt(bands, curLevel)?.Increment ?? 0;

			ExpCurveBand current = BandAt(bands, level);
			return current != null && current.FromLevel == level ? requirement + Math.Max(0, current.BarrierExtra) : requirement;
		}

		public static bool IsBarrierLevel(IReadOnlyList<ExpCurveBand> bands, int level)
		{
			ExpCurveBand current = bands == null ? null : BandAt(bands, level);
			return current != null && current.FromLevel == level && current.BarrierExtra > 0;
		}

		// 장벽 레벨에서만 획득 경험치 배율 적용
		public static float GainMultiplier(IReadOnlyList<ExpCurveBand> bands, int level, float barrierMultiplier)
			=> IsBarrierLevel(bands, level) && barrierMultiplier > 0f ? barrierMultiplier : 1f;

		// 보너스 적용 뒤 원시 경험치를 레벨 경계별로 소비, 장벽 이후 초과분의 배율 누출 방지
		// 소수점은 호출부의 기존 확률 반올림까지 유지
		public static double CalculateGain(IReadOnlyList<ExpCurveBand> bands, int baseRequired, float barrierMultiplier, int level, int currentExp, double baseGain)
		{
			double remaining = baseGain;
			double gain = 0d;
			while (remaining > 0d)
			{
				int required = Required(bands, baseRequired, level);
				if (required <= 0)
					return gain + remaining;

				if (currentExp >= required)
				{
					currentExp -= required;
					level++;
					continue;
				}

				double multiplier = GainMultiplier(bands, level, barrierMultiplier);
				double toBoundary = (required - currentExp) / multiplier;
				if (remaining < toBoundary)
					return gain + remaining * multiplier;

				gain += required - currentExp;
				remaining -= toBoundary;
				currentExp = 0;
				level++;
			}
			return gain;
		}

		public static int LevelAfter(IReadOnlyList<ExpCurveBand> bands, int baseRequired, float barrierMultiplier, int collectedExp)
		{
			int level = 0;
			double remaining = collectedExp;
			while (true)
			{
				double cost = Required(bands, baseRequired, level) / (double)GainMultiplier(bands, level, barrierMultiplier);
				if (cost <= 0d || remaining < cost)
					return level;
				remaining -= cost;
				level++;
			}
		}

		// 구간은 FromLevel 오름차순
		private static ExpCurveBand BandAt(IReadOnlyList<ExpCurveBand> bands, int level)
		{
			ExpCurveBand found = null;
			foreach (ExpCurveBand band in bands)
				if (band.FromLevel <= level)
					found = band;
			return found;
		}
	}
}
