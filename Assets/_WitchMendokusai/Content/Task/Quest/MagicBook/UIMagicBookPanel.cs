using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace WitchMendokusai
{
	public class UIMagicBookPanel : UIPanel
	{
		[SerializeField] private Transform panelsParent;
		[SerializeField] private Transform panelSelectButtonsParent;
		[SerializeField] private GameObject panelSelectButtonPrefab;
		protected int curPanelIndex = 0;
		protected List<UIChapter> chapters;
		protected List<UISlot> chapterSelectButtons;

		[SerializeField] private Button tooltipCloseButton;
		private ToolTip toolTip;
		private UIQuestToolTip questToolTip;

		public override bool IsFullscreen => true;

		protected override void OnInit()
		{
			// 비활성화 된 UIPanel도 찾기 위해 GetComponentsInChildren 사용, 부모가 panelsParent인 것만 필터링 - 2025.03.16 15:48
			chapters = panelsParent.GetComponentsInChildren<UIChapter>(true).Where(p => p.transform.parent == panelsParent).ToList();
			for (int i = 0; i < chapters.Count; i++)
			{
				chapters[i].Init();
				chapters[i].SetActive(false);
			}

			chapterSelectButtons = new List<UISlot>();
			for (int i = 0; i < chapters.Count; i++)
			{
				GameObject buttonInstance = Instantiate(panelSelectButtonPrefab, panelSelectButtonsParent);
				chapterSelectButtons.Add(buttonInstance.GetComponent<UISlot>());
				chapterSelectButtons[i].SetSlotIndex(i);
				chapterSelectButtons[i].Init();
				chapterSelectButtons[i].SetClickAction((slot) => { OpenChapter(slot.Index); });

				// chapterSelectButtons[i].SetSlot(chapters[i].PanelIcon, chapters[i].Name, string.Empty);
				chapterSelectButtons[i].gameObject.SetActive(true);
			}

			toolTip = GetComponentInChildren<ToolTip>(true);

			questToolTip = GetComponentInChildren<UIQuestToolTip>(true);
			questToolTip.Init();

			foreach (UIChapter chapter in chapters)
				chapter.SetToolTip(toolTip, questToolTip);

			tooltipCloseButton.onClick.AddListener(() => toolTip.gameObject.SetActive(false));
		}

		protected override void OnOpen()
		{
			OpenChapter(curPanelIndex);
			TimeManager.Instance.RegisterCallback(UpdateUI);
		}

		protected override void OnClose()
		{
			base.OnClose();
			TimeManager.Instance.RemoveCallback(UpdateUI);
		}

		public override void UpdateUI()
		{
			// foreach (UIPanel panel in panels)
			// 	panel.UpdateUI();
			chapters[curPanelIndex].UpdateUI();

			for (int i = 0; i < chapters.Count; i++)
				chapterSelectButtons[i].UpdateUI();
		}

		public void OpenChapter(int newPanelIndex)
		{
			// Debug.Log(nameof(OpenPanel));

			if (toolTip != null)
				toolTip.gameObject.SetActive(false);

			if (chapters == null || chapters.Count == 0)
				return;

			if (newPanelIndex < 0 || newPanelIndex >= chapters.Count)
				return;

			chapters[curPanelIndex].SetActive(false);
			curPanelIndex = newPanelIndex;
			chapters[curPanelIndex].SetActive(true);
			chapters[curPanelIndex].UpdateUI();
		}
	}
}