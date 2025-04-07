using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using static WitchMendokusai.SOHelper;

namespace WitchMendokusai
{
	public class UIMagicBookPanel : UIPanels
	{
		[SerializeField] private Button tooltipCloseButton;
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

			tooltipCloseButton.onClick.AddListener(() => toolTip.gameObject.SetActive(false));
		}

		protected override void OnOpen()
		{
			base.OnOpen();
			TimeManager.Instance.RegisterCallback(UpdateUI);
		}

		protected override void OnClose()
		{
			base.OnClose();
			TimeManager.Instance.RemoveCallback(UpdateUI);
		}

		public override void OpenPanel(int newPanelIndex)
		{
			// Debug.Log(nameof(OpenPanel));

			if (toolTip != null)
				toolTip.gameObject.SetActive(false);
			base.OpenPanel(newPanelIndex);
		}
	}
}