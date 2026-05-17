using System.Collections.Generic;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 던전 제약 선택 뷰 — uGUI UIDungeonConstraint(62L) 의 Toolkit 병렬 신설 (TASK-WM-113 S3-D).
	/// 구 UIDungeonConstraint 는 UIDungeonEntrance 가 여전히 실사용 → 빅뱅 X,
	/// 본 뷰는 *신규 병렬* (사용처 = 던전엔트런스 S3-E `UIDungeonEntranceToolkit`).
	/// 구 UIDungeonConstraint 의 제약 토글 로직 1:1 보존(ID/index 표현 원본 그대로 — 충실
	/// 마이그, 잠재 불일치 fix 는 별 사안). UIBase → VisualElement, prefab 고정 N UISlot →
	/// lazy ToolkitSlot 풀(GC 회피, hide-excess 시맨틱 등가, 클릭 액션 생성 시 배선).
	/// 구 UIDungeonConstraint deletion = 사용처 전부 이행 후 최후 E.
	/// </summary>
	public class UIDungeonConstraintToolkit : VisualElement
	{
		public const string USS_CLASS = "wm-dungeon-constraint";

		private Dungeon dungeon;
		private readonly List<ToolkitSlot> constraintSlots = new();

		public UIDungeonConstraintToolkit()
		{
			AddToClassList(USS_CLASS);
		}

		public void SetDungeon(Dungeon dungeon) => this.dungeon = dungeon;

		public void UpdateUI()
		{
			EnsureSlotCount(dungeon.Constraints.Count);

			for (int i = 0; i < constraintSlots.Count; i++)
			{
				if (i < dungeon.Constraints.Count)
				{
					DungeonConstraint constraint = dungeon.Constraints[i];

					constraintSlots[i].SetSlot(constraint);
					constraintSlots[i].SetDisable(dungeon.ConstraintSelected[constraint.ID] == false);
					constraintSlots[i].style.display = DisplayStyle.Flex;
				}
				else
				{
					constraintSlots[i].style.display = DisplayStyle.None;
				}
			}
		}

		public void ToggleConstraint(int index)
		{
			dungeon.ConstraintSelected[index] = dungeon.ConstraintSelected[index] == false;
			UpdateUI();
		}

		private void EnsureSlotCount(int count)
		{
			while (constraintSlots.Count < count)
			{
				ToolkitSlot slot = new ToolkitSlot();
				slot.SetSlotIndex(constraintSlots.Count);
				slot.SetClickAction((ToolkitSlot clickedSlot) => ToggleConstraint(clickedSlot.Index));
				constraintSlots.Add(slot);
				Add(slot);
			}
		}
	}
}
