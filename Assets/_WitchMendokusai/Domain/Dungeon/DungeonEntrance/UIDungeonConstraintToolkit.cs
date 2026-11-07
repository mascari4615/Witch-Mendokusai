using System.Collections.Generic;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 던전 제약 선택 뷰. 제약마다 체크 표시와 설명 한 줄, 클릭으로 토글
	/// 토글 로직은 구 UIDungeonConstraint 그대로 (ID/index 표현 원본 유지)
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
		private readonly List<Button> constraintRows = new();
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
					DungeonConstraint constraint = dungeon.Constraints[i];

					constraintTexts[i].text = constraint.Description;
					constraintRows[i].EnableInClassList(USS_ROW_SELECTED, dungeon.ConstraintSelected[constraint.ID]);
					constraintRows[i].style.display = DisplayStyle.Flex;
				}
				else
				{
					constraintRows[i].style.display = DisplayStyle.None;
				}
			}
		}

		public void ToggleConstraint(int index)
		{
			dungeon.ConstraintSelected[index] = dungeon.ConstraintSelected[index] == false;
			UpdateUI();
		}

		private void EnsureRowCount(int count)
		{
			while (constraintRows.Count < count)
			{
				int index = constraintRows.Count;

				Button row = new Button(() => { UiSfx.Play(UiSfxEvent.Confirm); ToggleConstraint(index); });
				row.AddToClassList(USS_ROW);

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
