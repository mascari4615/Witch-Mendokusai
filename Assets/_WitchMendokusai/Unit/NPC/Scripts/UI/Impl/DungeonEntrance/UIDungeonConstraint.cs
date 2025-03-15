using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace WitchMendokusai
{
	public class UIDungeonConstraint : UIPanel
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
			// dungeon.ConstraintSelected 전부 출력
			Debug.Log($"{nameof(UpdateUI)} {dungeon.name} {dungeon.ConstraintSelected.Count}");
			foreach (KeyValuePair<int, bool> entry in dungeon.ConstraintSelected)
			{
				Debug.Log($"UpdateUI {entry.Key} {entry.Value}");
			}

			Debug.Log($"===============================");
		
			// 임의로 모든 Dungeon의 제약을 출력
			SOHelper.ForEach<Dungeon>(dungeon =>
			{
				Debug.Log($"{dungeon.name} {dungeon.ConstraintSelected.Count}, {dungeon == this.dungeon}");

				foreach (KeyValuePair<int, bool> entry in dungeon.ConstraintSelected)
				{
					Debug.Log($"UpdateUI {entry.Key} {entry.Value}");
				}
			});
			
			Debug.Log($"===============================");
			
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