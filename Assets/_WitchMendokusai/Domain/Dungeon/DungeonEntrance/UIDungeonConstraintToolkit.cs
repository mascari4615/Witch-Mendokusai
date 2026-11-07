using System.Collections.Generic;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 던전 제약 표시 뷰. 제약마다 켜짐 표시와 설명 한 줄
	/// DungeonManager 는 선택 여부와 무관하게 던전 제약 전부를 적용 (DungeonContext.Constraints)
	/// 그래서 행은 항상 켜짐, 클릭 토글 없음. 꺼짐 표시는 적용 중인 규칙이 빠진 것처럼 읽힘
	/// </summary>
	public class UIDungeonConstraintToolkit : VisualElement
	{
		public const string USS_CLASS = "wm-dungeon-constraint";
		private const string USS_ROW = USS_CLASS + "__row";
		private const string USS_ROW_SELECTED = USS_ROW + "--selected";
		private const string USS_CHECK = USS_CLASS + "__check";
		private const string USS_TEXT = USS_CLASS + "__text";
		private const string USS_KNOB = USS_CLASS + "__knob";
		private const string USS_LINE = USS_CLASS + "__line";

		private Dungeon dungeon;
		private readonly List<VisualElement> constraintRows = new();
		private readonly List<Label> constraintTexts = new();

		public UIDungeonConstraintToolkit()
		{
			AddToClassList(USS_CLASS);
		}

		public void SetDungeon(Dungeon dungeon) => this.dungeon = dungeon;

		public void UpdateUI()
		{
			EnsureRowCount(dungeon.Constraints.Count);

			for (int i = 0; i < constraintRows.Count; i++)
			{
				if (i < dungeon.Constraints.Count)
				{
					constraintTexts[i].text = dungeon.Constraints[i].Description;
					constraintRows[i].style.display = DisplayStyle.Flex;
				}
				else
				{
					constraintRows[i].style.display = DisplayStyle.None;
				}
			}
		}

		private void EnsureRowCount(int count)
		{
			while (constraintRows.Count < count)
			{
				VisualElement row = new VisualElement { pickingMode = PickingMode.Ignore };
				row.AddToClassList(USS_ROW);
				row.AddToClassList(USS_ROW_SELECTED);

				VisualElement check = new VisualElement { pickingMode = PickingMode.Ignore };
				check.AddToClassList(USS_CHECK);
				row.Add(check);

				VisualElement knob = new VisualElement { pickingMode = PickingMode.Ignore };
				knob.AddToClassList(USS_KNOB);
				check.Add(knob);

				VisualElement line = new VisualElement { pickingMode = PickingMode.Ignore };
				line.AddToClassList(USS_LINE);
				row.Add(line);

				Label text = new Label { pickingMode = PickingMode.Ignore };
				text.AddToClassList(USS_TEXT);
				row.Add(text);

				constraintRows.Add(row);
				constraintTexts.Add(text);
				Add(row);
			}
		}
	}
}
