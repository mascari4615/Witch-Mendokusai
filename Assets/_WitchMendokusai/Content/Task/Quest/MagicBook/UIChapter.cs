using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace WitchMendokusai
{
	public class UIChapter : UIBase
	{
		private UIQuestSlot[] questSlots;
		[SerializeField] private RectTransform content;

		public override void Init()
		{
			// Debug.Log($"{name} {nameof(Init)}");

			questSlots = GetComponentsInChildren<UIQuestSlot>(true);

			foreach (UIQuestSlot slot in questSlots)
				slot.Init();
		}

		public void SetToolTip(ToolTip toolTip, UIQuestToolTip questToolTip)
		{
			// Debug.Log($"{name} {nameof(SetToolTip)}");

			foreach (UIQuestSlot slot in questSlots)
			{
				slot.ToolTipTrigger.SetClickToolTip(toolTip);
				slot.SetClickAction((slot) =>
				{
					slot.ToolTipTrigger.ClickToolTip.gameObject.SetActive(true);

					RuntimeQuest quest = QuestManager.Instance.GetQuest(slot.DataSO as QuestSO);

					// QuestManager.Instance.Quests.Data ToString
					// Debug.Log($"Q {QuestManager.Instance.Quests.Data.Select(x => x.SO.ID.ToString()).Aggregate((x, y) => $"{x}, {y}")}");
					// Debug.Log($"ClickClick {slot.name} | {quest} | {slot.DataSO as QuestSO} | {slot.DataSO}");
					questToolTip.SetQuest(quest);
					questToolTip.UpdateUI();
				});
			}
		}

		protected override void OnOpen()
		{
			// Debug.Log($"{name} {nameof(OnOpen)}");
		
			// 스크롤 위치 초기화
			content.anchoredPosition = Vector2.zero;
		}

		public override void UpdateUI()
		{
			// Debug.Log($"{name} {nameof(UpdateUI)}");
			
			foreach (UIQuestSlot slot in questSlots)
			{
				RuntimeQuest runtimeQuest = QuestManager.Instance.GetQuest(slot.DataSO as QuestSO);

				// HACK:
				slot.SetDisable(false);

				// 진행 중
				if (runtimeQuest != null)
				{
					slot.SetRuntimeQuestState(runtimeQuest.State);
					slot.SetQuest(runtimeQuest);
				}
				else
				{
					QuestSO questData = slot.DataSO as QuestSO;
					QuestState state = QuestManager.Instance.GetQuestState(questData.ID);
					slot.SetDisable(state == QuestState.Locked);
				}

				slot.UpdateUI();
			}
		}
	}
}