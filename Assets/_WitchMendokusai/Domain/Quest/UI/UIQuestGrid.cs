using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VContainer;
using VContainer.Unity;

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

		private void Awake()
		{
			LifetimeScope.Find<SceneLifetimeScope>()?.Container.Inject(this);
		}

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

			int activeSlotCount = 0;
			foreach (UIQuestSlot slot in Slots.Cast<UIQuestSlot>())
			{
				RuntimeQuest quest = Data.ElementAtOrDefault(slot.Index);

				if (quest == null)
				{
					slot.SetSlot(null);
					slot.gameObject.SetActive(dontShowEmptySlot == false);
				}
				else
				{
					bool slotActive = (curFilter == QuestType.None) || (quest.Type == curFilter);

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

					slot.gameObject.SetActive(slotActive);
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