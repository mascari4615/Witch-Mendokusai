using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace WitchMendokusai
{
	public class UIDungeonEntrance : UIPanel
	{
		[SerializeField] private Transform dungeonSelectButtonsParent;
		[SerializeField] private GameObject dungeonSelectButtonPrefab;
		[SerializeField] private Button dungeonEnterButton;
		[SerializeField] private ToolTip toolTip;

		protected List<UISlot> dungeonSelectButtons;

		private UIRewards rewardUI;
		private UIDungeonConstraint constraintUI;
		private int curDungeonIndex = 0;
		private List<Dungeon> dungeons;

		public override bool IsFullscreen => true;
		private Dungeon CurDungeon => dungeons[curDungeonIndex];

		protected override void OnInit()
		{
			rewardUI = GetComponentInChildren<UIRewards>(true);
			rewardUI.Init();

			constraintUI = GetComponentInChildren<UIDungeonConstraint>(true);
			constraintUI.Init();

			dungeonSelectButtons = new List<UISlot>();
			for (int i = 0; i < 10; i++) // TODO: Magic Number
			{
				GameObject buttonInstance = Instantiate(dungeonSelectButtonPrefab, dungeonSelectButtonsParent);
				dungeonSelectButtons.Add(buttonInstance.GetComponent<UISlot>());
				dungeonSelectButtons[i].SetSlotIndex(i);
				dungeonSelectButtons[i].Init();
				dungeonSelectButtons[i].SetClickAction((slot) => { SelectDungeon(slot.Index); });
				dungeonSelectButtons[i].gameObject.SetActive(false);
			}

			dungeonEnterButton.navigation = new Navigation
			{
				mode = Navigation.Mode.Explicit,
				selectOnUp = null,
				selectOnDown = null,
				selectOnLeft = dungeonSelectButtons.First().Selectable,
				selectOnRight = null
			};
			dungeonEnterButton.onClick.AddListener(EnterTheDungeon);
		}

		public override void SetNPC(NPCObject npc)
		{
			dungeons = NPCUtil.GetDungeons(npc.Data);

			if (dungeons == null || dungeons.Count == 0)
				Debug.LogError("No Dungeon Data");

			int maxButtonIndex = Mathf.Max(dungeonSelectButtons.Count - 1, dungeons.Count - 1);
			for (int i = 0; i < dungeonSelectButtons.Count; i++)
			{
				if (i < dungeons.Count)
				{
					dungeonSelectButtons[i].SetSlot(dungeons[i]);
					dungeonSelectButtons[i].gameObject.SetActive(true);

					dungeonSelectButtons[i].SetNavigation(
						new Navigation
						{
							mode = Navigation.Mode.Explicit,
							selectOnUp = (i > 0) ? dungeonSelectButtons[i - 1].Selectable : null,
							selectOnDown = (i < maxButtonIndex) ? dungeonSelectButtons[i + 1].Selectable : null,
							selectOnLeft = null,
							selectOnRight = null
						}
					);
				}
				else
				{
					dungeonSelectButtons[i].gameObject.SetActive(false);
				}
			}

			dungeonSelectButtons.First().Select(); // 첫 번째 던전에 UI Select Focus 주기 - 250315. 19:20
		}

		protected override void OnOpen()
		{
			SelectDungeon(0);
		}

		public override void UpdateUI()
		{
			UpdateTooltip();
		}

		public void SelectDungeon(int index)
		{
			curDungeonIndex = index;
			UpdateTooltip();

			dungeonEnterButton.Select();
		}

		private void UpdateTooltip()
		{
			toolTip.SetToolTipContent(dungeonSelectButtons[curDungeonIndex].Data);

			rewardUI.UpdateUI(CurDungeon.Rewards);

			constraintUI.SetDungeon(CurDungeon);
			constraintUI.UpdateUI();
		}

		public void EnterTheDungeon()
		{
			PanelGroup.ClosePanel();
			DungeonManager.Instance.StartDungeon(CurDungeon);
		}
	}
}