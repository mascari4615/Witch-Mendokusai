using System;
using System.Collections.Generic;

namespace WitchMendokusai
{
	// 제약 효과 하나의 종류. Stat 은 몬스터 스탯 가산 (UnitStatCalculator), 나머지는 판 규칙 (값은 % 또는 개수)
	public enum DungeonConstraintEffectKind
	{
		Stat = 0,
		// 웨이브 물량 %. 위협 단계 물량 배율에 곱함
		WaveRate = 1,
		// 플레이어가 받는 회복량 %
		HealRate = 2,
		// 레벨업 카드 선택지 수 가감 (개수)
		ChoiceCount = 3,
		// 시작 체력 % (최대 체력 기준)
		StartHealth = 4,
		// 난이도 오름 간격 %. 음수면 판 난이도가 빨리 오름
		DifficultyPace = 5,
		// 정예 사이 추가 정예 수 (개수). 위협 단계 정예 추가에 더함
		EliteExtra = 6,
	}

	// 켠 제약의 판 규칙 효과 합. 같은 종류는 더함
	public readonly struct DungeonConstraintRunEffect
	{
		private const float PERCENT = 100f;
		// 배율 하한. % 합이 -100 이하여도 0 이 되지 않게
		private const float MIN_MULTIPLIER = 0.1f;
		private const double ROUNDING_TOLERANCE = 0.0001d;

		public readonly int WavePercent;
		public readonly int HealPercent;
		public readonly int ChoiceDelta;
		public readonly int StartHealthPercent;
		public readonly int DifficultyPacePercent;
		public readonly int EliteExtraPerGap;

		public DungeonConstraintRunEffect(int wavePercent, int healPercent, int choiceDelta, int startHealthPercent, int difficultyPacePercent, int eliteExtraPerGap)
		{
			WavePercent = wavePercent;
			HealPercent = healPercent;
			ChoiceDelta = choiceDelta;
			StartHealthPercent = startHealthPercent;
			DifficultyPacePercent = difficultyPacePercent;
			EliteExtraPerGap = eliteExtraPerGap;
		}

		public static DungeonConstraintRunEffect Identity => new DungeonConstraintRunEffect(0, 0, 0, 0, 0, 0);

		public float WaveMultiplier => Multiplier(WavePercent);

		// 회복은 0 까지 줄 수 있음 (회복 -100% 제약)
		public int ScaleHeal(int heal) => Math.Max(0, (int)Math.Floor(heal * Ratio(HealPercent) + ROUNDING_TOLERANCE));

		// 선택지는 최소 1장
		public int ChoiceCount(int baseCount) => Math.Max(1, baseCount + ChoiceDelta);

		// 최소 1
		public int StartHealth(int maxHealth) => Math.Max(1, (int)Math.Ceiling(maxHealth * Ratio(StartHealthPercent) - ROUNDING_TOLERANCE));

		public double ScaleDifficultyStep(double stepSeconds) => stepSeconds * Multiplier(DifficultyPacePercent);

		// 위협 단계 효과에 물량과 정예를 얹은 판 효과. 단계 번호와 보상 배율은 그대로
		public DungeonThreatEffect ApplyTo(DungeonThreatEffect threat) => new DungeonThreatEffect(
			threat.Level, threat.EnemyHealth, threat.EnemyDamage, threat.EliteExtraPerGap + Math.Max(0, EliteExtraPerGap),
			threat.EnemySpeed, threat.WaveRate * WaveMultiplier, threat.RewardMultiplier);

		private static float Multiplier(int percent) => Math.Max(MIN_MULTIPLIER, 1f + percent / PERCENT);

		// 0 이상 비율. 정수 % 를 double 로 계산해 0.8 같은 값의 소수 오차를 피함
		private static double Ratio(int percent) => Math.Max(0d, 1d + percent / (double)PERCENT);
	}

	public static class DungeonConstraintRunRules
	{
		public readonly struct Entry
		{
			public readonly DungeonConstraintEffectKind Kind;
			public readonly int Value;

			public Entry(DungeonConstraintEffectKind kind, int value)
			{
				Kind = kind;
				Value = value;
			}
		}

		public static DungeonConstraintRunEffect Resolve(IEnumerable<Entry> entries)
		{
			int wave = 0;
			int heal = 0;
			int choice = 0;
			int startHealth = 0;
			int pace = 0;
			int elite = 0;
			foreach (Entry entry in entries)
			{
				switch (entry.Kind)
				{
					case DungeonConstraintEffectKind.Stat:
						break;
					case DungeonConstraintEffectKind.WaveRate:
						wave += entry.Value;
						break;
					case DungeonConstraintEffectKind.HealRate:
						heal += entry.Value;
						break;
					case DungeonConstraintEffectKind.ChoiceCount:
						choice += entry.Value;
						break;
					case DungeonConstraintEffectKind.StartHealth:
						startHealth += entry.Value;
						break;
					case DungeonConstraintEffectKind.DifficultyPace:
						pace += entry.Value;
						break;
					case DungeonConstraintEffectKind.EliteExtra:
						elite += entry.Value;
						break;
					default:
						throw new ArgumentOutOfRangeException(nameof(entries), entry.Kind, "처리하지 않는 제약 효과 종류");
				}
			}
			return new DungeonConstraintRunEffect(wave, heal, choice, startHealth, pace, elite);
		}
	}
}
