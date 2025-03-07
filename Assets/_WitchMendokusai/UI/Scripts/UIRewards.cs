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

		public void UpdateUI(List<RewardInfoData> datas)
		{
			canvasGroup.alpha = 1;

			if (datas == null || datas.Count == 0)
			{
				canvasGroup.alpha = 0;
				// foreach (UISlot slot in slots)
				// 	slot.gameObject.SetActive(false);
				return;
			}

			for (int i = 0; i < slots.Length; i++)
			{
				if (i < datas.Count)
				{
					slots[i].gameObject.SetActive(true);

					switch (datas[i].Type)
					{
						case RewardType.Item:
							ItemData itemData = GetItemData(datas[i].DataSOID);
							slots[i].SetSlot(itemData);
							break;
						case RewardType.Gold:
							slots[i].SetSlot(SOManager.Instance.Nyang, datas[i].Amount);
							break;
						case RewardType.Exp:
							slots[i].SetSlot(SOManager.Instance.VQExp, datas[i].Amount);
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