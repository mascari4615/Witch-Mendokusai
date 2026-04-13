using UnityEngine;

namespace WitchMendokusai
{
	public class UIQuestNode : UIQuestSlot
	{
		public void SetNode(QuestSO quest, Vector2 position)
		{
			SetSlot(quest);
			GetComponent<RectTransform>().anchoredPosition = position;
		}
	}
}
