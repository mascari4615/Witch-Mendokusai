using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static WitchMendokusai.SOHelper;

namespace WitchMendokusai
{
	public class UIQuestTooltipCriteria : MonoBehaviour
	{
		private CanvasGroup canvasGroup;
		private UISlot[] slots;

		public void Init()
		{
			canvasGroup = gameObject.AddComponent<CanvasGroup>();
			slots = GetComponentsInChildren<UISlot>(true);

			foreach (UISlot slot in slots)
			{
				slot.Init();
				slot.gameObject.SetActive(false);
			}
		}

		public void SetCriteria(RuntimeQuest quest)
		{
			canvasGroup.alpha = 1;
			if (quest == null || quest.Criteria == null || quest.Criteria.Count == 0)
			{
				canvasGroup.alpha = 0;
				// foreach (UISlot slot in slots)
				// 	slot.gameObject.SetActive(false);
				return;
			}

			List<RuntimeCriteria> criteria = quest.Criteria;

			for (int i = 0; i < slots.Length; i++)
			{
				if (i < criteria.Count)
				{
					slots[i].gameObject.SetActive(true);

					Sprite criteriaSprite = null;
					string criteriaName = "";
					string criteriaDesc = "";

					switch (criteria[i].Criteria)
					{
						case IntCriteria intCriteria:
							// HACK, TODO: Refactor
							criteriaSprite = GetGameStatData(GameStatType.VILLAGE_QUEST_EXP).Sprite;
							criteriaName = intCriteria.IntVariable.Name;
							break;
						case ItemCountCriteria itemCountCriteria:
							criteriaSprite = GetItemData(itemCountCriteria.ItemID).Sprite;
							criteriaName = GetItemData(itemCountCriteria.ItemID).Name;
							break;
						case StatCriteria statCriteria:
							// HACK
							criteriaSprite = GetGameStatData(GameStatType.VILLAGE_QUEST_EXP).Sprite;
							criteriaName = statCriteria.Type.ToString();
							break;
						case GameStatCriteria gameStatCriteria:
							// HACK
							criteriaSprite = GetGameStatData(GameStatType.VILLAGE_QUEST_EXP).Sprite;
							criteriaName = gameStatCriteria.Type.ToString();
							break;
					}

					if (criteria[i].Criteria is NumCriteria numCriteria)
					{
						criteriaDesc = $"{numCriteria.GetCurValue()}/{numCriteria.GetTargetValue()} ({numCriteria.GetProgress() * 100}%)";
					}

					slots[i].SetSlot(criteriaSprite, criteriaName, criteriaDesc);
				}
				else
				{
					slots[i].gameObject.SetActive(false);
				}
			}
		}
	}
}