using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace WitchMendokusai
{
	public abstract class UIPanels : UIPanel
	{
		[SerializeField] private Transform panelsParent;
		[SerializeField] private Transform panelSelectButtonsParent;

		protected int curPanelIndex = 0;
		protected List<UIPanel> panels;
		protected List<UISlot> panelSelectButtons;

		public override void Init()
		{
			// 비활성화 된 UIPanel도 찾기 위해 GetComponentsInChildren 사용, 부모가 panelsParent인 것만 필터링 - 2025.03.16 15:48
			panels = panelsParent.GetComponentsInChildren<UIPanel>(true).Where(p => p.transform.parent == panelsParent).ToList();
			for (int i = 0; i < panels.Count; i++)
			{
				panels[i].Init();
				panels[i].SetActive(false);
			}

			panelSelectButtons = panelSelectButtonsParent.GetComponentsInChildren<UISlot>(true).ToList();
			for (int i = 0; i < panelSelectButtons.Count; i++)
			{
				panelSelectButtons[i].SetSlotIndex(i);
				panelSelectButtons[i].Init();
				panelSelectButtons[i].SetClickAction((slot) => { OpenPanel(slot.Index); });

				if (i < panels.Count)
				{
					panelSelectButtons[i].SetSlot(panels[i].PanelIcon, panels[i].Name, string.Empty);
					panelSelectButtons[i].gameObject.SetActive(true);
				}
				else
				{
					panelSelectButtons[i].gameObject.SetActive(false);
				}
			}
		}

		protected override void OnOpen()
		{
			// Debug.Log($"{name} {nameof(OnOpen)}");
			OpenPanel(curPanelIndex);
		}

		public override void UpdateUI()
		{
			// foreach (UIPanel panel in panels)
			// 	panel.UpdateUI();
			panels[curPanelIndex].UpdateUI();

			for (int i = 0; i < panels.Count; i++)
				panelSelectButtons[i].UpdateUI();
		}

		public virtual void OpenPanel(int newPanelIndex)
		{
			// Debug.Log($"{nameof(OpenTabMenu)}, {menuType}");

			if (panels == null || panels.Count == 0)
				return;

			if (newPanelIndex < 0 || newPanelIndex >= panels.Count)
				return;

			panels[curPanelIndex].SetActive(false);
			curPanelIndex = newPanelIndex;
			panels[curPanelIndex].SetActive(true);
			panels[curPanelIndex].UpdateUI();
		}
	}
}