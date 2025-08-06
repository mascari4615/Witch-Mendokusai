using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	public class UITabInventoryPanel : UIPanel
	{
		private UIItemGrid itemInventoryUI;

		protected override void OnInit()
		{
			itemInventoryUI = GetComponentInChildren<UIItemGrid>(true);
			itemInventoryUI.Init();
		}

		public override void UpdateUI()
		{
			itemInventoryUI.UpdateUI();
		}
	}
}
