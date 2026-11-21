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

		// 아래는 제약마다 붙는 고유 규칙 (보상이나 획득 방식 변화). DungeonConstraintSideEffect
		// 회복 감소로 잃은 회복량의 % 를 판 안 냥으로
		HealLossToNyang = 7,
		// 판 난이도가 오를 때마다 경험치 구슬 (기본 구슬 몇 배)
		DifficultyExp = 8,
		// 처치 수 몇 마리마다 상자 하나
		KillChest = 9,
		// 정예 처치 때 상자 확률 %
		EliteChest = 10,
		// 레벨업 카드 희귀도 운 % (영구 행운에 더함)
		ChoiceLuck = 11,
	}

	// 켠 제약의 고유 규칙 합. 판 규칙 (DungeonConstraintRunEffect) 과 따로
	public readonly struct DungeonConstraintSideEffect
	{
		public readonly int HealLossToNyangPercent;
		public readonly int DifficultyExpOrbs;
		// 0 이면 없음
		public readonly int KillChestEvery;
		public readonly int EliteChestPercent;
		public readonly int ChoiceLuckPercent;

		public DungeonConstraintSideEffect(int healLossToNyangPercent, int difficultyExpOrbs, int killChestEvery, int eliteChestPercent, int choiceLuckPercent)
		{
			HealLossToNyangPercent = healLossToNyangPercent;
			DifficultyExpOrbs = difficultyExpOrbs;
			KillChestEvery = killChestEvery;
			EliteChestPercent = eliteChestPercent;
			ChoiceLuckPercent = choiceLuckPercent;
		}

		public static DungeonConstraintSideEffect Identity => new DungeonConstraintSideEffect(0, 0, 0, 0, 0);

		public float ChoiceLuck => ChoiceLuckPercent / DungeonConstraintRunEffect.PERCENT;

		// 잃은 회복의 냥. carry 는 남은 소수
		public int HealLossNyang(int lostHeal, ref double carry)
		{
			double nyang = lostHeal * (HealLossToNyangPercent / (double)DungeonConstraintRunEffect.PERCENT) + carry;
			int whole = (int)Math.Floor(nyang);
			carry = nyang - whole;
			return whole;
		}

		// 처치 수가 상자 기준에 닿았는지. killsSinceStart 는 판 시작부터 처치 수, chestsGiven 은 이미 준 상자 수
		public bool IsKillChestDue(int killsSinceStart, int chestsGiven) => KillChestEvery > 0 && killsSinceStart >= KillChestEvery * (chestsGiven + 1);

		// roll: 0 이상 100 미만
		public bool IsEliteChest(float roll) => roll < EliteChestPercent;
	}

	// 켠 제약의 판 규칙 효과 합. 같은 종류는 더함
	public readonly struct DungeonConstraintRunEffect
	{
		public const float PERCENT = 100f;
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

		public float WaveMultiplier => (float)Ratio(WavePercent);

		// 회복은 0 까지 줄 수 있음 (회복 -100% 제약). carry: 앞 회복에서 남은 소수. 재생 1 같은 작은 회복이 0 으로 사라지지 않고 비율대로 쌓임
		public int ScaleHeal(int heal, ref double carry)
		{
			double scaled = heal * Ratio(HealPercent) + carry;
			int whole = (int)Math.Floor(scaled + ROUNDING_TOLERANCE);
			carry = Math.Max(0d, scaled - whole);
			return whole;
		}

		// 선택지는 최소 1장
		public int ChoiceCount(int baseCount) => Math.Max(1, baseCount + ChoiceDelta);

		// 최소 1
		public int StartHealth(int maxHealth) => Math.Max(1, (int)Math.Ceiling(maxHealth * Ratio(StartHealthPercent) - ROUNDING_TOLERANCE));

		public double ScaleDifficultyStep(double stepSeconds) => stepSeconds * Ratio(DifficultyPacePercent);

		// 위협 단계 효과에 물량과 정예를 얹은 판 효과. 단계 번호와 보상 배율은 그대로
		public DungeonThreatEffect ApplyTo(DungeonThreatEffect threat) => new DungeonThreatEffect(
			threat.Level, threat.EnemyHealth, threat.EnemyDamage, threat.EliteExtraPerGap + EliteExtraPerGap,
			threat.EnemySpeed, threat.WaveRate * WaveMultiplier, threat.RewardMultiplier);

		// 정수 % 를 double 로 계산해 0.8 같은 값의 소수 오차를 피함. 음수가 되는 합은 Resolve 가 막음
		private static double Ratio(int percent) => 1d + percent / (double)PERCENT;
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
					case DungeonConstraintEffectKind.HealLossToNyang:
					case DungeonConstraintEffectKind.DifficultyExp:
					case DungeonConstraintEffectKind.KillChest:
					case DungeonConstraintEffectKind.EliteChest:
					case DungeonConstraintEffectKind.ChoiceLuck:
						break;
					default:
						throw new ArgumentOutOfRangeException(nameof(entries), entry.Kind, "처리하지 않는 제약 효과 종류");
				}
			}
			// 제약 데이터 잘못은 조용히 보정하지 않고 실패
			// 물량과 난이도 간격이 0 이면 생성 간격 나누기와 난이도 계산이 깨짐, 시작 체력은 0 이하나 늘리기 불가, 회복은 음수 불가, 정예는 빼기 불가
			if (wave <= -DungeonConstraintRunEffect.PERCENT || pace <= -DungeonConstraintRunEffect.PERCENT)
				throw new ArgumentOutOfRangeException(nameof(entries), $"물량 {wave}%, 난이도 간격 {pace}% 합은 -100 보다 커야 함");
			if (startHealth <= -DungeonConstraintRunEffect.PERCENT || startHealth > 0)
				throw new ArgumentOutOfRangeException(nameof(entries), $"시작 체력 {startHealth}% 합은 -100 초과 0 이하");
			if (heal < -DungeonConstraintRunEffect.PERCENT)
				throw new ArgumentOutOfRangeException(nameof(entries), $"회복 {heal}% 합은 -100 이상");
			if (elite < 0)
				throw new ArgumentOutOfRangeException(nameof(entries), $"정예 추가 {elite} 는 0 이상");
			return new DungeonConstraintRunEffect(wave, heal, choice, startHealth, pace, elite);
		}

		// 고유 규칙 합. 상자 간격은 여럿이면 가장 짧은 것
		public static DungeonConstraintSideEffect ResolveSide(IEnumerable<Entry> entries)
		{
			int healLoss = 0;
			int expOrbs = 0;
			int killChest = 0;
			int eliteChest = 0;
			int luck = 0;
			foreach (Entry entry in entries)
			{
				switch (entry.Kind)
				{
					case DungeonConstraintEffectKind.HealLossToNyang:
						healLoss += entry.Value;
						break;
					case DungeonConstraintEffectKind.DifficultyExp:
						expOrbs += entry.Value;
						break;
					case DungeonConstraintEffectKind.KillChest:
						if (entry.Value <= 0)
							throw new ArgumentOutOfRangeException(nameof(entries), entry.Value, "처치 상자 간격은 1 이상");
						killChest = killChest == 0 ? entry.Value : Math.Min(killChest, entry.Value);
						break;
					case DungeonConstraintEffectKind.EliteChest:
						eliteChest += entry.Value;
						break;
					case DungeonConstraintEffectKind.ChoiceLuck:
						luck += entry.Value;
						break;
				}
			}
			if (healLoss < 0 || expOrbs < 0 || eliteChest < 0 || luck < 0)
				throw new ArgumentOutOfRangeException(nameof(entries), "고유 규칙 값은 0 이상");
			return new DungeonConstraintSideEffect(healLoss, expOrbs, killChest, eliteChest, luck);
		}
	}
}
