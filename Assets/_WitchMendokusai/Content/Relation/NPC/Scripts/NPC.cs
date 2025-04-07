using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

namespace WitchMendokusai
{
	public enum NPCType
	{
		None = -1,
	
		Shop,
		DungeonEntrance,
		Pot,
		Anvil,
		Furnace,
		CraftingTable,
	
		Count
	}

	// NPC가 다루는 UI 정보
	[Serializable]
	public struct NPCPanelInfo
	{
		public NPCType Type;
		public List<DataSO> DataSOs;
	}

	[CreateAssetMenu(fileName = nameof(NPC), menuName = "Variable/" + nameof(Unit) + "/" + nameof(NPC))]
	public class NPC : Unit
	{
		[field: Header("_" + nameof(NPC))]
		[field: SerializeField] public List<NPCPanelInfo> PanelInfos { get; private set; }
		[field: SerializeField] public List<QuestSO> QuestData { get; private set; }

		public List<ItemDataBuffer> ItemDataBuffers => GetAllDataSOs(NPCType.Shop).Cast<ItemDataBuffer>().ToList();

		private List<DataSO> GetAllDataSOs(NPCType npcType)
		{
			return PanelInfos
					.Where(i => i.Type == npcType)
					.SelectMany(i => i.DataSOs)
					.ToList();
		}

		public List<NPCType> GetNPCTypeList()
		{
			return PanelInfos.Select(i => i.Type).ToList();
		}
	}
}