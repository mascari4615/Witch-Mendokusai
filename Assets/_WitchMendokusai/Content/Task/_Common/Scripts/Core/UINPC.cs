using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace WitchMendokusai
{
	// NPC Unit이 사용 중
	public enum NPCPanelType
	{
		None = -1,

		NPC = 0,
		Shop = 1,
		DungeonEntrance = 2,
		Pot = 3,
		Anvil = 4,
		Furnace = 5,
		CraftingTable = 6,

		Count = 7,
	}

	public class UINPC : UIContentBase<NPCPanelType>
	{
		public override NPCPanelType DefaultPanel => NPCPanelType.None;

		public override void Init()
		{
			Panels[NPCPanelType.NPC] = FindFirstObjectByType<UINPCMenu>(FindObjectsInactive.Include);
			Panels[NPCPanelType.Shop] = FindFirstObjectByType<UIShop>(FindObjectsInactive.Include);
			Panels[NPCPanelType.DungeonEntrance] = FindFirstObjectByType<UIDungeonEntrance>(FindObjectsInactive.Include);
			Panels[NPCPanelType.Pot] = FindFirstObjectByType<UIPot>(FindObjectsInactive.Include);
			Panels[NPCPanelType.Anvil] = FindFirstObjectByType<UIAnvil>(FindObjectsInactive.Include);
			Panels[NPCPanelType.Furnace] = FindFirstObjectByType<UIFurnace>(FindObjectsInactive.Include);
			Panels[NPCPanelType.CraftingTable] = FindFirstObjectByType<UICraftingTable>(FindObjectsInactive.Include);
		}
	}
}