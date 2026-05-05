using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace WitchMendokusai
{
	public class UIManager : Singleton<UIManager>
	{
		// Public Properties
		public List<IUIPanelGroup> PanelGroups { get; private set; } = new();
		public UITab Tab { get; private set; }
		public UINPC NPC { get; private set; }
		public UITransition Transition { get; private set; }
		public UIChat Chat { get; private set; }
		public UISpeechBubble SpeechBubble { get; private set; }
		public UIStatus Status { get; private set; }
		public CutSceneModule CutSceneModule { get; private set; }
		[field: SerializeField] public Canvas BaseCanvas { get; private set; }
	
		[SerializeField] private UIDungeon dungeonPrefab = null;
		[SerializeField] private UIAdventurerGuild adventurerGuildPrefab = null;

		private UIFloatingText damage;
		private UIPopup popup;
		private UIAdventurerGuild adventurerGuild;
		private UIStagePopup stagePopup;

		// 씬(World) 한정 UI Toolkit View — 글로벌 UIRoot 에 AddComponent 후 OnDestroy 에서 정리.
		private InventoryView inventoryView;
		private HotbarView hotbarView;
		private BuildingBarView buildingBarView;

		public bool IsAnyPanelFullscreenOpen
		{
			get
			{
				PanelGroups.RemoveAll(ui => ui == null || ui.Equals(null));
				return PanelGroups.Any(ui => ui.IsPanelOpen && ui.TryGetCurPanel(out UIPanel panel) && panel != null && panel.Equals(null) == false && panel.IsFullscreen);
			}
		}

		protected override void Awake()
		{
			base.Awake();

			// Content UIs
			Instantiate(dungeonPrefab, BaseCanvas.transform);
			Instantiate(adventurerGuildPrefab, BaseCanvas.transform);

			// Common UIs
			CutSceneModule = FindAnyObjectByType<CutSceneModule>(FindObjectsInactive.Include);
			damage = FindAnyObjectByType<UIFloatingText>(FindObjectsInactive.Include);
			popup = FindAnyObjectByType<UIPopup>(FindObjectsInactive.Include);
			Chat = FindAnyObjectByType<UIChat>(FindObjectsInactive.Include);
			SpeechBubble = FindAnyObjectByType<UISpeechBubble>(FindObjectsInactive.Include);
			adventurerGuild = FindAnyObjectByType<UIAdventurerGuild>(FindObjectsInactive.Include);
			adventurerGuild.gameObject.SetActive(false);

			Transition = FindAnyObjectByType<UITransition>(FindObjectsInactive.Include);
			stagePopup = FindAnyObjectByType<UIStagePopup>(FindObjectsInactive.Include);
			Status = FindAnyObjectByType<UIStatus>(FindObjectsInactive.Include);

			Tab = FindAnyObjectByType<UITab>(FindObjectsInactive.Include);
			NPC = FindAnyObjectByType<UINPC>(FindObjectsInactive.Include);

			// 씬 한정 view 등록 — 글로벌 UIRoot 에 AddComponent
			GameObject uiRootGameObject = UIRoot.Instance.gameObject;
			inventoryView = uiRootGameObject.AddComponent<InventoryView>();
			hotbarView = uiRootGameObject.AddComponent<HotbarView>();
			buildingBarView = uiRootGameObject.AddComponent<BuildingBarView>();
		}

		protected override void OnDestroy()
		{
			if (inventoryView != null)
				Destroy(inventoryView);
			if (hotbarView != null)
				Destroy(hotbarView);
			if (buildingBarView != null)
				Destroy(buildingBarView);

			base.OnDestroy();
		}

		private void Start()
		{
			Status.Init();
			Status.gameObject.SetActive(false);

			RegisterOverlayUI(Tab);
			RegisterOverlayUI(NPC);
		}

		public void RegisterOverlayUI(IUIPanelGroup ui)
		{
			if (ui == null || PanelGroups.Contains(ui))
				return;

			PanelGroups.Add(ui);
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

		public void ToggleTabUI()
		{
			if (Tab.IsPanelOpen)
				Tab.ClosePanel();
			else if (IsAnyPanelFullscreenOpen == false)
				Tab.SetPanel(TabPanelType.TabMenu);
		}

		public void OnCancelInput()
		{
			SettingView settingView = UIRoot.Instance.SettingView;

			// SettingView가 열려있으면 먼저 닫음
			if (settingView.IsOpen)
			{
				settingView.Close();
				return;
			}

			// UI Toolkit WindowManager가 관리하는 윈도우가 열려있으면 그 쪽이 닫음 (중복 처리 방지)
			if (WindowManager.TryGetExistingInstance(out WindowManager windowManager) && windowManager.GetTopmostOpen() != null)
				return;

			PanelGroups.RemoveAll(ui => ui == null || ui.Equals(null));

			// 닫을 수 있는 UI 닫기
			foreach (IUIPanelGroup ui in PanelGroups)
			{
				if (ui.IsPanelOpen && ui.CanBeClosedByCancelInput)
				{
					ui.ClosePanel();
					return;
				}
			}

			settingView.Open();
		}

		public void ToggleStatus()
		{
			Status.gameObject.SetActive(!Status.gameObject.activeSelf);

			if (Status.gameObject.activeSelf)
				Status.UpdateUI();
		}
	}
}