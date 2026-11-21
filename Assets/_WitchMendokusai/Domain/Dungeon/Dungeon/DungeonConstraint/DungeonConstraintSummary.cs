using System.Collections.Generic;

namespace WitchMendokusai
{
	/// <summary> 켠 제약의 합산 결과 한 줄 (적용 수치 미리보기). 같은 스탯은 더하고, 판 규칙은 종류별 합 </summary>
	public static class DungeonConstraintSummary
	{
		// catalog: 스탯 이름을 찾을 제약 목록 (던전 전체). 켠 것만 보면 저주만 켰을 때 이름이 없음
		public static string Describe(IReadOnlyList<DungeonConstraint> applied, IReadOnlyList<DungeonConstraint> catalog)
		{
			List<string> parts = new();
			// 스탯 효과: 그 스탯이 첫 효과인 제약의 이름과 단위로 표시 (저주처럼 여러 효과가 섞인 제약의 이름은 안 씀)
			Dictionary<UnitStatType, (string Label, string Unit, int Sum)> stats = new();
			Dictionary<UnitStatType, (string Label, string Unit)> labels = new();
			foreach (DungeonConstraint constraint in catalog)
			{
				if (labels.ContainsKey(constraint.Effects[0].StatType) == false && constraint.Effects.Count == 1 && constraint.Effects[0].Kind == DungeonConstraintEffectKind.Stat)
					labels[constraint.Effects[0].StatType] = (constraint.Name, constraint.ValueUnit);
			}
			List<UnitStatType> statOrder = new();
			foreach (DungeonConstraint constraint in applied)
			{
				foreach (DungeonConstraintEffectInfo effect in constraint.Effects)
				{
					if (effect.Kind != DungeonConstraintEffectKind.Stat)
						continue;
					if (stats.TryGetValue(effect.StatType, out (string Label, string Unit, int Sum) entry))
					{
						stats[effect.StatType] = (entry.Label, entry.Unit, entry.Sum + effect.Value);
						continue;
					}
					(string Label, string Unit) known = labels[effect.StatType]; // 이름 없는 스탯은 데이터 잘못 (단일 스탯 제약이 없음)
					stats[effect.StatType] = (known.Label, known.Unit, effect.Value);
					statOrder.Add(effect.StatType);
				}
			}
			foreach (UnitStatType statType in statOrder)
			{
				(string label, string unit, int sum) = stats[statType];
				parts.Add($"{label} {Signed(sum)}{unit}");
			}

			DungeonConstraintRunEffect run = DungeonConstraintRules.RunEffect(applied);
			AddPart(parts, "적 수", run.WavePercent, "%");
			AddPart(parts, "회복", run.HealPercent, "%");
			AddPart(parts, "선택지", run.ChoiceDelta, "장");
			AddPart(parts, "시작 체력", run.StartHealthPercent, "%");
			AddPart(parts, "난이도 간격", run.DifficultyPacePercent, "%");
			AddPart(parts, "정예", run.EliteExtraPerGap, "마리");

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

		private static void AddPart(List<string> parts, string label, int value, string unit)
		{
			if (value != 0)
				parts.Add($"{label} {Signed(value)}{unit}");
		}

		private static string Signed(int value) => value > 0 ? "+" + value : value.ToString();
	}
}
