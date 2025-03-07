using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	public class UIQuestPanel : UIPanel
	{
		private UIQuestGrid questBufferUI;

		public override void Init()
		{
			questBufferUI = GetComponentInChildren<UIQuestGrid>(true);
			questBufferUI.Init();
		}

		public override void UpdateUI()
		{
			questBufferUI.UpdateUI();
		}
	}
}
