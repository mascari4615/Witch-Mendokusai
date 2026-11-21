using System.Collections.Generic;

namespace WitchMendokusai
{
	/// <summary> 켠 제약의 합산 결과 한 줄 (적용 수치 미리보기). 같은 스탯은 더하고, 판 규칙은 종류별 합 </summary>
	public static class DungeonConstraintSummary
	{
		public static string Describe(IReadOnlyList<DungeonConstraint> applied)
		{
			List<string> parts = new();
			// 스탯 효과: 처음 나온 묶음 이름으로 표시
			Dictionary<UnitStatType, (string Label, string Unit, int Sum)> stats = new();
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
					stats[effect.StatType] = (constraint.Name, constraint.ValueUnit, effect.Value);
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
