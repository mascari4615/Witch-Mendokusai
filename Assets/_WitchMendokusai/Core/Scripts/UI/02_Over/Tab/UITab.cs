using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace WitchMendokusai
{
	public class UITab : UIPanel
	{
		[field: Header("_" + nameof(UITab))]
		[SerializeField] private Transform menuButtonsParent;
		[SerializeField] private GameObject menuButtonPrefab;

		private readonly List<PanelType> targetPanels = new()
		{
			PanelType.MagicBook,
			PanelType.Quest,
			PanelType.Inventory,
			PanelType.Doll,
			PanelType.Setting,
			PanelType.Map,
		};

		protected int curButtonIndex = 0;
		protected List<UISlot> menuButtons;

		public override void Init()
		{
			menuButtons = menuButtonsParent.GetComponentsInChildren<UISlot>(true).ToList();
			for (int i = 0; i < menuButtons.Count; i++)
			{
				// GameObject buttonInstance = Instantiate(menuButtonPrefab, menuButtonsParent);
				// menuButtons.Add(buttonInstance.GetComponent<UISlot>());
				menuButtons[i].SetSlotIndex(i);
				menuButtons[i].Init();

				if (i < targetPanels.Count)
				{
					UIPanel panel = UIManager.Instance.PanelUIs[targetPanels[i]];
					menuButtons[i].SetSlot(panel.PanelIcon, panel.Name, string.Empty);
					menuButtons[i].SetClickAction((slot) => { UIManager.Instance.SetPanel(targetPanels[slot.Index]); });
					menuButtons[i].gameObject.SetActive(true);
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