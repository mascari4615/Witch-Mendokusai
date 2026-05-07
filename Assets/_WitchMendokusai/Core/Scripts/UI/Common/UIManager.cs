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
		public TransitionView Transition { get; private set; }
		public UIChat Chat { get; private set; }
		public SpeechBubbleView SpeechBubble { get; private set; }
		public CutSceneModule CutSceneModule { get; private set; }
		[field: SerializeField] public Canvas BaseCanvas { get; private set; }
	
		[SerializeField] private UIDungeon dungeonPrefab = null;
		[SerializeField] private UIAdventurerGuild adventurerGuildPrefab = null;

		private FloatingTextView floatingText;
		private UIAdventurerGuild adventurerGuild;

		// 씬(World) 한정 UI Toolkit View — 글로벌 UIRoot 에 AddComponent 후 OnDestroy 에서 정리.
		private InventoryView inventoryView;
		private HotbarView hotbarView;
		private BuildingBarView buildingBarView;
		private QuestView questView;
		private DollView dollView;
		private StatusView statusView;
		private PopupView popupView;
		private StagePopupView stagePopupView;

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
			Chat = FindAnyObjectByType<UIChat>(FindObjectsInactive.Include);
			adventurerGuild = FindAnyObjectByType<UIAdventurerGuild>(FindObjectsInactive.Include);
			adventurerGuild.gameObject.SetActive(false);

			Tab = FindAnyObjectByType<UITab>(FindObjectsInactive.Include);
			NPC = FindAnyObjectByType<UINPC>(FindObjectsInactive.Include);

			// 씬 한정 view 등록 — 글로벌 UIRoot 에 AddComponent
			GameObject uiRootGameObject = UIRoot.Instance.gameObject;
			inventoryView = uiRootGameObject.AddComponent<InventoryView>();
			hotbarView = uiRootGameObject.AddComponent<HotbarView>();
			buildingBarView = uiRootGameObject.AddComponent<BuildingBarView>();
			questView = uiRootGameObject.AddComponent<QuestView>();
			dollView = uiRootGameObject.AddComponent<DollView>();
			statusView = uiRootGameObject.AddComponent<StatusView>();
			popupView = uiRootGameObject.AddComponent<PopupView>();
			stagePopupView = uiRootGameObject.AddComponent<StagePopupView>();
			floatingText = uiRootGameObject.AddComponent<FloatingTextView>();
			SpeechBubble = uiRootGameObject.AddComponent<SpeechBubbleView>();
			Transition = uiRootGameObject.AddComponent<TransitionView>();
		}

		protected override void OnDestroy()
		{
			if (inventoryView != null)
				Destroy(inventoryView);
			if (hotbarView != null)
				Destroy(hotbarView);
			if (buildingBarView != null)
				Destroy(buildingBarView);
			if (questView != null)
				Destroy(questView);
			if (dollView != null)
				Destroy(dollView);
			if (statusView != null)
				Destroy(statusView);
			if (popupView != null)
				Destroy(popupView);
			if (stagePopupView != null)
				Destroy(stagePopupView);
			if (floatingText != null)
				Destroy(floatingText);
			if (SpeechBubble != null)
				Destroy(SpeechBubble);
			if (Transition != null)
				Destroy(Transition);

			base.OnDestroy();
		}

		private void Start()
		{
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
			StartCoroutine(floatingText.AniTextUI(textType, damageInfo.damage.ToString(), pos));
		}

		public void PopText(string msg, TextType textType, Vector3 pos = default)
		{
			StartCoroutine(floatingText.AniTextUI(textType, msg, pos));
		}

		public void StagePopup(Stage stage)
		{
			stagePopupView.Popup(stage);
		}

		public void Popup(DataSO dataSO)
		{
			popupView.Popup(dataSO);
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

	}
}