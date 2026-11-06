using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	public class UIQuestGrid : UIDataGrid<RuntimeQuest>
	{
		[SerializeField] private Transform filtersParent;
		private QuestType curFilter = QuestType.None;

		private UIQuestToolTip questToolTip;

		private TimeManager timeManager;

		[Inject]
		public void Construct(TimeManager timeManager)
		{
			this.timeManager = timeManager;
		}

		// TODO: [SerializeField] private bool resetFilterOnEnable = true;

		private readonly List<RuntimeQuest> filteredQuests = new();

		private RuntimeQuest CurQuest => filteredQuests.ElementAtOrDefault(CurSlotIndex);

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
			filteredQuests.Clear();
			foreach (RuntimeQuest candidate in Data)
				if (curFilter == QuestType.None || candidate.Type == curFilter)
					filteredQuests.Add(candidate);

			if (CurSlotIndex >= filteredQuests.Count)
				CurSlotIndex = filteredQuests.Count - 1;

			int activeSlotCount = 0;
			foreach (UIQuestSlot slot in Slots.Cast<UIQuestSlot>())
			{
				RuntimeQuest quest = filteredQuests.ElementAtOrDefault(slot.Index);

				if (quest == null)
				{
					slot.SetSlot(null);
					slot.gameObject.SetActive(dontShowEmptySlot == false);
				}
				else
				{
					slot.SetRuntimeQuestState(quest.State);
					slot.SetQuest(quest);
					slot.UpdateUI();

					if (quest.QuestSOID == -1)
					{
						slot.SetSlot(null, quest.Name, quest.Description);
					}
					else
					{
						QuestSO questSO = SOHelper.GetQuestSO(quest.QuestSOID);
						if (questSO != null)
							slot.SetSlot(questSO);
						else
							slot.SetSlot(null, quest.Name, quest.Description);
					}

					slot.gameObject.SetActive(true);
				}

				activeSlotCount += slot.gameObject.activeSelf ? 1 : 0;
			}

			if (clickToolTip != null && CurSlot != null && CurSlot.Data != null)
				clickToolTip.SetToolTipContent(CurSlot.Data);

			if (questToolTip != null)
			{
				questToolTip.SetQuest(CurQuest);
				questToolTip.UpdateUI();
			}

			UpdateNoElementInfo();
		}

		public void SetFilter(QuestType filter)
		{
			curFilter = filter;
			UpdateUI();
		}

		private void OnEnable() => timeManager.RegisterCallback(UpdateUI);
		private void OnDisable() => timeManager.RemoveCallback(UpdateUI);
	}
}