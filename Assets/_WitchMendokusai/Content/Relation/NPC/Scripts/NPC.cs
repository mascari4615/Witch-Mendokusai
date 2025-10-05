using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

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

		public List<ItemDataBuffer> ItemDataBuffers => GetAllDataSOs(NPCPanelType.Shop).Cast<ItemDataBuffer>().ToList();

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