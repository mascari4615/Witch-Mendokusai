using System;
using System.Collections.Generic;
using System.Linq;
using MessagePipe;
using UnityEngine;
using VContainer;
using VContainer.Unity;

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
		// TASK-WM-118 I3 — NPC = *cross-wiring*(씬 sibling 참조), 콘텐츠 X.
		// 구: UIManager.Start 가 할당 → 다른 패널그룹(UINPC) Start 가 uiManager.NPC
		// 읽음 = 무보장 Start 순서 → uiManager.NPC null → UINPCMenu.OnInit:74 NRE
		// (마스킹 체인 :51→:47→:74 의 메커니즘). lazy 프로퍼티 = 접근 시 self-resolve
		// → Start 순서 무관(WM-115 R1/R5 eager→lazy 정합). 콘텐츠 생성은 Start 잔존
		// (WM-078 #5b ValueFactory 재앙 회피 제약 보존). R4 자식주입=InjectGameObject 불변.
		private UINPC _npc;
		public UINPC NPC => _npc != null
			? _npc
			: (_npc = FindAnyObjectByType<UINPC>(FindObjectsInactive.Include));
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

		// TASK-WM-115 R2 — Instantiate-before-Inject NRE 차단 seam.
		// prefab 을 비활성 토글한 채 Instantiate → 인스턴스 자식 OnEnable 미발화 →
		// container.Inject 로 deps 주입 완료 후 활성 → OnEnable 이 valid deps 로 실행.
		// (ObjectPoolManager.ObjectPool.CreateObject 와 동일 canonical 패턴.)
		private T InstantiateInjectedActive<T>(T prefab, Transform parent, bool activateAfter) where T : Component
		{
			bool prefabWasActive = prefab.gameObject.activeSelf;
			if (prefabWasActive)
				prefab.gameObject.SetActive(false);

			T inst = Instantiate(prefab, parent);

			if (prefabWasActive)
				prefab.gameObject.SetActive(true);

			foreach (MonoBehaviour mb in inst.GetComponentsInChildren<MonoBehaviour>(true))
				container.Inject(mb);

			if (activateAfter)
				inst.gameObject.SetActive(true);

			return inst;
		}

		private void Start()
		{
			// container 의존 UI 생성/주입 — Awake(container null) 도 Construct(SceneLifetimeScope Build 중 →
			// 대량 container.Inject 재진입 = ValueFactory catastrophe) 도 아닌 Start.
			// Start = Construct 후 + Build 완료 후 → container valid, 재진입 0 (캐스케이드 d405bfde 검증 패턴, TASK-WM-078 2026-05-16).
			// Content UIs — 계층 전체 inject (UIDungeonRuntime / UIDungeonResult / UIDungeonEntrance 등)
			// TASK-WM-115 R2 — 비활성 Instantiate → 자식 전체 Inject → 활성. active prefab 을
			// 그냥 Instantiate 하면 자식 OnEnable 이 container.Inject *전* 발화 → deps null NRE
			// (UIQuestGrid.OnEnable timeManager null). ObjectPoolManager.CreateObject 와 동일 검증 패턴.
			UIDungeon dungeonInst = InstantiateInjectedActive(dungeonPrefab, BaseCanvas.transform, activateAfter: true);

			adventurerGuild = InstantiateInjectedActive(adventurerGuildPrefab, BaseCanvas.transform, activateAfter: false);

			// Common UIs
			CutSceneModule = FindAnyObjectByType<CutSceneModule>(FindObjectsInactive.Include);
			Chat = FindAnyObjectByType<UIChat>(FindObjectsInactive.Include);
			container.Inject(Chat);

			// TASK-WM-118 I3 — NPC 할당 제거(이제 lazy 프로퍼티, 접근 시 self-resolve =
			// Start 순서 무관). 아래 NPC 접근이 lazy resolve 트리거 + 자식 주입.
			// TASK-WM-115 R4 — container.Inject(NPC) = UINPC 컴포넌트만 → 씬배치 자식 패널
			// ([Panel] NPC/DungeonEntrance·Shop·Pot·… 의 [Inject] Construct) 미주입 →
			// UIDungeonEntrance.dungeonManager null → EnterTheDungeon NRE. R3b 와 동일 root.
			// InjectGameObject = VContainer 표준 계층-재귀 (established 패턴 수렴).
			container.InjectGameObject(NPC.gameObject);

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