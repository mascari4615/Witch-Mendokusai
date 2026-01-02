using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace WitchMendokusai
{
	public class UIQuestGrid : UIDataGrid<RuntimeQuest>
	{
		[SerializeField] private Transform filtersParent;
		private QuestType curFilter = QuestType.None;

		private UIQuestToolTip questToolTip;

		// TODO: [SerializeField] private bool resetFilterOnEnable = true;

		private RuntimeQuest CurQuest => Data.Count > 0 ? Data[CurSlotIndex] : null;

		public override void Init()
		{
			base.Init();

			// 필터 버튼 초기화
			if (filtersParent != null)
			{
				UISlot[] fillerButtons = filtersParent.GetComponentsInChildren<UISlot>(true);
				for (int i = 0; i < fillerButtons.Length; i++)
				{
					fillerButtons[i].Init();
					fillerButtons[i].SetSlotIndex(i);
					fillerButtons[i].SetClickAction((slot) =>
					{
						QuestType newFilter = (QuestType)(slot.Index - 1);
						SetFilter(newFilter);
					});
				}
			}

			questToolTip = GetComponentInChildren<UIQuestToolTip>(true);

			if (questToolTip != null)
				questToolTip.Init();
		}

		public override void UpdateUI()
		{
			if (CurSlotIndex >= Data.Count)
				CurSlotIndex = Data.Count - 1;

			base.UpdateUI();

			if (questToolTip != null)
			{
				questToolTip.SetQuest(CurQuest);
				questToolTip.UpdateUI();
			}
		}

		protected override void SetSlotData(int index, RuntimeQuest quest)
		{
			if (quest == null)
			{
				Slots[index].SetSlot(null);
				return;
			}

			UIQuestSlot slot = Slots[index] as UIQuestSlot;

			slot.SetRuntimeQuestState(quest.State);
			slot.SetQuest(quest);
			slot.UpdateUI();

			if (quest.SO == null)
				slot.SetSlot(null, quest.Name, quest.Description);
			else
				slot.SetSlot(quest.SO);
		}

		public void SetFilter(QuestType filter)
		{
			curFilter = filter;
			SetFilterFunc((quest) => (curFilter == QuestType.None) || (quest.Type == curFilter));
			UpdateUI();
		}

		private void OnEnable() => TimeManager.Instance.RegisterCallback(UpdateUI);
		private void OnDisable() => TimeManager.Instance.RemoveCallback(UpdateUI);
	}
}