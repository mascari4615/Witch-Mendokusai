using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace WitchMendokusai
{
	public class UIDungeonEntrance : UINPCPanel
	{
		[SerializeField] private Transform dungeonSelectButtonParent;
		[SerializeField] private UISlot dungeonSlot;

		private List<UISlot> dungeonSelectButtons;
		private UIRewards rewardUI;
		private UIDungeonConstraint constraintUI;

		private int curDungeonIndex = 0;
		private List<Dungeon> dungeons;

		private Dungeon CurDungeon => dungeons[curDungeonIndex];

		public override void Init()
		{
			base.Init();

			dungeonSelectButtons = dungeonSelectButtonParent.GetComponentsInChildren<UISlot>(true).ToList();

			for (int i = 0; i < dungeonSelectButtons.Count; i++)
			{
				dungeonSelectButtons[i].SetSlotIndex(i);
				dungeonSelectButtons[i].Init();
				dungeonSelectButtons[i].SetClickAction((UISlot slot) => SelectDungeon(slot.Index));
			}

			rewardUI = GetComponentInChildren<UIRewards>(true);
			rewardUI.Init();

			constraintUI = GetComponentInChildren<UIDungeonConstraint>(true);
			constraintUI.Init();
		}

		public override void SetNPC(NPCObject npc)
		{
			dungeons = NPCUtil.GetDungeons(npc.Data);

			if (dungeons == null || dungeons.Count == 0)
				Debug.LogError("No Dungeon Data");
		}

		public override void UpdateUI()
		{
			for (int i = 0; i < dungeonSelectButtons.Count; i++)
			{
				if (i < dungeons.Count)
				{
					dungeonSelectButtons[i].SetSlot(dungeons[i]);
					dungeonSelectButtons[i].gameObject.SetActive(true);
				}
				else
				{
					dungeonSelectButtons[i].gameObject.SetActive(false);
				}
			}

			SelectDungeon(0);
		}

		public void SelectDungeon(int index)
		{
			curDungeonIndex = index;
			UpdateDungeonPanel();
		}

		private void UpdateDungeonPanel()
		{
			dungeonSlot.SetSlot(CurDungeon);
			rewardUI.UpdateUI(CurDungeon.Rewards);

			constraintUI.SetDungeon(CurDungeon);
			constraintUI.UpdateUI();
		}

		public void EnterTheDungeon()
		{
			DungeonManager.Instance.StartDungeon(CurDungeon);
		}
	}
}