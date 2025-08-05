using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// NPC Unit이 사용 중
	public enum PanelType
	{
		None = -1,

#region Tab
		Tab = 0,
		MagicBook = 11,
		Quest = 12,
		Inventory = 13,
		Doll = 14,
		Setting = 1,
		Map = 2,
#endregion

		NPC = 3,

		Shop = 5,
		DungeonEntrance = 6,
		Pot = 7,
		Anvil = 8,
		Furnace = 9,
		CraftingTable = 10,
	}

	public class UIManager : Singleton<UIManager>
	{
		public PanelType CurPanel { get; private set; }
		public Stack<PanelType> PanelStack { get; private set; } = new();
		public Dictionary<PanelType, UIPanel> PanelUIs { get; private set; } = new();

		[field: SerializeField] public CutSceneModule CutSceneModule { get; private set; }
		private UIFloatingText damage;
		private UIPopup popup;
		public UIChat Chat { get; private set; }
		private UIAdventurerGuild adventurerGuild;

		public UITransition Transition { get; private set; }
		private UIStagePopup stagePopup;
		public UIStatus Status { get; private set; }

		protected override void Awake()
		{
			base.Awake();

			CutSceneModule = FindFirstObjectByType<CutSceneModule>(FindObjectsInactive.Include);
			damage = FindFirstObjectByType<UIFloatingText>(FindObjectsInactive.Include); ;
			popup = FindFirstObjectByType<UIPopup>(FindObjectsInactive.Include);
			Chat = FindFirstObjectByType<UIChat>(FindObjectsInactive.Include);
			adventurerGuild = FindFirstObjectByType<UIAdventurerGuild>(FindObjectsInactive.Include);

			Transition = FindFirstObjectByType<UITransition>(FindObjectsInactive.Include);
			stagePopup = FindFirstObjectByType<UIStagePopup>(FindObjectsInactive.Include);
			Status = FindFirstObjectByType<UIStatus>(FindObjectsInactive.Include);

			// 패널 초기화
			{
				PanelUIs[PanelType.Tab] = FindFirstObjectByType<UITab>(FindObjectsInactive.Include);
				PanelUIs[PanelType.MagicBook] = FindFirstObjectByType<UIMagicBookPanel>(FindObjectsInactive.Include);
				PanelUIs[PanelType.Quest] = FindFirstObjectByType<UIQuestPanel>(FindObjectsInactive.Include);
				PanelUIs[PanelType.Inventory] = FindFirstObjectByType<UITabInventoryPanel>(FindObjectsInactive.Include);
				PanelUIs[PanelType.Doll] = FindFirstObjectByType<UIDollPanel>(FindObjectsInactive.Include);
				PanelUIs[PanelType.Setting] = FindFirstObjectByType<UISetting>(FindObjectsInactive.Include);
				// PanelUIs[PanelType.DungeonResult] = FindFirstObjectByType<UIDungeonResult>(FindObjectsInactive.Include);
				PanelUIs[PanelType.NPC] = FindFirstObjectByType<UINPC>(FindObjectsInactive.Include);
				PanelUIs[PanelType.Shop] = FindFirstObjectByType<UIShop>(FindObjectsInactive.Include);
				PanelUIs[PanelType.DungeonEntrance] = FindFirstObjectByType<UIDungeonEntrance>(FindObjectsInactive.Include);
				PanelUIs[PanelType.Pot] = FindFirstObjectByType<UIPot>(FindObjectsInactive.Include);
				PanelUIs[PanelType.Anvil] = FindFirstObjectByType<UIAnvil>(FindObjectsInactive.Include);
				PanelUIs[PanelType.Furnace] = FindFirstObjectByType<UIFurnace>(FindObjectsInactive.Include);
				PanelUIs[PanelType.CraftingTable] = FindFirstObjectByType<UICraftingTable>(FindObjectsInactive.Include);
				PanelUIs[PanelType.Map] = FindFirstObjectByType<UIMap>(FindObjectsInactive.Include);
			}
		}

		private void Start()
		{
			foreach (UIPanel uiPanel in PanelUIs.Values)
			{
				uiPanel.Init();
				uiPanel.SetActive(false);
			}

			SetPanel(PanelType.None);

			Status.Init();
			Status.gameObject.SetActive(false);
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
			if (CurPanel != PanelType.None)
			{
				if (CurPanel != PanelType.Setting)
					SetPanel(PanelType.None);
			}
			else
			{
				SetPanel(PanelType.Tab);
			}
		}

		public void ToggleOverlayUI_Setting()
		{
			if (CurPanel != PanelType.None)
			{
				SetPanel(PanelType.None);
			}
			else
			{
				SetPanel(PanelType.Setting);
			}
		}

		public void SetPanel(PanelType overlayUI, NPCObject npcObject = null)
		{
			if (CurPanel == overlayUI)
				return;

			if (CurPanel != PanelType.None)
				PanelUIs[CurPanel].SetActive(false);

			CurPanel = overlayUI;
			if (PanelUIs.TryGetValue(overlayUI, out UIPanel uiPanel))
			{
				uiPanel.SetNPC(npcObject);
				uiPanel.SetActive(true);
				uiPanel.UpdateUI();
			}
		}

		public void ToggleStatus()
		{
			Status.gameObject.SetActive(!Status.gameObject.activeSelf);

			if (Status.gameObject.activeSelf)
				Status.UpdateUI();
		}
	}
}