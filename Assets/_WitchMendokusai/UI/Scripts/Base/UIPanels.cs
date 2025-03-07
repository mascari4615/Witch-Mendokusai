using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace WitchMendokusai
{
	public abstract class UIPanels : UIPanel
	{
		[SerializeField] private Transform panelSelectButtonsParent;
		protected UISlot[] panelSelectButtons;
		[SerializeField] private Transform panelsParent;
		protected int curPanelIndex = 0;
		protected UIPanel[] panels;

		public override void Init()
		{
			panels = new UIPanel[panelsParent.childCount];
			for (int i = 0; i < panels.Length; i++)
			{
				GameObject p = panelsParent.GetChild(i).gameObject;
				p.SetActive(i == 0);

				panels[i] = p.GetComponent<UIPanel>();
				if (panels[i] == null)
				{
					panels = new UIPanel[0];
					break;
				}
				panels[i].Init();
			}

			panelSelectButtons = panelSelectButtonsParent.GetComponentsInChildren<UISlot>(true);
			for (int i = 0; i < panelSelectButtons.Length; i++)
			{
				if (i >= panels.Length)
				{
					panelSelectButtons[i].gameObject.SetActive(false);
					continue;
				}

				panelSelectButtons[i].SetSlotIndex(i);
				panelSelectButtons[i].SetSlot(panels[i].PanelIcon, panels[i].Name, string.Empty);
				panelSelectButtons[i].Init();
				panelSelectButtons[i].SetClickAction((slot) => { OpenPanel(slot.Index); });
			}
		}

		public override void OnOpen()
		{
			OpenPanel(curPanelIndex);
		}

		public override void UpdateUI()
		{
		}

		public virtual void OpenPanel(int newPanelIndex)
		{
			// Debug.Log($"{nameof(OpenTabMenu)}, {menuType}");
			curPanelIndex = newPanelIndex;

			if (panels.Length > 0)
			{
				for (int i = 0; i < panels.Length; i++)
					panels[i].gameObject.SetActive(i == curPanelIndex);
				panels[curPanelIndex].OnOpen();
				panels[curPanelIndex].UpdateUI();
			}
		}
	}
}