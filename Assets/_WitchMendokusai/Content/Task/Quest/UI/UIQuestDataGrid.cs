using System.Collections;
using System.Linq;
using UnityEngine;

namespace WitchMendokusai
{
	public class UIQuestDataGrid : UIDataGrid<QuestSO>
	{

		public override void UpdateUI()
		{
			for (int i = 0; i < Slots.Count; i++)
			{
				UIQuestSlot slot = Slots[i] as UIQuestSlot;
				QuestSO quest = DataBufferSO.Data.ElementAtOrDefault(i);

				if (quest == null)
				{
					slot.SetSlot(null);
					slot.gameObject.SetActive(dontShowEmptySlot == false);
				}
				else
				{
					// bool slotActive = quest.State > QuestState.Wait;

					slot.SetSlot(quest);
					// slot.gameObject.SetActive(slotActive);
					slot.gameObject.SetActive(true);
				}
			}
		}
	}
}