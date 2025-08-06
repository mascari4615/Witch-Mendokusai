using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace WitchMendokusai
{
	public class UIDungeonConstraint : UIBase
	{
		// TODO: 던전 난이도/제약

		// 난이도 : 보통, 어려움, 매우 어려움
		// 하나의 UI

		// 제약 : ~

		// 슬롯으로?

		private Dungeon dungeon;
		private List<UISlot> constraintSlots;

		public override void Init()
		{
			constraintSlots = GetComponentsInChildren<UISlot>(true).ToList();
			
			for (int i = 0; i < constraintSlots.Count; i++)
			{
				constraintSlots[i].SetSlotIndex(i);
				constraintSlots[i].Init();
				constraintSlots[i].SetClickAction((UISlot slot) => ToggleConstraint(slot.Index));
			}
		}

		public void SetDungeon(Dungeon dungeon)
		{
			this.dungeon = dungeon;
		}

		public override void UpdateUI()
		{
			for (int i = 0; i < constraintSlots.Count; i++)
			{
				if (i < dungeon.Constraints.Count)
				{
					DungeonConstraint constraint = dungeon.Constraints[i];

					constraintSlots[i].SetSlot(constraint);
					constraintSlots[i].SetDisable(dungeon.ConstraintSelected[constraint.ID] == false);
					constraintSlots[i].gameObject.SetActive(true);
				}
				else
				{
					constraintSlots[i].gameObject.SetActive(false);
				}
			}
		}

		public void ToggleConstraint(int index)
		{
			dungeon.ConstraintSelected[index] = !dungeon.ConstraintSelected[index];
			UpdateUI();
		}
	}
}