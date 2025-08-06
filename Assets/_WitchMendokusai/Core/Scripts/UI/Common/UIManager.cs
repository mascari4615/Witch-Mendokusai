using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace WitchMendokusai
{
	public class UIManager : Singleton<UIManager>
	{
		public bool IsPanelOpen => OverlayUIs.Any(ui => ui.IsPanelOpen);

		[field: SerializeField] public CutSceneModule CutSceneModule { get; private set; }
		private UIFloatingText damage;
		private UIPopup popup;
		public UIChat Chat { get; private set; }
		private UIAdventurerGuild adventurerGuild;

		public UITransition Transition { get; private set; }
		private UIStagePopup stagePopup;
		public UIStatus Status { get; private set; }

		public List<IUIContentBase> OverlayUIs { get; private set; } = new();

		public UITab Tab { get; private set; }
		public UINPC NPC { get; private set; }

		protected override void Awake()
		{
			base.Awake();

			CutSceneModule = FindFirstObjectByType<CutSceneModule>(FindObjectsInactive.Include);
			damage = FindFirstObjectByType<UIFloatingText>(FindObjectsInactive.Include);
			popup = FindFirstObjectByType<UIPopup>(FindObjectsInactive.Include);
			Chat = FindFirstObjectByType<UIChat>(FindObjectsInactive.Include);
			adventurerGuild = FindFirstObjectByType<UIAdventurerGuild>(FindObjectsInactive.Include);

			Transition = FindFirstObjectByType<UITransition>(FindObjectsInactive.Include);
			stagePopup = FindFirstObjectByType<UIStagePopup>(FindObjectsInactive.Include);
			Status = FindFirstObjectByType<UIStatus>(FindObjectsInactive.Include);

			Tab = FindFirstObjectByType<UITab>(FindObjectsInactive.Include);
			NPC = FindFirstObjectByType<UINPC>(FindObjectsInactive.Include);
		}

		private void Start()
		{
			Status.Init();
			Status.gameObject.SetActive(false);

			RegisterOverlayUI(Tab);
			RegisterOverlayUI(NPC);
		}

		public void RegisterOverlayUI(IUIContentBase ui)
		{
			if (ui == null || OverlayUIs.Contains(ui))
				return;

			OverlayUIs.Add(ui);
		}

		public void PopDamage(DamageInfo damageInfo, Vector3 pos = default)
		{
			TextType textType = DamageUtil.DamageTypeToTextType(damageInfo.type);
			StartCoroutine(damage.AniTextUI(textType, damageInfo.damage.ToString(), pos));
		}

		public void PopText(string msg, TextType textType, Vector3 pos = default)
		{
			StartCoroutine(damage.AniTextUI(textType, msg, pos));
		}

		public void StagePopup(Stage stage)
		{
			stagePopup.Popup(stage);
		}

		public void Popup(DataSO dataSO)
		{
			popup.Popup(dataSO);
		}

		public void ToggleOverlayUI_Tab()
		{
			foreach (IUIContentBase ui in OverlayUIs)
			{
				if ((ui is UITab tab) && (tab.CurPanelType == TabPanelType.Setting))
					continue;

				if (ui.IsPanelOpen)
				{
					ui.ClosePanel();
					return;
				}
			}

			Tab.SetPanel(TabPanelType.TabMenu);
		}

		public void ToggleOverlayUI_Setting()
		{
			foreach (IUIContentBase ui in OverlayUIs)
			{
				if (ui.IsPanelOpen)
				{
					ui.ClosePanel();
					return;
				}
			}
		
			Tab.SetPanel(TabPanelType.Setting);
		}

		public void ToggleStatus()
		{
			Status.gameObject.SetActive(!Status.gameObject.activeSelf);

			if (Status.gameObject.activeSelf)
				Status.UpdateUI();
		}
	}
}