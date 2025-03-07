using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	[CreateAssetMenu(fileName = nameof(ItemDataBuffer), menuName = "DataBuffer/" + nameof(ItemData))]
	public class ItemDataBuffer : DataBufferSO<ItemData>
	{
		[System.NonSerialized] public Dictionary<int, int> itemCountDic = new();
		public override void Add(ItemData itemData)
		{
			if (itemCountDic.ContainsKey(itemData.ID))
			{
				itemCountDic[itemData.ID]++;
			}
			else
			{
				itemCountDic.Add(itemData.ID, 1);
				Datas.Add(itemData);
			}
		}

		public override bool Remove(ItemData itemData)
		{
			if (itemCountDic.ContainsKey(itemData.ID))
			{
				itemCountDic[itemData.ID]--;
				if (itemCountDic[itemData.ID] <= 0)
				{
					itemCountDic.Remove(itemData.ID);
					Datas.Remove(itemData);
				}
				return true;
			}
			return false;
		}

		public override void Clear()
		{
			Datas.Clear();
			itemCountDic.Clear();
		}
	}
}