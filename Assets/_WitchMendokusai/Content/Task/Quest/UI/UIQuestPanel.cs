using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	public class UIQuestPanel : UIPanel
	{
		private UIQuestGrid questGrid;

		public override void Init()
		{
			questGrid = GetComponentInChildren<UIQuestGrid>(true);
			questGrid.Init();
		}

		public override void UpdateUI()
		{
			questGrid.UpdateUI();
		}
	}
}
