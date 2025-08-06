using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace WitchMendokusai
{
	public enum TabPanelType
	{
		None = -1,

		TabMenu = 0,
		MagicBook = 1,
		Quest = 2,
		Inventory = 3,
		Doll = 4,
		Setting = 5,
		Map = 6,
		
		Count = 7
	}

	public class UITab : UIContentBase<TabPanelType>
	{
		public override TabPanelType DefaultPanel => TabPanelType.None;

		public override void Init()
		{
			Panels[TabPanelType.TabMenu] = FindFirstObjectByType<UITabMenu>(FindObjectsInactive.Include);
			Panels[TabPanelType.MagicBook] = FindFirstObjectByType<UIMagicBookPanel>(FindObjectsInactive.Include);
			Panels[TabPanelType.Quest] = FindFirstObjectByType<UIQuestPanel>(FindObjectsInactive.Include);
			Panels[TabPanelType.Inventory] = FindFirstObjectByType<UITabInventoryPanel>(FindObjectsInactive.Include);
			Panels[TabPanelType.Doll] = FindFirstObjectByType<UIDollPanel>(FindObjectsInactive.Include);
			Panels[TabPanelType.Setting] = FindFirstObjectByType<UISetting>(FindObjectsInactive.Include);
			Panels[TabPanelType.Map] = FindFirstObjectByType<UIMap>(FindObjectsInactive.Include);
		}
	}
}