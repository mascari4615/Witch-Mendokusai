using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static WitchMendokusai.SOHelper;

namespace WitchMendokusai
{
	public class UIRewards : MonoBehaviour
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

		public void UpdateUI(List<RewardInfo> infos) =>
			UpdateUI(infos.ConvertAll(x => new RewardInfoData(x)));

		public void UpdateUI(List<RewardInfoData> data)
		{
			canvasGroup.alpha = 1;

			if (data == null || data.Count == 0)
			{
				canvasGroup.alpha = 0;
				// foreach (UISlot slot in slots)
				// 	slot.gameObject.SetActive(false);
				return;
			}

			for (int i = 0; i < slots.Length; i++)
			{
				if (i < data.Count)
				{
					slots[i].gameObject.SetActive(true);

					switch (data[i].Type)
					{
						case RewardType.Item:
							ItemData itemData = GetItemData(data[i].DataSOID);
							slots[i].SetSlot(itemData);
							break;
						case RewardType.Gold:
							slots[i].SetSlot(GetGameStatData(GameStatType.NYANG), data[i].Amount);
							break;
						case RewardType.Exp:
							slots[i].SetSlot(GetGameStatData(GameStatType.VILLAGE_QUEST_EXP), data[i].Amount);
							break;
					}
				}
				else
				{
					slots[i].gameObject.SetActive(false);
				}
			}
		}
	}
}