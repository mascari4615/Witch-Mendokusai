using System.Collections.Generic;

namespace WitchMendokusai
{
	/// <summary>
	/// 제약 규칙 한 곳. 위험도 합, 같은 묶음 배타, 열 순서
	/// 켬 상태는 넘겨받은 사전 (던전 저장값이나 설정 창의 고치는 중 사본)
	/// </summary>
	public static class DungeonConstraintRules
	{
		public static int TotalRisk(Dungeon dungeon, IReadOnlyDictionary<int, bool> selected)
		{
			int total = 0;
			foreach (DungeonConstraint constraint in dungeon.Constraints)
			{
				if (selected[constraint.ID])
					total += constraint.Level;
			}
			return total;
		}

		public static int TotalRisk(Dungeon dungeon) => TotalRisk(dungeon, dungeon.ConstraintSelected);

		/// <summary> 묶음마다 가장 높은 단계의 합. 위험도 눈금 칸 수 </summary>
		public static int MaxRisk(Dungeon dungeon)
		{
			Dictionary<string, int> highest = new();
			foreach (DungeonConstraint constraint in dungeon.Constraints)
			{
				highest.TryGetValue(constraint.FamilyKey, out int level);
				if (constraint.Level > level)
					highest[constraint.FamilyKey] = constraint.Level;
			}
			int total = 0;
			foreach (int level in highest.Values)
				total += level;
			return total;
		}

		/// <summary> 켜면 같은 묶음의 다른 단계는 끔 </summary>
		public static void Set(Dungeon dungeon, Dictionary<int, bool> selected, DungeonConstraint target, bool isOn)
		{
			if (isOn)
			{
				foreach (DungeonConstraint constraint in dungeon.Constraints)
				{
					if (constraint != target && constraint.FamilyKey == target.FamilyKey)
						selected[constraint.ID] = false;
				}
			}
			selected[target.ID] = isOn;
		}

		/// <summary> 같은 묶음에 둘 이상 켜져 있으면 가장 높은 단계만 남김. 세이브 불러오기 뒤 (묶음을 나중에 정한 옛 세이브) </summary>
		public static void Normalize(Dungeon dungeon, Dictionary<int, bool> selected)
		{
			Dictionary<string, DungeonConstraint> highest = new();
			foreach (DungeonConstraint constraint in dungeon.Constraints)
			{
				if (selected[constraint.ID] == false)
					continue;
				if (highest.TryGetValue(constraint.FamilyKey, out DungeonConstraint kept) && kept.Level >= constraint.Level)
					continue;
				highest[constraint.FamilyKey] = constraint;
			}
			foreach (DungeonConstraint constraint in dungeon.Constraints)
			{
				if (selected[constraint.ID] && highest[constraint.FamilyKey] != constraint)
					selected[constraint.ID] = false;
			}
		}

		/// <summary> 같은 묶음의 다른 단계가 켜져 있으면 true (설정 창 배타 표시) </summary>
		public static bool IsBlockedBySibling(Dungeon dungeon, IReadOnlyDictionary<int, bool> selected, DungeonConstraint target)
		{
			foreach (DungeonConstraint constraint in dungeon.Constraints)
			{
				if (constraint != target && constraint.FamilyKey == target.FamilyKey && selected[constraint.ID])
					return true;
			}
			return false;
		}

		/// <summary> 묶음 열 순서. 던전 목록에 처음 나온 순서 </summary>
		public static List<string> FamilyOrder(Dungeon dungeon)
		{
			List<string> order = new();
			foreach (DungeonConstraint constraint in dungeon.Constraints)
			{
				if (order.Contains(constraint.FamilyKey) == false)
					order.Add(constraint.FamilyKey);
			}
			return order;
		}
	}
}
