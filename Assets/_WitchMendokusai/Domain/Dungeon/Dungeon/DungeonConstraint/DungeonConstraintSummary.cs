using System.Collections.Generic;

namespace WitchMendokusai
{
	/// <summary> 켠 제약의 합산 결과 한 줄 (적용 수치 미리보기). 같은 스탯은 더하고, 판 규칙은 종류별 합 </summary>
	public static class DungeonConstraintSummary
	{
		public static string Describe(IReadOnlyList<DungeonConstraint> applied)
		{
			List<string> parts = new();
			// 스탯 효과: 같은 스탯은 더함. 글은 DungeonConstraintEffectText (제약 이름과 별개)
			Dictionary<UnitStatType, int> stats = new();
			List<UnitStatType> statOrder = new();
			foreach (DungeonConstraint constraint in applied)
			{
				foreach (DungeonConstraintEffectInfo effect in constraint.Effects)
				{
					if (effect.Kind != DungeonConstraintEffectKind.Stat)
						continue;
					if (stats.TryGetValue(effect.StatType, out int sum) == false)
						statOrder.Add(effect.StatType);
					stats[effect.StatType] = sum + effect.Value;
				}
			}
			foreach (UnitStatType statType in statOrder)
				parts.Add(DungeonConstraintEffectText.Part(DungeonConstraintEffectKind.Stat, statType, stats[statType]));

			DungeonConstraintRunEffect run = DungeonConstraintRules.RunEffect(applied);
			AddPart(parts, DungeonConstraintEffectKind.WaveRate, run.WavePercent);
			AddPart(parts, DungeonConstraintEffectKind.HealRate, run.HealPercent);
			AddPart(parts, DungeonConstraintEffectKind.ChoiceCount, run.ChoiceDelta);
			AddPart(parts, DungeonConstraintEffectKind.StartHealth, run.StartHealthPercent);
			AddPart(parts, DungeonConstraintEffectKind.DifficultyPace, run.DifficultyPacePercent);
			AddPart(parts, DungeonConstraintEffectKind.EliteExtra, run.EliteExtraPerGap);

			// 고유 규칙
			DungeonConstraintSideEffect side = DungeonConstraintRules.SideEffect(applied);
			if (side.KillChestEvery > 0)
				parts.Add($"{side.KillChestEvery}마리마다 상자");
			if (side.HealLossToNyangPercent > 0)
				parts.Add($"잃은 회복 {side.HealLossToNyangPercent}% 냥");
			if (side.EliteChestPercent > 0)
				parts.Add($"정예 상자 {side.EliteChestPercent}%");
			if (side.ChoiceLuckPercent > 0)
				parts.Add($"희귀 운 +{side.ChoiceLuckPercent}%");
			if (side.DifficultyExpOrbs > 0)
				parts.Add($"난이도마다 경험치 x{side.DifficultyExpOrbs}");
			return string.Join(", ", parts);
		}

		private static void AddPart(List<string> parts, DungeonConstraintEffectKind kind, int value)
		{
			if (value != 0)
				parts.Add(DungeonConstraintEffectText.Part(kind, UnitStatType.NONE, value));
		}
	}
}
