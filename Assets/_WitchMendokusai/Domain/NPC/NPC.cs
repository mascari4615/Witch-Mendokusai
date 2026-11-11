using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace WitchMendokusai
{
	// NPC가 다루는 UI 정보
	[Serializable]
	public struct NPCPanelInfo
	{
		public NPCPanelType Type;
		public List<DataSO> DataSOs;
	}

	[CreateAssetMenu(fileName = nameof(NPC), menuName = "WM/Variable/" + nameof(Unit) + "/" + nameof(NPC))]
	public class NPC : Unit
	{
		[field: Header("_" + nameof(NPC))]
		[field: SerializeField] public List<NPCPanelInfo> PanelInfos { get; private set; }
		[field: SerializeField] public List<QuestSO> QuestData { get; private set; }
		[field: Tooltip("상점 구매가를 0 으로 처리. 아이템 구매가는 그대로")]
		[field: SerializeField] public bool IsShopFree { get; private set; }
		[field: Tooltip("개발 빌드에서만 보이는 NPC. 릴리스 빌드에서는 배치돼 있어도 꺼짐")]
		[field: SerializeField] public bool DevBuildOnly { get; private set; }

		public List<ItemDataBuffer> ItemDataBuffers => GetAllDataSOs(NPCPanelType.Shop).Cast<ItemDataBuffer>().ToList();

		public int GetShopPrice(ItemData itemData) => IsShopFree ? 0 : itemData.PurchasePrice;

		private List<DataSO> GetAllDataSOs(NPCPanelType panelType)
		{
			return PanelInfos
					.Where(i => i.Type == panelType)
					.SelectMany(i => i.DataSOs)
					.ToList();
		}

		public List<NPCPanelType> GetPanelTypeList()
		{
			return PanelInfos.Select(i => i.Type).ToList();
		}
	}
}