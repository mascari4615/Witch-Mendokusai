using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace WitchMendokusai
{
	public class UIMagicBookPanel : UIPanel
	{
		[SerializeField] private List<ChapterSO> chapterDatas;
		[SerializeField] private UIChapter chapterPrefab;
		[SerializeField] private Transform panelsParent;
		[SerializeField] private Transform panelSelectButtonsParent;
		[SerializeField] private GameObject panelSelectButtonPrefab;

		protected int curPanelIndex = 0;
		protected List<UIChapter> chapters = new();
		protected List<UISlot> chapterSelectButtons = new();

		[SerializeField] private Button tooltipCloseButton;
		private ToolTip toolTip;
		private UIQuestToolTip questToolTip;

		public override bool IsFullscreen => true;

		protected override void OnInit()
		{
			toolTip = GetComponentInChildren<ToolTip>(true);
			questToolTip = GetComponentInChildren<UIQuestToolTip>(true);
			questToolTip.Init();

			foreach (ChapterSO chapterSO in chapterDatas)
			{
				if (chapterSO == null)
				{
					Debug.LogWarning("[UIMagicBookPanel] chapterDatas에 null 항목이 있습니다. 인스펙터를 확인해주세요.", this);
					continue;
				}
				UIChapter chapter = Instantiate(chapterPrefab, panelsParent);
				chapter.Init();
				chapter.SetData(chapterSO);
				chapter.SetToolTip(toolTip, questToolTip);
				chapter.SetActive(false);
				chapters.Add(chapter);
			}

			chapterSelectButtons = new List<UISlot>();
			for (int i = 0; i < chapters.Count; i++)
			{
				int index = i;
				GameObject buttonInstance = Instantiate(panelSelectButtonPrefab, panelSelectButtonsParent);
				UISlot button = buttonInstance.GetComponent<UISlot>();
				button.SetSlotIndex(index);
				button.Init();
				button.SetClickAction((_) => OpenChapter(index));
				button.gameObject.SetActive(true);
				chapterSelectButtons.Add(button);
			}

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
			chapters[curPanelIndex].UpdateUI();

			for (int i = 0; i < chapterSelectButtons.Count; i++)
				chapterSelectButtons[i].UpdateUI();
		}

		public void OpenChapter(int newPanelIndex)
		{
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
