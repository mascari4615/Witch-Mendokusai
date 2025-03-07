using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static WitchMendokusai.SOHelper;

namespace WitchMendokusai
{
	public class UIMagicBookPanel : UIPanels
	{
		private ToolTip toolTip;
		private UIQuestToolTip questToolTip;

		public override void Init()
		{
			// Debug.Log(nameof(Init));

			base.Init();

			toolTip = GetComponentInChildren<ToolTip>(true);

			questToolTip = GetComponentInChildren<UIQuestToolTip>(true);
			questToolTip.Init();

			foreach (UIChapter chapter in panels.Cast<UIChapter>())
				chapter.SetToolTip(toolTip, questToolTip);
		}

		public override void UpdateUI()
		{
			// Debug.Log(nameof(UpdateUI) + DataManager.Instance.QuestState[0]);

			base.UpdateUI();

			foreach (UIChapter chapter in panels.Cast<UIChapter>())
				chapter.UpdateUI();
		}

		public override void OpenPanel(int newPanelIndex)
		{
			// Debug.Log(nameof(OpenPanel));

			if (toolTip != null)
				toolTip.gameObject.SetActive(false);
			base.OpenPanel(newPanelIndex);
		}

		private void OnEnable() => TimeManager.Instance.RegisterCallback(UpdateUI);
		private void OnDisable() => TimeManager.Instance.RemoveCallback(UpdateUI);
	}
}