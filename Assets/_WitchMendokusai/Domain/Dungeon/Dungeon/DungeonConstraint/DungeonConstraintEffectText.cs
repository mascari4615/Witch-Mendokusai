using System.Collections.Generic;

namespace WitchMendokusai
{
	/// <summary>
	/// 제약 효과의 화면 글 단일 출처. 제약 이름 (Name, 예: 단단함) 과 별개로 무엇이 얼마나 바뀌는지 (적 체력 +10)
	/// 난이도를 올리는 효과만 다룸. 보상 쪽 고유 규칙은 DungeonConstraintSummary 와 설명 줄
	/// 말은 입장 창 위협 단계 글 (적 체력, 적 피해, 적 속도, 정예 추가) 과 맞춤
	/// </summary>
	public static class DungeonConstraintEffectText
	{
		/// <summary> 제약 하나의 효과 전체. 효과가 여럿이면 쉼표로 (켠 제약 목록) </summary>
		public static string Of(DungeonConstraint constraint)
		{
			List<string> parts = new();
			foreach (DungeonConstraintEffectInfo effect in constraint.Effects)
			{
				if (IsPenalty(effect.Kind))
					parts.Add(Part(effect.Kind, effect.StatType, effect.Value));
			}
			return string.Join(", ", parts);
		}

		/// <summary> 좁은 자리 (입장 창 칩, 설정 창 칸). 앞의 '적' 을 빼고 첫 효과 하나, 나머지는 개수 ("적 수 +10% 외 2") </summary>
		public static string Compact(DungeonConstraint constraint)
		{
			string first = null;
			int more = 0;
			foreach (DungeonConstraintEffectInfo effect in constraint.Effects)
			{
				if (IsPenalty(effect.Kind) == false)
					continue;
				if (first == null)
					first = $"{Target(effect.Kind, effect.StatType, false)} {Signed(effect.Value)}{Unit(effect.Kind, effect.StatType)}";
				else
					more++;
			}
			if (first == null)
				return string.Empty;
			return more > 0 ? $"{first} 외 {more}" : first;
		}

		public static string Part(DungeonConstraintEffectKind kind, UnitStatType statType, int value) => $"{Target(kind, statType, true)} {Signed(value)}{Unit(kind, statType)}";

		public static bool IsPenalty(DungeonConstraintEffectKind kind) => kind switch
		{
			DungeonConstraintEffectKind.Stat or DungeonConstraintEffectKind.WaveRate or DungeonConstraintEffectKind.HealRate
				or DungeonConstraintEffectKind.ChoiceCount or DungeonConstraintEffectKind.StartHealth
				or DungeonConstraintEffectKind.DifficultyPace or DungeonConstraintEffectKind.EliteExtra => true,
			_ => false,
		};

		// full: 켠 목록과 합산 줄의 긴 말. 좁은 자리는 짧은 말 (몬스터 스탯 앞 '적' 도 뺌, 제약 판은 전부 몬스터 대상)
		private static string Target(DungeonConstraintEffectKind kind, UnitStatType statType, bool full) => kind switch
		{
			DungeonConstraintEffectKind.Stat => (full ? "적 " : string.Empty) + StatTarget(statType),
			DungeonConstraintEffectKind.WaveRate => "적 수",
			DungeonConstraintEffectKind.HealRate => full ? "받는 회복" : "회복",
			DungeonConstraintEffectKind.ChoiceCount => full ? "카드 선택지" : "선택지",
			DungeonConstraintEffectKind.StartHealth => "시작 체력",
			DungeonConstraintEffectKind.DifficultyPace => full ? "난이도 오름 간격" : "난이도 간격",
			DungeonConstraintEffectKind.EliteExtra => full ? "정예 사이 정예" : "정예",
			_ => throw new System.ArgumentOutOfRangeException(nameof(kind), kind, "효과 글이 없는 종류"),
		};

		// 제약이 쓰는 몬스터 스탯만. 새 스탯 제약을 만들면 여기에 글을 더함
		private static string StatTarget(UnitStatType statType) => statType switch
		{
			UnitStatType.HP_MAX => "체력",
			UnitStatType.MOVEMENT_SPEED_BONUS => "속도",
			UnitStatType.DAMAGE_BONUS => "피해",
			_ => throw new System.ArgumentOutOfRangeException(nameof(statType), statType, "제약 효과 글이 없는 스탯"),
		};

		private static string Unit(DungeonConstraintEffectKind kind, UnitStatType statType) => kind switch
		{
			DungeonConstraintEffectKind.Stat => statType == UnitStatType.DAMAGE_BONUS ? "%" : string.Empty,
			DungeonConstraintEffectKind.ChoiceCount => "장",
			DungeonConstraintEffectKind.EliteExtra => "마리",
			_ => "%",
		};

		public static string Signed(int value) => value > 0 ? "+" + value : value.ToString();
	}
}
