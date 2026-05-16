using System;
using System.Collections.Generic;
using System.Linq;
using MessagePipe;
using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	public class UIManager : MonoBehaviour
	{
		public static UIManager Instance { get; private set; }

		public static bool TryGetExistingInstance(out UIManager mgr)
		{
			mgr = Instance;
			return mgr != null;
		}

		// Public Properties
		public List<IUIPanelGroup> PanelGroups { get; private set; } = new();
		public UINPC NPC { get; private set; }
		public TransitionView Transition { get; private set; }
		public UIChat Chat { get; private set; }
		public SpeechBubbleView SpeechBubble { get; private set; }
		public CutSceneModule CutSceneModule { get; private set; }
		[field: SerializeField] public Canvas BaseCanvas { get; private set; }

		private UIRoot uiRoot;
		private WindowManager windowManager;
		private IObjectResolver container;
		private IDisposable questCompletedSub;

		[Inject]
		public void Construct(UIRoot uiRoot, WindowManager windowManager, IObjectResolver container, ISubscriber<QuestCompletedEvent> questCompletedSubscriber)
		{
			this.uiRoot = uiRoot;
			this.windowManager = windowManager;
			this.container = container;
			questCompletedSub = questCompletedSubscriber.Subscribe(OnQuestCompleted);
		}
	
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

		private void Awake()
		{
			if (Instance != null && Instance != this) { Destroy(gameObject); return; }
			Instance = this;
		}

		private void OnQuestCompleted(QuestCompletedEvent evt)
		{
			if (evt.Type != QuestType.Achievement || evt.QuestSOID == -1)
			{
				return;
			}

			QuestSO questSO = SOHelper.GetQuestSO(evt.QuestSOID);
			Debug.Assert(questSO != null, $"OnQuestCompleted: QuestSO ID={evt.QuestSOID} not registered in SOManager");
			Popup(questSO);
		}

		private void OnDestroy()
		{
			questCompletedSub?.Dispose();

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

			if (Instance == this)
				Instance = null;
		}

		private void Start()
		{
			// container 의존 UI 생성/주입 — Awake(container null) 도 Construct(SceneLifetimeScope Build 중 →
			// 대량 container.Inject 재진입 = ValueFactory catastrophe) 도 아닌 Start.
			// Start = Construct 후 + Build 완료 후 → container valid, 재진입 0 (캐스케이드 d405bfde 검증 패턴, TASK-WM-078 2026-05-16).
			// Content UIs — 계층 전체 inject (UIDungeonRuntime / UIDungeonResult / UIDungeonEntrance 등)
			UIDungeon dungeonInst = Instantiate(dungeonPrefab, BaseCanvas.transform);
			foreach (MonoBehaviour mb in dungeonInst.GetComponentsInChildren<MonoBehaviour>(true))
				container.Inject(mb);

			adventurerGuild = Instantiate(adventurerGuildPrefab, BaseCanvas.transform);
			foreach (MonoBehaviour mb in adventurerGuild.GetComponentsInChildren<MonoBehaviour>(true))
				container.Inject(mb);
			adventurerGuild.gameObject.SetActive(false);

			// Common UIs
			CutSceneModule = FindAnyObjectByType<CutSceneModule>(FindObjectsInactive.Include);
			Chat = FindAnyObjectByType<UIChat>(FindObjectsInactive.Include);
			container.Inject(Chat);

			NPC = FindAnyObjectByType<UINPC>(FindObjectsInactive.Include);
			container.Inject(NPC);

			// 씬 한정 view 등록 — 글로벌 UIRoot 에 AddComponent
			GameObject uiRootGameObject = uiRoot.gameObject;
			inventoryView = uiRootGameObject.AddComponent<InventoryView>();
			container.Inject(inventoryView);
			hotbarView = uiRootGameObject.AddComponent<HotbarView>();
			container.Inject(hotbarView);
			buildingBarView = uiRootGameObject.AddComponent<BuildingBarView>();
			container.Inject(buildingBarView);
			questView = uiRootGameObject.AddComponent<QuestView>();
			container.Inject(questView);
			dollView = uiRootGameObject.AddComponent<DollView>();
			container.Inject(dollView);
			statusView = uiRootGameObject.AddComponent<StatusView>();
			container.Inject(statusView);
			popupView = uiRootGameObject.AddComponent<PopupView>();
			container.Inject(popupView);
			stagePopupView = uiRootGameObject.AddComponent<StagePopupView>();
			container.Inject(stagePopupView);
			floatingText = uiRootGameObject.AddComponent<FloatingTextView>();
			container.Inject(floatingText);
			SpeechBubble = uiRootGameObject.AddComponent<SpeechBubbleView>();
			container.Inject(SpeechBubble);
			// DialogueRunner — SceneLifetimeScope.RegisterComponentOnNewGameObject 추출. 여기서 AddComponent X.
			Transition = uiRootGameObject.AddComponent<TransitionView>();
			container.Inject(Transition);

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

		public void OnCancelInput()
		{
			SettingView settingView = uiRoot.SettingView;

			// SettingView가 열려있으면 먼저 닫음
			if (settingView.IsOpen)
			{
				settingView.Close();
				return;
			}

			// UI Toolkit WindowManager가 관리하는 윈도우가 열려있으면 그 쪽이 닫음 (중복 처리 방지)
			if (windowManager != null && windowManager.GetTopmostOpen() != null)
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