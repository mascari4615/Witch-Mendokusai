using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace WitchMendokusai
{
	public class UITabMenu : UIPanel
	{
		[field: Header("_" + nameof(UITabMenu))]
		// [SerializeField] private Transform menuButtonsParent;
		// [SerializeField] private GameObject menuButtonPrefab;

		protected int curButtonIndex = 0;

		[SerializeField] protected List<UISlot> menuButtons;

		protected override void OnInit()
		{
			// menuButtons = menuButtonsParent.GetComponentsInChildren<UISlot>(true).ToList();
			int maxButtonIndex = Mathf.Max(menuButtons.Count - 1, (int)TabPanelType.Count - 1);
			for (int i = 0; i < menuButtons.Count; i++)
			{
				// GameObject buttonInstance = Instantiate(menuButtonPrefab, menuButtonsParent);
				// menuButtons.Add(buttonInstance.GetComponent<UISlot>());
				menuButtons[i].SetSlotIndex(i);
				menuButtons[i].Init();
			}

			UITab tab = UIManager.Instance.Tab;

			// Navigation 설정 (먼저 Init이 되어야 함)
			for (int i = 0; i < menuButtons.Count; i++)
			{
				if (i < (int)TabPanelType.Count)
				{
					UIPanel panel = tab.Panels[(TabPanelType)i];
					menuButtons[i].SetSlot(panel.PanelIcon, panel.Name, string.Empty);
					menuButtons[i].SetClickAction((slot) => { tab.SetPanel((TabPanelType)slot.Index); });
					menuButtons[i].gameObject.SetActive(true);

					menuButtons[i].SetNavigation(
						new Navigation
						{
							mode = Navigation.Mode.Explicit,
							selectOnUp = null,
							selectOnDown = null,
							selectOnLeft = (i > 0) ? menuButtons[i - 1].Selectable : null,
							selectOnRight = (i < maxButtonIndex) ? menuButtons[i + 1].Selectable : null
						}
					);
				}
				else
				{
					menuButtons[i].gameObject.SetActive(false);
				}
			}
		}

		protected override void OnOpen()
		{
			menuButtons[0].Select();
		}

		public override void UpdateUI()
		{
		}
	}
}