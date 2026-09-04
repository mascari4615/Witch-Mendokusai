using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using WitchMendokusai.DomainSDK.Contracts;
using WitchMendokusai.DomainSDK.Idle;
using WitchMendokusai.Idle.UI;
using WitchMendokusai.Presentation;
using BigNumberText = WitchMendokusai.Numerics.BigNumberText;

namespace WitchMendokusai.Idle
{
	/// <summary>
	/// V2 작전 화면. 정본 <c>memo/wm/design/idle/layout.md</c> (사용자 확정 2026-08-30).
	///
	/// ★ 화면은 둘. 왼쪽 <b>전투 창</b>(1200)과 오른쪽 <b>관리 열</b>(720).
	///   HUD 는 전부 전투 창 안: 작전 코드, 웨이브, 스테퍼, 반복, 재화 3, 배속, 로그, 손패, 코스트, AUTO.
	///   관리 열은 탭 7 + 판 하나. 한 번에 한 판.
	/// ★ 분할 토글: 전투 풀화면이면 관리 열이 오른쪽에서 덮는 서랍, 탭은 우하.
	/// ★ 상점, 연구소 탭은 전투 창의 3D 씬 자리를 그 탭의 씬으로 바꾼다 (지금은 자리 표시만).
	/// ★ 규칙은 한 줄도 없다. 사진을 그리고 의도를 보낸다. 판정은 전부 코어.
	/// ★ 이름(골드, 뽑기, 환생 조각, 탭 이름)은 전부 임시 (layout.md §6).
	/// </summary>
	[ExecuteAlways]
	[RequireComponent(typeof(PanelRenderer))]
	public sealed class BattleScreen : MonoBehaviour, IGameView<IdleSnapshot>
	{
		[Header("수치. 비워 두면 코드 기본값")]
		[SerializeField] private TuningSO tuningAsset;
		[SerializeField] private HeroCatalogSO heroCatalogAsset;
		[SerializeField] private UIContentSO uiContentAsset;
		[SerializeField] private GearPresentationSO gearPresentationAsset;
		[SerializeField] private RuntimeSettingsSO runtimeSettingsAsset;

		[Header("UI Builder 정본과 반복 템플릿")]
		[SerializeField] private IdleViewAssetsSO viewAssets;

		[Header("무대. 씬이 꽂아 준다")]
		[SerializeField] private BattleStage stage;

		private VisualTreeAsset screenAsset => viewAssets.Screen;
		private VisualTreeAsset bagCellAsset => viewAssets.BagCell;
		private VisualTreeAsset forgeKindAsset => viewAssets.ForgeKind;
		private VisualTreeAsset cardAsset => viewAssets.Card;
		private VisualTreeAsset queueChipAsset => viewAssets.QueueChip;
		private VisualTreeAsset choiceCardAsset => viewAssets.ChoiceCard;
		private VisualTreeAsset waveDotAsset => viewAssets.WaveDot;
		private VisualTreeAsset producerRowAsset => viewAssets.ProducerRow;
		private VisualTreeAsset rowButtonAsset => viewAssets.RowButton;
		private VisualTreeAsset rowLabelAsset => viewAssets.RowLabel;

		private IdleSession session;
		private float untilUiRefresh;
		private SessionPersistence persistence;
		private ProceduralSfx sound;
		private bool clickSoundHooked;
		private ScreenRootController screenRootController;
		private VisualElement panelRoot;

		// 에디트 모드 미리보기 (사용자 2026-08-30: UI 수정은 Play 없이). 저장 읽기와 쓰기 없음. 임시 판 위 시뮬만
		/// <summary>화면 에셋이 없어 못 짓는 판. 켜 두되 아무것도 안 그린다</summary>
		private bool broken;

		private bool preview;

#if UNITY_EDITOR
		// 미리보기 시계와 첫 틱 표식. 에디터 전용 경로에서만 읽으므로 여기 밖에 두면
		// 플레이어 빌드에서 CS0414(쓰기만 하고 안 읽음)로 죽는다 (실측 2026-09-01, csc.rsp 가 -warnaserror+)
		private double previewClock;
		private bool previewTicked;
#endif

		/// <summary>미리보기 시뮬 진행 여부. 기본은 첫 틱 뒤 정지 (정적 장면). Dev Panel 이 켠다</summary>
		public static bool PreviewRunning { get; set; }

		// 짓기가 끝나야 그린다. 짓는 도중 Render 가 돌면 아직 없는 조각(맵 팝업)에서 죽는다 (실측 2026-08-30)
		private bool built;

		// ── 탭 ────────────────────────────────────────────────────────────
		private enum Tab { Doll = 0, Item = 1, Codex = 2, Shop = 3, Lab = 4, Dungeon = 5, Invest = 6 }

		// ── 전투 창 ───────────────────────────────────────────────────────
		private VisualElement battle;
		private BattleHudController battleHudController;

		private Label logLabel;
		private Label noteLabel;
		private float noteLeft;

		private CardHandController cardHandController;
		private Button[] dungeonRows;

		// ── 관리 열 ───────────────────────────────────────────────────────
		/// <summary>UI 뿌리. 폭을 재서 무대 카메라를 맞춘다</summary>
		private VisualElement root;

		private SidePanelController sidePanelController;
		private Tab openTab = Tab.Doll;
		private bool split = true;
		private bool sideOpen;

		// 인형
		private readonly List<Button> partyButtons = new List<Button>();
		private HeroSelectionController heroSelectionController;

		/// <summary>장비를 볼 인형의 편성 자리 (2026-08-31 인형별 장비). 찬 편성 칸을 누르면 바뀐다</summary>
		private int gearSeat;

		/// <summary>그 자리의 인형 번호. 빈 자리면 -1</summary>
		private int gearHeroId => session != null && gearSeat >= 0 && gearSeat < session.State.Party.Length
			? session.State.Party[gearSeat]
			: -1;

		/// <summary>한 인형의 장비 넷. 매 프레임 새 배열을 안 만들려고 들고 있는다</summary>
		private readonly IdleItem[] gearOfHero = new IdleItem[IdleGear.SLOT_COUNT];
		private Label dollName;
		private Label[] statNames;
		private Label[] statValues;
		private Label[] statLevels;
		private Button[,] statButtons;
		private Label statFeedback;
		private int statFeedbackVersion;
		private readonly List<Button> wornCells = new List<Button>();

		// 장비 고르기 팝업 (사용자 2026-08-31). 인형이 여럿이라 가방에서 바로 장착하면 대상이 불명
		private GearSelectionController gearSelectionController;

		// 아이템. 서브탭 가방 / 공방 (layout.md §3)
		private int itemSub;
		private readonly Button[] itemSubButtons = new Button[2];
		private VisualElement bagView;
		private VisualElement forgeView;
		private Label gearSummary;
		private VisualElement bagGrid;
		private readonly List<Button> bagCells = new List<Button>();
		private Button bulkMergeButton;

		// 공방. 울티마식 3×3, 같은 부위 같은 단계 9개 → 한 단계 위 (비용 없음. 사용자 2026-08-30)
		private IdleItemSlot forgeSlot;
		private int forgeTier;
		private readonly List<Label> forgeCells = new List<Label>();
		private Label forgeResult;
		private Label forgeTitle;
		private Button forgeButton;
		private VisualElement forgeKinds;
		private readonly List<Button> forgeKindButtons = new List<Button>();
		private readonly List<int> forgeKindKeys = new List<int>();

		// 감정(잠재)은 알파 뒤. 자리만, 숨김
		private Label appraiseCap;
		private VisualElement appraiseRows;
		private readonly List<Button> appraiseButtons = new List<Button>();

		// 도감
		private Label codexLabel;
		private VisualElement codexRows;
		private readonly List<Label> codexLabels = new List<Label>();

		// 상점
		private Button pullButton;
		private Label pullOdds;
		private Button bagButton;
		private Label bagNote;

		// 연구소
		private Label prestigeSummary;
		private Button prestigeButton;

		// 투자
		private Label baseSummary;
		private readonly List<Button> producerButtons = new List<Button>();

		// 툴팁
		private Label tooltip;
		private PointerTooltipController tooltipController;

		// 팝업
		private VisualElement mapPopup;
		private VisualElement mapRows;
		private readonly List<Button> mapButtons = new List<Button>();
		private VisualElement goldPopup;
		private Label goldAmount;
		private Label goldIncome;
		private VisualElement settingsPopup;
		private readonly List<Button> speedButtons = new List<Button>();
		private ModalController modalController;
		private HeroVisualPresenter heroVisualPresenter;
		private GearVisualPresenter gearVisualPresenter;

		public PresentationKind Kind => PresentationKind.UIOnly;

		private void OnEnable()
		{
			if (tuningAsset == null)
			{
				Debug.LogWarning("[Idle] 수치 에셋이 안 꽂혀 있다. 코드 기본값으로 돈다.");
			}

			// UXML 이 정본 (사용자 2026-08-30). 없으면 조용한 빈 화면 대신 여기서 정지
			//
			// ⚠ enabled 를 끄면 그 상태가 씬에 저장됨 (실측 2026-08-31). 에셋을 채워도 복구 불가
			//   플래그만 세우고 컴포넌트는 켠 채로
			if (MissingAsset(out string what))
			{
				Debug.LogError("[Idle] 화면 에셋이 없다: " + what + ". Dev Panel 의 씬 짓기로 다시 꽂아라");
				broken = true;
				return;
			}

			broken = false;
			IdleHeroes.Configure(heroCatalogAsset.ToDomain());
			heroVisualPresenter = new HeroVisualPresenter(heroCatalogAsset);
			gearVisualPresenter = new GearVisualPresenter(gearPresentationAsset);

			// 배치 빌드에서는 아무것도 안 세운다 (실측 2026-09-01: 20회 연속 빌드 실패).
			// 씬 검사(IdleSceneBuilder.Verify)가 씬을 열면 [ExecuteAlways] 때문에 여기가 돌고,
			// -nographics 배치에는 카메라도 패널도 없음. 빌드가 Unknown 으로 사망
			if (Application.isBatchMode)
			{
				return;
			}

			screenRootController = new ScreenRootController(
				GetComponent<PanelRenderer>(),
				screenAsset,
				OnPanelReloaded);
			screenRootController.Enable();

			IdleTuning tuning = tuningAsset != null ? tuningAsset.ToTuning() : new IdleTuning();
			preview = Application.isPlaying == false;
			persistence = null;

			IdleState state;
			IdleAwayReport away = default;

			if (preview)
			{
#if UNITY_EDITOR
				previewTicked = false;
#endif
				state = runtimeSettingsAsset.CreatePreviewState(tuning);
				session = new IdleSession(tuning, state);
#if UNITY_EDITOR
				UnityEditor.EditorApplication.update -= PreviewTick;
				UnityEditor.EditorApplication.update += PreviewTick;
				previewClock = UnityEditor.EditorApplication.timeSinceStartup;
#endif
			}
			else
			{
				persistence = new SessionPersistence(runtimeSettingsAsset.SaveIntervalSeconds);
				state = persistence.LoadState();
				session = new IdleSession(tuning, state);
				away = persistence.CatchUp(session);
				EnsureSound();
			}

			if (stage != null)
			{
				stage.Build();
			}
			else
			{
				Debug.LogWarning("[Idle] 무대가 안 꽂혀 있다. HUD 만 뜬다. 씬 빌더로 다시 지어라.");
			}

			split = PlayerPrefs.GetInt("idle.split", 1) == 1;

			BuildAll(away);
			Render(session.Capture());
		}

#if UNITY_EDITOR
		/// <summary>에디트 모드의 한 틱. 시뮬을 밟고 모든 뷰를 다시 그린다</summary>
		private void PreviewTick()
		{
			if (this == null || preview == false)
			{
				UnityEditor.EditorApplication.update -= PreviewTick;
				return;
			}

			double now = UnityEditor.EditorApplication.timeSinceStartup;
			float delta = Mathf.Min(0.25f, (float)(now - previewClock));
			previewClock = now;

			// 정적 장면이 기본. 첫 틱만 밟아 전장을 세우고 멈춘다 (사용자: UI 와 정적 3D 확인용)
			if (previewTicked && PreviewRunning == false)
			{
				return;
			}

			previewTicked = true;
			Tick(delta);
			UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
		}
#endif

		/// <summary>안 꽂힌 화면 에셋 이름. 전부 있으면 거짓</summary>
		private bool MissingAsset(out string what)
		{
			what = string.Empty;

			if (heroCatalogAsset == null) { what = "heroCatalogAsset"; }
			else if (uiContentAsset == null) { what = "uiContentAsset"; }
			else if (gearPresentationAsset == null) { what = "gearPresentationAsset"; }
			else if (runtimeSettingsAsset == null) { what = "runtimeSettingsAsset"; }
			else if (viewAssets == null) { what = "viewAssets"; }
			else if (viewAssets.TryValidate(out string viewError) == false)
			{
				what = "viewAssets: " + viewError;
			}
			else if (uiContentAsset.TryValidate(System.Enum.GetValues(typeof(Tab)).Length, out string uiError) == false)
			{
				what = "uiContentAsset: " + uiError;
			}
			else if (gearPresentationAsset.TryValidate(out string gearError) == false)
			{
				what = "gearPresentationAsset: " + gearError;
			}
			else if (runtimeSettingsAsset.TryValidate(out string runtimeError) == false)
			{
				what = "runtimeSettingsAsset: " + runtimeError;
			}

			return what.Length > 0;
		}

		private void OnDisable()
		{
#if UNITY_EDITOR
			UnityEditor.EditorApplication.update -= PreviewTick;
#endif
			screenRootController?.Dispose();
			screenRootController = null;
			panelRoot = null;
			clickSoundHooked = false;
			modalController?.Dispose();
			if (preview)
			{
				session = null;
				return;
			}

			persistence?.Close(session);
			session = null;
		}

		private void OnApplicationPause(bool paused)
		{
			if (paused)
			{
				WriteDown();
			}
		}

		private void OnApplicationQuit()
		{
			WriteDown();
		}

		private void Update()
		{
			// 미리보기는 에디터 틱(PreviewTick) 담당. 에디트 모드의 플레이어 루프는 Update 를 안 부름 (실측 2026-08-30)
			if (preview)
			{
				return;
			}

			if (built == false)
			{
				BuildAll(default);
				if (built == false)
				{
					return;
				}
			}

			Tick(Time.unscaledDeltaTime);
		}

		private void Tick(float delta)
		{
			if (session == null || broken)
			{
				return;
			}

			// 보고 있는 동안은 위험 진행. 적의 공격, 쓰러짐, 부활
			session.AdvanceLive(delta);
			session.AdvanceSurge(delta);
			IdleSnapshot snapshot = session.Capture();

			if (stage != null)
			{
				stage.Render(snapshot, delta);
			}

			if (noteLeft > 0f)
			{
				noteLeft -= delta;
				noteLabel.style.opacity = noteLeft < 1f ? noteLeft : 1f;
				if (noteLeft <= 0f)
				{
					noteLabel.text = string.Empty;
				}
			}

			untilUiRefresh -= delta;
			if (untilUiRefresh <= 0f)
			{
				untilUiRefresh = runtimeSettingsAsset.UIRefreshSeconds;
				Render(snapshot);
			}

			if (preview)
			{
				return;
			}

			persistence?.Tick(delta, session);
		}

		private void WriteDown()
		{
			persistence?.Save(session);
		}

		// ── 짓기 ──────────────────────────────────────────────────────────

		private void BuildAll(IdleAwayReport away)
		{
			// PanelRenderer OnEnable 전 호출 방어. 이전 판 완료 상태로 빈 라벨에 닿지 않게
			built = false;
			modalController?.Dispose();
			ResetViewCollections();
			if (panelRoot == null)
			{
				return;
			}

			this.root = panelRoot;
			VisualElement root = this.root;
			modalController = new ModalController(root, runtimeSettingsAsset.ModalRepaintMilliseconds);
			if (clickSoundHooked == false)
			{
				root.RegisterCallback<ClickEvent>(OnButtonClicked);
				clickSoundHooked = true;
			}

			// 창 크기가 바뀌면 무대 폭도 다시 (모바일 회전, PC 창 조절)
			root.RegisterCallback<GeometryChangedEvent>(_ =>
			{
				AimCamera();
				ApplySafeArea();
			});
			VisualElement shell = root.Q<VisualElement>("shell");
			tooltip = root.Q<Label>("tooltip");
			tooltipController = new PointerTooltipController(tooltip, runtimeSettingsAsset.TooltipTouchMilliseconds);

			BuildBattle(shell);
			BuildSide(shell);
			BuildMapPopup();
			BuildGearPopup();
			BuildHeroPopup();
			BuildGoldPopup();
			BuildSettingsPopup();
			BuildAwayPopup(away);

			if (stage != null)
			{
				stage.SetFloatingTextRoot(battle);
			}

			built = true;
			ApplySplit();

		}

		private void OnPanelReloaded(VisualElement rootElement)
		{
			if (panelRoot != rootElement)
			{
				clickSoundHooked = false;
			}

			panelRoot = rootElement;
			if (session == null || broken)
			{
				return;
			}

			BuildAll(default);
			Render(session.Capture());
		}

		private void ResetViewCollections()
		{
			battleHudController = null;
			cardHandController = null;
			sidePanelController = null;
			partyButtons.Clear();
			wornCells.Clear();
			heroSelectionController = null;
			gearSelectionController = null;
			bagCells.Clear();
			forgeCells.Clear();
			forgeKindButtons.Clear();
			forgeKindKeys.Clear();
			appraiseButtons.Clear();
			codexLabels.Clear();
			producerButtons.Clear();
			mapButtons.Clear();
			speedButtons.Clear();
		}

		private void BuildBattle(VisualElement shell)
		{
			battle = shell.Q<VisualElement>("battle");

			// 빈 곳 누르기는 응원 한 대. 무대 그 자체가 큰 버튼
			battle.RegisterCallback<PointerDownEvent>(OnTapped);

			BindBattleHud();
			BuildBattleExtras();
		}

		private void BuildBattleExtras()
		{
			Button wipe = battle.Q<Button>("wipe-button");
			wipe.style.display = Application.isEditor || Debug.isDebugBuild ? DisplayStyle.Flex : DisplayStyle.None;
			wipe.clicked += WipeAndRestart;
			cardHandController.BringAimToFront();
			wipe.BringToFront();
		}

		private void BindBattleHud()
		{
			battleHudController = new BattleHudController(
				battle,
				waveDotAsset,
				uiContentAsset,
				() => OpenTab(Tab.Doll),
				ToggleMap,
				StepStage,
				ToggleHold,
				OpenGoldPopup,
				ToggleSplit,
				OpenSettingsPopup,
				ToggleAutoCast);
			cardHandController = new CardHandController(
				battle,
				cardAsset,
				queueChipAsset,
				uiContentAsset,
				CanAimCard,
				Cast,
				PickFoe,
				CastVolleyAt);
		}

		private bool CanAimCard(int handIndex)
		{
			return session != null && handIndex >= 0 && handIndex < IdleCards.HAND_SIZE
				&& IdleCards.HandAt(session.State, handIndex) == IdleCardKind.Volley
				&& IdleCards.CanCast(session.State, session.Tuning, IdleCardKind.Volley);
		}

		private long? PickFoe(Vector2 position)
		{
			return stage != null && stage.TryPickFoe(position, out long foeIndex) ? foeIndex : (long?)null;
		}

		private bool CastVolleyAt(int handIndex, long foeIndex)
		{
			if (session.TryCastCardAt(handIndex, foeIndex, out IdleCardResult result) == false)
			{
				return false;
			}
			stage.OnVolley(foeIndex);
			SayOnce("일제 사격. 표시한 범위에 집중 포화.", runtimeSettingsAsset.NoteSeconds);
			WriteDown();
			Render(session.Capture());
			return true;
		}

		private void BuildSide(VisualElement shell)
		{
			sidePanelController = new SidePanelController(
				shell, battle, uiContentAsset, index => OpenTab((Tab)index), CloseSide);
			BuildDollPage();
			BuildItemPage();
			BuildCodexPage();
			BuildShopPage();
			BuildLabPage();
			BuildDungeonPage();
			BuildInvestPage();
		}

		private VisualElement UsePage(Tab tab, string hostName)
		{
			return sidePanelController.BindPage((int)tab, hostName, root);
		}

		private VisualElement UsePopup(string hostName)
		{
			VisualElement host = root.Q<VisualElement>(hostName);
			VisualElement popup = host.Q<VisualElement>("popup");
			popup.style.display = DisplayStyle.None;
			return popup;
		}

		/// <summary>인형 탭 (layout.md §3). 모양은 UXML, 여기는 값과 클릭만</summary>
		private void BuildDollPage()
		{
			BindDollPage(UsePage(Tab.Doll, "doll-page-host"));
		}

		/// <summary>인형 탭을 UXML 에서. 모양은 에셋, 코드는 이름으로 찾아 값과 클릭만</summary>
		private void BindDollPage(VisualElement page)
		{
			statNames = new Label[uiContentAsset.StatCount];
			statValues = new Label[uiContentAsset.StatCount];
			statLevels = new Label[uiContentAsset.StatCount];
			statButtons = new Button[uiContentAsset.StatCount, uiContentAsset.StatUpgradeAmountCount];
			for (int slot = 0; slot < IdleHeroes.PARTY_SLOTS; slot++)
			{
				int captured = slot;
				Button seat = page.Q<Button>("seat-" + slot);
				seat.clicked += () => BeginSeat(captured);
				partyButtons.Add(seat);
			}

			dollName = page.Q<Label>("doll-name");
			statFeedback = page.Q<Label>("stat-feedback");
			statFeedback.style.visibility = Visibility.Hidden;
			for (int stat = 0; stat < uiContentAsset.StatCount; stat++)
			{
				int capturedStat = stat;
				statNames[stat] = page.Q<Label>("stat-name-" + stat);
				statValues[stat] = page.Q<Label>("stat-value-" + stat);
				statLevels[stat] = page.Q<Label>("stat-level-" + stat);
				statNames[stat].text = uiContentAsset.StatName(stat);

				for (int amount = 0; amount < uiContentAsset.StatUpgradeAmountCount; amount++)
				{
					int capturedAmount = uiContentAsset.StatUpgradeAmount(amount);
					Button button = page.Q<Button>("stat-" + stat + "-x" + capturedAmount);
					button.clicked += () => Raise((IdleUpgradeKind)capturedStat, capturedAmount);
					HookTooltip(button, () => StatTip((IdleUpgradeKind)capturedStat, capturedAmount));
					statButtons[stat, amount] = button;
				}
			}

			for (int slot = 0; slot < uiContentAsset.GearSlotCount; slot++)
			{
				int captured = slot;
				Button cell = page.Q<Button>("worn-" + slot);
				cell.clicked += () => OpenGear(captured);
				HookTooltip(cell, () => WornTip(captured));
				wornCells.Add(cell);
			}

		}

		/// <summary>아이템 탭 (layout.md §3). 가방과 공방. 모양은 UXML</summary>
		private void BuildItemPage()
		{
			BindItemPage(UsePage(Tab.Item, "item-page-host"));
		}

		/// <summary>아이템 탭을 UXML 에서. 가방과 공방의 수량만 코어 사진으로 채운다</summary>
		private void BindItemPage(VisualElement page)
		{
			itemSubButtons[0] = page.Q<Button>("bag-subtab");
			itemSubButtons[0].clicked += () => OpenItemSub(0);
			itemSubButtons[1] = page.Q<Button>("forge-subtab");
			itemSubButtons[1].clicked += () => OpenItemSub(1);

			bagView = page.Q<VisualElement>("bag-view");
			gearSummary = page.Q<Label>("gear-summary");
			bagGrid = page.Q<VisualElement>("bag-grid");
			for (int index = 0; index < 40; index++)
			{
				int captured = index;
				// 가방에서 바로 장착하지 않는다. 장비는 인형 탭의 칸에서 고른다 (사용자 2026-08-31)
				Button cell = AddBagCell(bagGrid, null);
				HookTooltip(cell, () => BagTip(captured));
				bagCells.Add(cell);
			}
			bulkMergeButton = page.Q<Button>("bulk-merge-button");
			bulkMergeButton.clicked += MergeAll;

			forgeView = page.Q<VisualElement>("forge-view");
			forgeKinds = page.Q<VisualElement>("forge-kinds");
			for (int index = 0; index < 9; index++)
			{
				forgeCells.Add(page.Q<Label>("forge-cell-" + index));
			}
			forgeResult = page.Q<Label>("forge-result");
			forgeTitle = page.Q<Label>("forge-title");
			forgeButton = page.Q<Button>("forge-button");
			forgeButton.clicked += MergeForge;

			appraiseCap = page.Q<Label>("appraise-cap");
			appraiseCap.style.display = DisplayStyle.None;
			appraiseRows = page.Q<VisualElement>("appraise-rows");
			appraiseRows.style.display = DisplayStyle.None;

			OpenItemSub(0);
		}

		private Button AddBagCell(VisualElement parent, System.Action clicked)
		{
			TemplateContainer tree = bagCellAsset.Instantiate();
			Button cell = tree.Q<Button>("bag-cell");
			cell.RemoveFromHierarchy();
			cell.text = string.Empty;
			cell.clicked += clicked;
			parent.Add(cell);
			return cell;
		}

		private Button AddForgeKind(VisualElement parent, System.Action clicked)
		{
			TemplateContainer tree = forgeKindAsset.Instantiate();
			Button kind = tree.Q<Button>("forge-kind");
			kind.RemoveFromHierarchy();
			kind.clicked += clicked;
			parent.Add(kind);
			return kind;
		}

		private void OpenItemSub(int which)
		{
			itemSub = which;
			bagView.style.display = which == 0 ? DisplayStyle.Flex : DisplayStyle.None;
			forgeView.style.display = which == 1 ? DisplayStyle.Flex : DisplayStyle.None;

			for (int index = 0; index < itemSubButtons.Length; index++)
			{
				itemSubButtons[index].EnableInClassList("idle-subtab--on", index == which);
			}

			if (built)
			{
				Render(session.Capture());
			}
		}

		private void BuildCodexPage()
		{
			VisualElement page = UsePage(Tab.Codex, "codex-page-host");
			codexLabel = page.Q<Label>("codex-label");
			codexRows = page.Q<VisualElement>("codex-rows");
		}

		private void BuildShopPage()
		{
			VisualElement page = UsePage(Tab.Shop, "shop-page-host");
			pullButton = page.Q<Button>("pull-button");
			pullButton.clicked += Pull;
			pullOdds = page.Q<Label>("pull-odds");
			bagButton = page.Q<Button>("bag-button");
			bagButton.clicked += BuyBag;
			bagNote = page.Q<Label>("bag-note");
		}

		private void BuildLabPage()
		{
			VisualElement page = UsePage(Tab.Lab, "lab-page-host");
			prestigeSummary = page.Q<Label>("prestige-summary");
			prestigeButton = page.Q<Button>("prestige-button");
			prestigeButton.clicked += Prestige;
		}

		/// <summary>던전 넷 (economy.md). 알파 9번이라 지금은 눌리지 않는다</summary>
		private void BuildDungeonPage()
		{
			VisualElement page = UsePage(Tab.Dungeon, "dungeon-page-host");

			dungeonRows = new Button[IdleDungeons.COUNT];

			for (int index = 0; index < dungeonRows.Length; index++)
			{
				Button row = page.Q<Button>("dungeon-" + index);
				dungeonRows[index] = row;

				if (row != null)
				{
					// 입장권은 세지만 들어가지는 못한다. 던전 안 판이 아직 없다 (알파 9번)
					row.SetEnabled(false);
				}
			}
		}

		/// <summary>던전 이름. 순서는 <c>IdleDungeonKind</c> 그대로</summary>
		private string NameOf(IdleDungeonKind kind)
		{
			return uiContentAsset.DungeonName(kind);
		}

		/// <summary>남은 입장권과 다시 찰 때까지 (economy.md 4)</summary>
		private void RenderDungeons(IdleSnapshot snapshot)
		{
			if (dungeonRows == null)
			{
				return;
			}

			long hours = (long)(snapshot.TicketRefillSeconds / 3600d);
			long minutes = (long)(snapshot.TicketRefillSeconds / 60d) % 60L;

			for (int index = 0; index < dungeonRows.Length; index++)
			{
				if (dungeonRows[index] == null)
				{
					continue;
				}

				dungeonRows[index].text = string.Format("{0}  입장권 {1}  다시 참까지 {2}시간 {3}분",
					NameOf((IdleDungeonKind)index), snapshot.Tickets[index], hours, minutes);
			}
		}

		private void BuildInvestPage()
		{
			VisualElement page = UsePage(Tab.Invest, "invest-page-host");
			baseSummary = page.Q<Label>("base-summary");
			VisualElement host = page.Q<VisualElement>("producers");

			for (int kind = 0; kind < 8; kind++)
			{
				int captured = kind;
				TemplateContainer tree = producerRowAsset.Instantiate();
				Button row = tree.Q<Button>("row");
				row.RemoveFromHierarchy();
				row.clicked += () => BuyProducer(captured);
				host.Add(row);
				producerButtons.Add(row);
			}
		}

		private void BuildMapPopup()
		{
			mapPopup = UsePopup("map-popup-host");
			modalController.Register(mapPopup, CloseMap);
			mapPopup.Q<Button>("map-close").clicked += ToggleMap;
			mapRows = mapPopup.Q<VisualElement>("map-rows");
		}

		/// <summary>장비 고르기 팝업. 관리 열 위에 뜬다</summary>
		private void BuildGearPopup()
		{
			gearSelectionController = new GearSelectionController(
				UsePopup("gear-popup-host"),
				choiceCardAsset,
				modalController,
				gearVisualPresenter,
				uiContentAsset,
				Equip);
		}

		private void BuildHeroPopup()
		{
			heroSelectionController = new HeroSelectionController(
				UsePopup("hero-popup-host"),
				choiceCardAsset,
				modalController,
				heroVisualPresenter,
				uiContentAsset,
				ChooseHero);
		}

		private void BuildGoldPopup()
		{
			goldPopup = UsePopup("gold-popup-host");
			modalController.Register(goldPopup, CloseGoldPopup);
			goldAmount = goldPopup.Q<Label>("gold-amount");
			goldIncome = goldPopup.Q<Label>("gold-income");
			goldPopup.Q<Button>("gold-close").clicked += CloseGoldPopup;
		}

		private void BuildSettingsPopup()
		{
			settingsPopup = UsePopup("settings-popup-host");
			modalController.Register(settingsPopup, CloseSettingsPopup);
			settingsPopup.Q<Button>("settings-close").clicked += CloseSettingsPopup;
			for (int index = 0; index < 3; index++)
			{
				int captured = index;
				Button button = settingsPopup.Q<Button>("speed-" + index);
				button.clicked += () => SetSpeed(captured);
				speedButtons.Add(button);
			}

			logLabel = settingsPopup.Q<Label>("log-label");
			noteLabel = settingsPopup.Q<Label>("note-label");
		}

		private void OpenGoldPopup()
		{
			CloseMap();
			CloseHeroPopup();
			CloseGear();
			CloseSettingsPopup();
			modalController.Show(goldPopup);
			Render(session.Capture());
		}

		private void CloseGoldPopup()
		{
			if (goldPopup == null) { return; }
			modalController.Hide(goldPopup);
		}

		private void OpenSettingsPopup()
		{
			CloseMap();
			CloseHeroPopup();
			CloseGear();
			CloseGoldPopup();
			modalController.Show(settingsPopup);
			Render(session.Capture());
		}

		private void CloseSettingsPopup()
		{
			if (settingsPopup == null) { return; }
			modalController.Hide(settingsPopup);
		}

		private void BuildAwayPopup(IdleAwayReport away)
		{
			if (away.HasAnything == false)
			{
				return;
			}

			VisualElement shade = UsePopup("away-popup-host");
			shade.style.display = DisplayStyle.Flex;
			shade.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());

			shade.Q<Label>("away-span").text = string.Format("{0} 동안 작전이 계속됐습니다", DescribeSpan(away.CreditedSeconds));
			shade.Q<Label>("gold-value").text = "+" + BigNumberText.Format(away.ResourceGained);
			shade.Q<Label>("kills-value").text = "+" + BigNumberText.Format(away.KillsGained);
			shade.Q<Label>("stages-value").text = "+" + BigNumberText.Format(away.StagesGained);
			shade.Q<Label>("items-value").text = "+" + BigNumberText.Format(away.ItemsGained);

			Label warning = shade.Q<Label>("away-warning");
			if (away.HitCap)
			{
				warning.text = string.Format("오프라인 상한 {0}. 넘긴 {1}은 보상에 포함되지 않았습니다.",
					DescribeSpan(away.CapSeconds), DescribeSpan(away.LostSeconds));
				warning.style.display = DisplayStyle.Flex;
			}

			shade.Q<Button>("away-close").clicked += () => shade.style.display = DisplayStyle.None;
		}

		private void OpenHeroPopup(int slot)
		{
			if (slot < 0 || slot >= session.State.Party.Length)
			{
				slot = 0;
			}

			gearSeat = slot;
			CloseGear();
			CloseGoldPopup();
			CloseSettingsPopup();
			heroSelectionController.Open(slot);
			Render(session.Capture());
		}

		private void CloseHeroPopup()
		{
			heroSelectionController?.Close();
		}

		private void RenderHeroPopup(IdleSnapshot snapshot)
		{
			heroSelectionController?.Render(snapshot);
		}

		/// <summary>이 부위에 낄 수 있는 가방 아이템만 보여준다</summary>
		private void OpenGear(int slot)
		{
			if (gearHeroId < 0)
			{
				SayOnce("먼저 편성 칸에서 인형을 고른다", runtimeSettingsAsset.NoteSeconds);
				return;
			}

			CloseHeroPopup();
			CloseGoldPopup();
			CloseSettingsPopup();
			gearSelectionController.Open(slot);
			Render(session.Capture());
		}

		private void CloseGear()
		{
			gearSelectionController?.Close();
		}

		private void RenderGear(IdleSnapshot snapshot)
		{
			gearSelectionController?.Render(snapshot, session.State, gearHeroId);
		}

		// ── 그리기 ────────────────────────────────────────────────────────

		public void Render(IdleSnapshot snapshot)
		{
			if (built == false)
			{
				return;
			}

			battleHudController.Render(snapshot, session.State);
			goldAmount.text = uiContentAsset.GoldAmountText(BigNumberText.Format(snapshot.Resource));
			goldIncome.text = uiContentAsset.GoldIncomeText(BigNumberText.Format(snapshot.IncomePerSecond));

			logLabel.text = NextStep(snapshot);

			RenderHand(snapshot);
			RenderTabBadges(snapshot);

			if (mapPopup.style.display == DisplayStyle.Flex)
			{
				RenderMap(snapshot);
			}

			if (split || sideOpen)
			{
				RenderPage(snapshot);
				RenderGear(snapshot);
				RenderHeroPopup(snapshot);
			}

			for (int index = 0; index < speedButtons.Count; index++)
			{
				speedButtons[index].EnableInClassList("idle-settings-speed--selected",
					System.Math.Abs(snapshot.Speed - (index + 1d)) < 0.001d);
			}
		}

		private void RenderHand(IdleSnapshot snapshot)
		{
			cardHandController.Render(snapshot);
		}

		private void RenderTabBadges(IdleSnapshot snapshot)
		{
			sidePanelController.RenderBadges(snapshot, (int)openTab, split || sideOpen);
		}

		private void RenderPage(IdleSnapshot snapshot)
		{
			switch (openTab)
			{
				case Tab.Doll: RenderDollPage(snapshot); break;
				case Tab.Item: RenderItemPage(snapshot); break;
				case Tab.Codex: RenderCodexPage(snapshot); break;
				case Tab.Shop: RenderShopPage(snapshot); break;
				case Tab.Lab: RenderLabPage(snapshot); break;
				case Tab.Invest: RenderInvestPage(snapshot); break;
				case Tab.Dungeon: RenderDungeons(snapshot); break;
				default: break;
			}
		}

		private void RenderDollPage(IdleSnapshot snapshot)
		{
			for (int slot = 0; slot < partyButtons.Count; slot++)
			{
				int id = slot < snapshot.Party.Length ? snapshot.Party[slot] : -1;
				string tag = uiContentAsset.SeatText(IdleHeroes.IsMainSlot(slot));
				Button seat = partyButtons[slot];
				seat.text = string.Empty;
				VisualElement portrait = seat.Q<VisualElement>("seat-icon-" + slot);
				Label label = seat.Q<Label>("seat-label-" + slot);
				portrait.style.display = id >= 0 ? DisplayStyle.Flex : DisplayStyle.None;
				label.text = id >= 0 ? tag + "  " + IdleHeroes.KindOf(id).Name : tag + "  +";
				if (id >= 0) { heroVisualPresenter.SetPortrait(portrait, id); }
				int selectedSeat = heroSelectionController != null ? heroSelectionController.SelectedSeat : -1;
				partyButtons[slot].EnableInClassList("idle-party-seat--picking", selectedSeat == slot);
				partyButtons[slot].EnableInClassList("idle-party-seat--geared", selectedSeat < 0 && gearSeat == slot);
			}

			int wearer = gearHeroId;
			dollName.text = wearer >= 0
				? uiContentAsset.GrowthTitle(IdleHeroes.KindOf(wearer).Name)
				: uiContentAsset.EmptySeatText;
			for (int stat = 0; stat < uiContentAsset.StatCount; stat++)
			{
				IdleUpgradeKind kind = (IdleUpgradeKind)stat;
				IdleUpgradeView one = session.ViewHeroStat(wearer, kind, 1);
				statValues[stat].text = StatValueText(kind, one.CurrentValue);
				statLevels[stat].text = "Lv." + one.Level;

				for (int amount = 0; amount < uiContentAsset.StatUpgradeAmountCount; amount++)
				{
					int count = uiContentAsset.StatUpgradeAmount(amount);
					IdleUpgradeView purchase = session.ViewHeroStat(wearer, kind, count);
					Button button = statButtons[stat, amount];
					button.text = purchase.IsMaxed
						? uiContentAsset.MaxedText
						: string.Format("×{0}\n{1}", count, BigNumberText.Format(purchase.NextCost));
					bool canAfford = wearer >= 0 && purchase.CanAfford;
					button.EnableInClassList("idle-stat-buy--ready", canAfford);
					button.EnableInClassList("idle-stat-buy--maxed", purchase.IsMaxed);
					button.SetEnabled(canAfford);
				}
			}

			// 장비는 인형별 (2026-08-31). 사진의 Worn 은 전장 전체 요약이라 코어에 직접 조회
			if (wearer >= 0)
			{
				IdleGear.CopyWornOf(session.State, wearer, gearOfHero);
			}
			else
			{
				for (int slot = 0; slot < gearOfHero.Length; slot++)
				{
					gearOfHero[slot] = default;
				}
			}

			for (int slot = 0; slot < wornCells.Count && slot < gearOfHero.Length; slot++)
			{
				IdleItem one = gearOfHero[slot];
				wornCells[slot].text = string.Empty;
				VisualElement icon = wornCells[slot].Q<VisualElement>("worn-icon-" + slot);
				Label badge = wornCells[slot].Q<Label>("worn-label-" + slot);
				icon.style.display = one.IsEmpty ? DisplayStyle.None : DisplayStyle.Flex;
				badge.text = one.IsEmpty ? uiContentAsset.GearSlotName(slot) : string.Empty;
				if (one.IsEmpty == false) { gearVisualPresenter.SetSprite(icon, slot, one.Tier); }
				wornCells[slot].EnableInClassList("idle-worn-cell--empty", one.IsEmpty);
				wornCells[slot].SetEnabled(wearer >= 0);
				gearVisualPresenter.SetTierOutline(wornCells[slot], one.IsEmpty ? 0 : one.Tier);
			}

		}

		/// <summary>배속을 다음 자리로 (gap-2026-08-23 P1-6). 보고 있는 동안만</summary>
		private void SetSpeed(int step)
		{
			if (session == null)
			{
				return;
			}

			session.SetSpeedStep(step);
			Render(session.Capture());
		}

		/// <summary>자동 시전 켜고 끄기 (P1-6)</summary>
		private void ToggleAutoCast()
		{
			if (session == null)
			{
				return;
			}

			session.ToggleAutoCast();
			Render(session.Capture());
		}

		/// <summary>가방 한 묶음 넓히기 (상점). 판정은 코어가 한다</summary>
		private void BuyBag()
		{
			if (session == null)
			{
				return;
			}

			session.BuyBagUpgrade();
			Render(session.Capture());
		}

		/// <summary>인형 레벨 한 칸 (economy.md 표 3). 판정은 코어가 한다</summary>
		private void RaiseHeroLevel(int heroId)
		{
			if (session == null)
			{
				return;
			}

			session.RaiseHeroLevel(heroId);
			Render(session.Capture());
		}

		private void RenderItemPage(IdleSnapshot snapshot)
		{
			bool full = snapshot.Bag.Length >= snapshot.BagCapacity;
			gearSummary.text = uiContentAsset.BagSummaryText(snapshot.Bag.Length, snapshot.BagCapacity, full);
			gearSummary.EnableInClassList("idle-warn", full);

			for (int index = 0; index < bagCells.Count; index++)
			{
				if (index >= snapshot.BagCapacity)
				{
					bagCells[index].style.display = DisplayStyle.None;
					continue;
				}

				bagCells[index].style.display = DisplayStyle.Flex;

				if (index >= snapshot.Bag.Length)
				{
					bagCells[index].text = string.Empty;
					bagCells[index].Q<VisualElement>("bag-icon").style.display = DisplayStyle.None;
					bagCells[index].Q<Label>("bag-potential").text = string.Empty;
					bagCells[index].SetEnabled(false);
					gearVisualPresenter.SetTierOutline(bagCells[index], 0);
					continue;
				}

				IdleItem one = snapshot.Bag[index];
				bagCells[index].text = string.Empty;
				VisualElement icon = bagCells[index].Q<VisualElement>("bag-icon");
				icon.style.display = DisplayStyle.Flex;
				gearVisualPresenter.SetSprite(icon, (int)one.Slot, one.Tier);
				bagCells[index].Q<Label>("bag-potential").text =
					uiContentAsset.ItemPotentialText(one.IsRaw, one.PotentialValue);
				bagCells[index].SetEnabled(true);
				gearVisualPresenter.SetTierOutline(bagCells[index], one.Tier);
			}

			bulkMergeButton.text = uiContentAsset.BulkMergeText(snapshot.MergeCount);
			bulkMergeButton.SetEnabled(IdleAdvice.MergeableCount(snapshot) > 0);

			RenderForge(snapshot);
			RenderAppraise(snapshot);
		}

		/// <summary>공방. 가방에 있는 종류(부위, 단계)를 칩으로 늘어놓고, 고른 종류를 3×3 에 채운다</summary>
		private void RenderForge(IdleSnapshot snapshot)
		{
			int room = snapshot.TierCeiling + 2;
			int[] counts = new int[room];

			for (int index = 0; index < snapshot.Bag.Length; index++)
			{
				IdleItem one = snapshot.Bag[index];
				int key = one.Tier;
				if (key >= 0 && key < counts.Length)
				{
					counts[key]++;
				}
			}

			List<int> keys = new List<int>();
			for (int key = 0; key < counts.Length; key++)
			{
				if (counts[key] > 0)
				{
					keys.Add(key);
				}
			}

			if (forgeKindKeys.Count != keys.Count || KeysDiffer(keys))
			{
				forgeKinds.Clear();
				forgeKindButtons.Clear();
				forgeKindKeys.Clear();

				for (int index = 0; index < keys.Count; index++)
				{
					int key = keys[index];
					forgeKindKeys.Add(key);
					forgeKindButtons.Add(AddForgeKind(forgeKinds, () => PickForge(key)));
				}
			}

			for (int index = 0; index < forgeKindButtons.Count; index++)
			{
				int key = forgeKindKeys[index];
				int tier = key;
				forgeKindButtons[index].text = uiContentAsset.ForgeKindText(tier, counts[key]);
				gearVisualPresenter.SetTierOutline(forgeKindButtons[index], tier);
				forgeKindButtons[index].EnableInClassList("idle-forge-kind--on", forgeTier == tier);
			}

			int forgeKey = forgeTier;
			int have = forgeTier > 0 && forgeKey < counts.Length ? counts[forgeKey] : 0;
			int shown = have > snapshot.MergeCount ? snapshot.MergeCount : have;

			for (int index = 0; index < forgeCells.Count; index++)
			{
				bool filled = index < shown;
				forgeCells[index].text = filled ? uiContentAsset.ForgeCellText(forgeTier) : string.Empty;
				gearVisualPresenter.SetTierOutline(forgeCells[index], filled ? forgeTier : 0);
			}

			bool ready = forgeTier > 0 && have >= snapshot.MergeCount;
			forgeResult.text = forgeTier > 0 ? uiContentAsset.ForgeResultText(forgeTier + 1) : string.Empty;
			gearVisualPresenter.SetTierOutline(forgeResult, forgeTier > 0 ? forgeTier + 1 : 0);
			forgeResult.EnableInClassList("idle-forge-cell--ready", ready);

			forgeTitle.text = forgeTier > 0
				? uiContentAsset.ForgeSelectionText(forgeTier, have, snapshot.MergeCount)
				: uiContentAsset.ForgeEmptyHintText(snapshot.MergeCount);
			forgeButton.SetEnabled(ready);
		}

		private bool KeysDiffer(List<int> keys)
		{
			for (int index = 0; index < keys.Count && index < forgeKindKeys.Count; index++)
			{
				if (keys[index] != forgeKindKeys[index])
				{
					return true;
				}
			}

			return false;
		}

		private void PickForge(int key)
		{
			forgeTier = key;
			forgeSlot = IdleItemSlot.Head;
			Render(session.Capture());
		}

		private void RenderAppraise(IdleSnapshot snapshot)
		{
			if (appraiseButtons.Count != snapshot.DroppedByTier.Length)
			{
				appraiseRows.Clear();
				appraiseButtons.Clear();

				for (int tier = 1; tier <= snapshot.DroppedByTier.Length; tier++)
				{
					int captured = tier;
					appraiseButtons.Add(AddRowButton(appraiseRows, () => Appraise(captured)));
				}
			}

			for (int tier = 1; tier <= appraiseButtons.Count; tier++)
			{
				long count = snapshot.DroppedByTier[tier - 1];
				double cost = IdleGear.AppraiseCost(tier, session.Tuning);
				AppraiseBlock why = IdlePotentials.WhyNot(session.State, session.Tuning, tier);

				appraiseButtons[tier - 1].text = why == AppraiseBlock.TierTooLow
					? string.Format("T{0} {1}개. 잠재 없음", tier, BigNumberText.Format(count))
					: string.Format("T{0} {1}개. 감정 {2}", tier, BigNumberText.Format(count),
						BigNumberText.Format(cost));
				appraiseButtons[tier - 1].SetEnabled(why == AppraiseBlock.None);
			}
		}

		private void RenderCodexPage(IdleSnapshot snapshot)
		{
			codexLabel.text = string.Format("도감 {0}점. 판 전체 ×{1:0.00}. 보유 {2}/{3}",
				snapshot.CodexScore, snapshot.CodexMultiplier, snapshot.Heroes.Length, IdleHeroes.Count);

			if (codexLabels.Count != IdleHeroes.Count)
			{
				codexRows.Clear();
				codexLabels.Clear();

				for (int id = 0; id < IdleHeroes.Count; id++)
				{
					TemplateContainer tree = rowLabelAsset.Instantiate();
					Label row = tree.Q<Label>("row");
					row.RemoveFromHierarchy();
					codexRows.Add(row);
					codexLabels.Add(row);
				}
			}

			for (int id = 0; id < codexLabels.Count; id++)
			{
				IdleHeroKind kind = IdleHeroes.KindOf(id);
				int at = session.State.IndexOfHero(id);
				bool owned = at >= 0;
				int stars = owned ? session.State.Heroes[at].Stars : 0;

				codexLabels[id].text = owned
					? string.Format("{0}{1}  {2} / {3}", kind.Name, Stars(stars),
						uiContentAsset.GradeName(kind.Grade), uiContentAsset.AxisName(kind.Axis))
					: string.Format("???  {0}", uiContentAsset.GradeName(kind.Grade));
				codexLabels[id].EnableInClassList("idle-row-title--dim", owned == false);
			}
		}

		private void RenderShopPage(IdleSnapshot snapshot)
		{
			// 가방 넓히기. 골드로 사고 환생 때 사라진다 (사용자 판정 2026-09-01, 울티마 스쿼드)
			bagButton.text = snapshot.BagUpgradeCost > 0d
				? string.Format("가방 +{0}칸. 골드 {1}", IdleShop.BAG_STEP_HINT, BigNumberText.Format(snapshot.BagUpgradeCost))
				: "더 못 넓힌다";
			bagButton.SetEnabled(snapshot.CanBuyBag);
			bagNote.text = string.Format("지금 {0}칸. 환생하면 처음으로 돌아간다",
				snapshot.BagCapacity);

			pullButton.text = snapshot.CanPull
				? string.Format("1회 뽑기. 골드 {0} + 뽑기 재화 {1} (가진 것 {2})",
					BigNumberText.Format(snapshot.PullCost), snapshot.PullStoneCost, snapshot.Stones)
				: snapshot.Stones < snapshot.PullStoneCost
					? string.Format("뽑기 재화가 없다 (가진 것 {0})", snapshot.Stones)
					: string.Format("골드 {0} 이 모자란다", BigNumberText.Format(snapshot.PullCost));
			pullButton.SetEnabled(snapshot.CanPull);

			pullOdds.text = string.Format("레전드 {0:P1} / 에픽 {1:P0} / 레어 {2:P0}. {3}번 안에 레전드 보장",
				snapshot.LegendChance, snapshot.EpicChance, snapshot.RareChance, snapshot.PullsToPity);
		}

		private void RenderLabPage(IdleSnapshot snapshot)
		{
			prestigeSummary.text = string.Format("환생 조각 {0}. 지금 환생하면 +{1}. 배수 ×{2:0.00}",
				snapshot.PrestigePoints, snapshot.PrestigeAward, snapshot.PrestigeMultiplier);

			prestigeButton.text = snapshot.PrestigeAward > 0L
				? string.Format("페이지를 넘긴다. +{0}", snapshot.PrestigeAward)
				: string.Format("{0}구역부터 환생할 수 있다", snapshot.PrestigeNextStage);
			prestigeButton.SetEnabled(snapshot.PrestigeAward > 0L);
		}

		private void RenderInvestPage(IdleSnapshot snapshot)
		{
			baseSummary.text = string.Format("생산자가 초당 {0} 를 낸다",
				BigNumberText.Format(snapshot.IncomePerSecond));

			for (int kind = 0; kind < producerButtons.Count; kind++)
			{
				if (kind >= snapshot.Producers.Length)
				{
					producerButtons[kind].style.display = DisplayStyle.None;
					continue;
				}

				IdleProducerView view = snapshot.Producers[kind];
				producerButtons[kind].style.display = view.Hidden ? DisplayStyle.None : DisplayStyle.Flex;
				producerButtons[kind].text = string.Format("{0}번 ×{1}. 초당 {2}. 다음 {3}",
					kind + 1, view.Owned,
					BigNumberText.Format(view.OutputTotal),
					BigNumberText.Format(view.NextCost));
				producerButtons[kind].SetEnabled(view.CanAfford);
			}
		}

		private void RenderMap(IdleSnapshot snapshot)
		{
			int top = snapshot.BestStage;
			int bottom = top - 7 < 1 ? 1 : top - 7;
			int count = top - bottom + 1;

			if (mapButtons.Count != count)
			{
				mapRows.Clear();
				mapButtons.Clear();

				for (int index = 0; index < count; index++)
				{
					int target = top - index;
					mapButtons.Add(AddRowButton(mapRows, () => GoToStage(target)));
				}
			}

			for (int index = 0; index < mapButtons.Count; index++)
			{
				int target = top - index;
				bool here = target == snapshot.Stage;
				mapButtons[index].text = string.Format("S-{0}{1}{2}", target,
					here ? "  지금 여기" : string.Empty,
					target == snapshot.BestFarmingStage ? "  (가장 잘 벌리는 곳)" : string.Empty);
				mapButtons[index].SetEnabled(here == false && IdleModel.CanGoToStage(session.State, target));
				mapButtons[index].EnableInClassList("idle-row-button--strong", here);
			}
		}

		/// <summary>코어가 고른 한 걸음을 사람 말로.</summary>
		private string NextStep(IdleSnapshot snapshot)
		{
			if (false && snapshot.Repeating)
			{
				return "전멸. 인형과 아이템을 손보고 다시 도전한다";
			}

			IdleAdviceResult advice = IdleAdvice.NextStep(snapshot);
			return uiContentAsset.AdviceText(advice.Step, advice.Amount, DescribeSpan(advice.Amount));
		}

		// ── 화면 상태 ─────────────────────────────────────────────────────

		private void OpenTab(Tab tab)
		{
			openTab = tab;
			heroSelectionController?.ClearSelection();
			sideOpen = true;

			sidePanelController.ShowPage((int)tab);

			// 상점, 연구소는 왼쪽 씬이 바뀐다 (layout.md §2). 지금은 덮개
			bool altScene = tab == Tab.Shop || tab == Tab.Lab;
			battleHudController.SetAlternateScene(altScene,
				tab == Tab.Shop ? "SHOP 3D SCENE 자리" : "LAB 3D SCENE 자리");

			ApplySplit();
			Render(session.Capture());
		}

		private void CloseSide()
		{
			sideOpen = false;
			battleHudController.SetAlternateScene(false, string.Empty);
			ApplySplit();
		}

		/// <summary>
		/// 무대 카메라를 전투 창 폭에
		///
		/// ★ 옛 값은 1200/1920 <b>고정</b>이었다. 모바일은 20:9 도 흔해서 그 비율에서는
		///   무대와 관리 열 경계가 어긋난다 (실측 2026-09-01: 2400x1080 에서 95px 차이)
		/// ★ 그래서 관리 열의 <b>실제 폭</b> 기준. 비율이 무엇이든 경계가 붙음
		/// </summary>
		private void AimCamera()
		{
			Camera main = Camera.main;
			if (main == null)
			{
				return;
			}

			if (split == false)
			{
				main.rect = new Rect(0f, 0f, 1f, 1f);
				return;
			}

			float share = uiContentAsset.BattleWidthShare;
			float sideWidth = sidePanelController != null ? sidePanelController.ResolvedWidth : float.NaN;

			if (float.IsNaN(sideWidth) == false && sideWidth > 0f && root != null)
			{
				float whole = root.resolvedStyle.width;
				if (float.IsNaN(whole) == false && whole > sideWidth)
				{
					share = 1f - sideWidth / whole;
				}
			}

			main.rect = new Rect(0f, 0f, share, 1f);
		}

		/// <summary>
		/// 노치와 둥근 모서리를 피해 UI 를 안쪽으로 (모바일)
		///
		/// ★ <see cref="Screen.safeArea"/> 는 PC 에서 화면 전체. 아무 효과 없음
		/// ★ 무대(3D)는 제외. 잘려도 되는 배경이고, 밀면 분할 경계가 또 어긋남
		/// </summary>
		private void ApplySafeArea()
		{
			if (root == null)
			{
				return;
			}

			Rect safe = Screen.safeArea;
			float wide = Screen.width;
			float high = Screen.height;

			if (wide <= 0f || high <= 0f)
			{
				return;
			}

			// 화면 픽셀을 UI 논리 크기로. 패널이 스케일하므로 비율 환산
			float scale = root.resolvedStyle.width > 0f && float.IsNaN(root.resolvedStyle.width) == false
				? root.resolvedStyle.width / wide
				: 1f;

			root.style.paddingLeft = safe.xMin * scale;
			root.style.paddingRight = (wide - safe.xMax) * scale;
			root.style.paddingBottom = safe.yMin * scale;
			root.style.paddingTop = (high - safe.yMax) * scale;
		}

		private void ToggleSplit()
		{
			split = split == false;
			PlayerPrefs.SetInt("idle.split", split ? 1 : 0);
			sideOpen = false;
			ApplySplit();
			Render(session.Capture());
		}

		private void ApplySplit()
		{
			sidePanelController.Apply((int)openTab, split, sideOpen);
			battleHudController.SetSplit(split);

			AimCamera();
			ApplySafeArea();

		}

		private void ToggleMap()
		{
			bool open = mapPopup.style.display != DisplayStyle.Flex;
			if (open)
			{
				CloseHeroPopup();
				CloseGear();
				CloseGoldPopup();
				CloseSettingsPopup();
			}
			if (open)
			{
				modalController.Show(mapPopup);
			}
			else
			{
				modalController.Hide(mapPopup);
			}
			Render(session.Capture());
		}

		private void CloseMap()
		{
			modalController.Hide(mapPopup);
		}

		/// <summary>
		/// 저장 삭제 뒤 처음부터 재시작. 디버그 전용
		///
		/// ★ 끄면서 저장하는 길(<see cref="OnDisable"/>)이 지운 것을 되살리지 않게 차단 뒤 끔
		/// </summary>
		private void WipeAndRestart()
		{
			// 미리보기는 저장과 무관. 임시 판만 새로
			if (preview)
			{
				enabled = false;
				enabled = true;
				return;
			}

			persistence.WipeAndSkipClose();
			enabled = false;
			enabled = true;
		}

		// ── 의도 ──────────────────────────────────────────────────────────

		private void OnTapped(PointerDownEvent moment)
		{
			if (moment.target is Button || (moment.target is VisualElement element && IsInsideBox(element)))
			{
				return;
			}

			session.Send(new IdleTapIntent());

			if (stage != null)
			{
				stage.OnTap();
			}

			Render(session.Capture());
		}

		private static bool IsInsideBox(VisualElement element)
		{
			for (VisualElement at = element; at != null; at = at.parent)
			{
				if (at.ClassListContains("idle-box"))
				{
					return true;
				}
			}

			return false;
		}

		private void Cast(int handIndex)
		{
			IdleCardKind selected = IdleCards.HandAt(session.State, handIndex);
			if (selected == IdleCardKind.Volley)
			{
				cardHandController.CancelAim();
				SayOnce("일제 사격 카드를 끌어 적에게 놓으세요", runtimeSettingsAsset.NoteSeconds);
				return;
			}

			if (session.TryCastCard(handIndex, out IdleCardResult result) == false)
			{
				return;
			}

			IdleCardKind kind = result.Kind;

			switch (kind)
			{
				case IdleCardKind.Volley:
					// Volley is target-only and is resolved by EndSkillDrag.
					SayOnce("일제 사격. 모두 달려들었다", runtimeSettingsAsset.NoteSeconds);
					break;

				case IdleCardKind.Supply:
					if (stage != null) { stage.OnSupply((float)session.Tuning.SupplySeconds); }
					SayOnce(string.Format("긴급 보급. {0:0}초 동안 수입 ×{1:0.#}",
						session.Tuning.SupplySeconds, session.Tuning.SupplyMultiplier), runtimeSettingsAsset.NoteSeconds);
					break;

				default:
					if (stage != null) { stage.OnAppraise(); }
					SayOnce(result.HasRoll
						? string.Format("비밀 감정. T{0} → {1:P1}{2}",
							result.Roll.Tier, result.Roll.Value, result.Roll.Replaced ? " 갈아 끼움" : string.Empty)
						: "비밀 감정. 굴릴 것이 없다", runtimeSettingsAsset.NoteSeconds);
					break;
			}

			WriteDown();
			Render(session.Capture());
		}

		private void NextStage()
		{
			if (session.Send(new IdleNextStageIntent()))
			{
				SayOnce("다시 내려간다. 부대는 만전이다", runtimeSettingsAsset.NoteSeconds);
				WriteDown();
			}

			Render(session.Capture());
		}

		private void StepStage(int delta)
		{
			GoToStage(session.Capture().Stage + delta);
		}

		private void GoToStage(int target)
		{
			CloseMap();
			if (session.Send(new IdleGoToStageIntent(target)))
			{
				WriteDown();
			}

			Render(session.Capture());
		}

		private void ToggleHold()
		{
			session.Send(new IdleHoldStageIntent(session.Capture().HoldingStage == false));
			WriteDown();
			Render(session.Capture());
		}

		private void Raise(IdleUpgradeKind kind, int amount)
		{
			if (gearHeroId < 0)
			{
				return;
			}

			IdleUpgradeView before = session.ViewHeroStat(gearHeroId, kind, amount);
			double resourceBefore = session.State.Resource;
			bool raised = session.Send(new IdleRaiseUpgradeIntent(gearHeroId, kind, amount));
			if (raised)
			{
				WriteDown();
			}
			Render(session.Capture());

			if (raised)
			{
				IdleUpgradeView after = session.ViewHeroStat(gearHeroId, kind, 1);
				ShowStatRaised(kind, amount, before.CurrentValue, after.CurrentValue,
					resourceBefore - session.State.Resource);
			}
		}

		private void ShowStatRaised(IdleUpgradeKind kind, int amount, double before, double after, double spent)
		{
			int stat = (int)kind;
			int amountIndex = uiContentAsset.IndexOfStatUpgradeAmount(amount);
			if (stat < 0 || stat >= statValues.Length || amountIndex < 0)
			{
				return;
			}

			statFeedbackVersion++;
			sound?.Good();
			int version = statFeedbackVersion;
			statFeedback.text = string.Format("{0}  {1} → {2}   골드 -{3}",
				uiContentAsset.StatName(stat), StatValueText(kind, before), StatValueText(kind, after), BigNumberText.Format(spent));
			statFeedback.style.visibility = Visibility.Visible;
			statFeedback.AddToClassList("idle-stat-feedback--shown");
			statValues[stat].AddToClassList("idle-stat-label--raised");
			statButtons[stat, amountIndex].AddToClassList("idle-stat-buy--raised");

			statFeedback.schedule.Execute(() =>
			{
				if (version == statFeedbackVersion)
				{
					statValues[stat].RemoveFromClassList("idle-stat-label--raised");
					statButtons[stat, amountIndex].RemoveFromClassList("idle-stat-buy--raised");
				}
			}).StartingIn(350L);

			statFeedback.schedule.Execute(() =>
			{
				if (version == statFeedbackVersion)
				{
					statFeedback.RemoveFromClassList("idle-stat-feedback--shown");
					statFeedback.style.visibility = Visibility.Hidden;
				}
			}).StartingIn(1200L);
		}

		private void BuyProducer(int kind)
		{
			session.Send(new IdleBuyProducerIntent(kind));
			Render(session.Capture());
		}

		private void Equip(int bagIndex)
		{
			session.Send(new IdleEquipIntent(gearHeroId, bagIndex));
			WriteDown();
			Render(session.Capture());
		}

		private void Merge(int tier, IdleItemSlot slot)
		{
			if (session.Send(new IdleMergeIntent(tier, slot)))
			{
				SayOnce(string.Format("{0} {1}단계 → {2}단계", uiContentAsset.GearSlotName((int)slot), tier, tier + 1), runtimeSettingsAsset.NoteSeconds);
				WriteDown();
			}

			Render(session.Capture());
		}

		private void MergeForge()
		{
			if (forgeTier <= 0)
			{
				return;
			}

			Merge(forgeTier, forgeSlot);
		}

		/// <summary>가방에서 합칠 수 있는 묶음을 전부 합친다. 낮은 단계부터</summary>
		private void MergeAll()
		{
			int merged = 0;
			IdleSnapshot now = session.Capture();

			for (int tier = 1; tier <= now.TierCeiling + 1; tier++)
			{
				while (session.Send(new IdleMergeIntent(tier, IdleItemSlot.Head)))
				{
					merged++;
				}
			}

			if (merged > 0)
			{
				SayOnce(string.Format("{0}번 합쳤다", merged), runtimeSettingsAsset.NoteSeconds);
				WriteDown();
			}

			Render(session.Capture());
		}

		private void Appraise(int tier)
		{
			if (session.TryAppraise(tier, out PotentialRoll roll))
			{
				SayOnce(string.Format("T{0} 감정 → {1:P1}{2}",
					roll.Tier, roll.Value, roll.Replaced ? " 갈아 끼움" : string.Empty), runtimeSettingsAsset.NoteSeconds);
				WriteDown();
			}

			Render(session.Capture());
		}

		private void Pull()
		{
			if (session.TryPull(out IdleHeroPull got) == false)
			{
				return;
			}

			IdleHeroKind kind = IdleHeroes.KindOf(got.Id);
			SayOnce(string.Format("{0} {1}{2}{3}",
				uiContentAsset.GradeName(got.Grade), kind.Name,
				got.IsNew ? ". 처음 본 얼굴" : string.Empty,
				got.ByPity ? " (천장)" : string.Empty), runtimeSettingsAsset.NoteSeconds * 2f);

			WriteDown();
			Render(session.Capture());
		}

		/// <summary>
		/// 편성 칸을 눌렀다. 빈 칸이면 채우기, 찬 칸이면 <b>장비 대상</b>을 그 인형으로
		///
		/// ★ 인형이 여럿이라 장비를 누구 것으로 볼지 정해야 한다 (사용자 2026-08-31)
		/// </summary>
		private void BeginSeat(int slot)
		{
			OpenHeroPopup(slot);
		}

		private void ChooseHero(int id)
		{
			int slot = heroSelectionController != null ? heroSelectionController.SelectedSeat : -1;

			if (slot < 0)
			{
				slot = FirstEmptySeat();
			}

			if (slot < 0)
			{
				SayOnce("자리가 다 찼다. 바꿀 칸을 먼저 누른다", runtimeSettingsAsset.NoteSeconds);
				Render(session.Capture());
				return;
			}

			session.Send(new IdleSetPartyIntent(slot, id));
			gearSeat = slot;
			CloseHeroPopup();
			WriteDown();
			Render(session.Capture());
		}

		private int FirstEmptySeat()
		{
			IdleSnapshot now = session.Capture();

			for (int slot = 0; slot < now.Party.Length; slot++)
			{
				if (now.Party[slot] < 0)
				{
					return slot;
				}
			}

			return -1;
		}

		private void Prestige()
		{
			if (session.Send(new IdlePrestigeIntent()))
			{
				SayOnce("환생. 새 종이. 코스트는 그대로다", runtimeSettingsAsset.NoteSeconds * 2f);
				WriteDown();
			}

			Render(session.Capture());
		}

		// ── 툴팁 ───────────────────────────────────────────────────────────

		/// <summary>마우스를 올리면 뜨는 설명. PC 우선 (layout.md §1). 글은 부르는 쪽이 만든다</summary>
		/// <summary>
		/// 설명 붙이기. 마우스는 올리면, 손가락은 누르면
		///
		/// ★ 모바일에 호버 없음 (2026-09-01). 호버만 걸면 장비 정보 조회 불가
		/// </summary>
		private void HookTooltip(VisualElement target, System.Func<string> text)
		{
			tooltipController.Bind(target, text);
		}

		private string StatTip(IdleUpgradeKind kind, int amount)
		{
			if (session == null || gearHeroId < 0)
			{
				return "인형을 먼저 선택";
			}

			IdleUpgradeView view = session.ViewHeroStat(gearHeroId, kind, amount);
			if (view.IsMaxed)
			{
				return uiContentAsset.StatName((int)kind) + "\n최대 성장";
			}

			string wait = view.CanAfford || double.IsInfinity(view.SecondsToAfford)
				? string.Empty
				: string.Format("\n약 {0:0}초 뒤 구매", view.SecondsToAfford);
			return string.Format("{0} ×{1}\n{2} → {3}\n골드 {4}{5}",
				uiContentAsset.StatName((int)kind), amount,
				StatValueText(kind, view.CurrentValue), StatValueText(kind, view.NextValue),
				BigNumberText.Format(view.NextCost), wait);
		}

		private void EnsureSound()
		{
			if (sound == null && Application.isPlaying && Application.isBatchMode == false)
			{
				sound = new ProceduralSfx(gameObject, runtimeSettingsAsset.SoundVolume, runtimeSettingsAsset.SoundMinGapSeconds);
			}
		}

		private void OnButtonClicked(ClickEvent moment)
		{
			if (moment.target is Button button && button.ClassListContains("idle-stat-buy") == false)
			{
				sound?.Click();
			}
		}

		private string BagTip(int index)
		{
			IdleSnapshot now = session.Capture();
			if (index >= now.Bag.Length)
			{
				return string.Empty;
			}

			IdleItem one = now.Bag[index];
			IdleItem wearing = now.Worn[(int)one.Slot];
			return string.Format("{0} {1}단계\n효과 ×{2:0.00}\n차고 있는 것 {3}\n누르면 찬다",
				uiContentAsset.GearSlotName((int)one.Slot), one.Tier,
				IdleGear.MultiplierOfItem(one, session.Tuning),
				wearing.IsEmpty ? "없음" : wearing.Tier + "단계 ×" + IdleGear.MultiplierOfItem(wearing, session.Tuning).ToString("0.00"));
		}

		private string WornTip(int slot)
		{
			IdleSnapshot now = session.Capture();
			IdleItem one = now.Worn[slot];
			return one.IsEmpty
				? uiContentAsset.GearSlotName(slot) + "\n비었다. 아이템 탭에서 찬다"
				: string.Format("{0} {1}단계\n효과 ×{2:0.00}", uiContentAsset.GearSlotName(slot), one.Tier, IdleGear.MultiplierOfItem(one, session.Tuning));
		}

		// ── 잔손 ──────────────────────────────────────────────────────────

		private void SayOnce(string what, float seconds)
		{
			noteLabel.text = what;
			noteLabel.style.opacity = 1f;
			noteLeft = seconds;
		}

		private string StatValueText(IdleUpgradeKind kind, double value)
		{
			return uiContentAsset.StatValueText(kind, value);
		}

		private static string Stars(int stars)
		{
			return stars <= 0 ? string.Empty : " " + new string('★', stars);
		}

		private Button AddRowButton(VisualElement parent, System.Action action)
		{
			TemplateContainer tree = rowButtonAsset.Instantiate();
			Button button = tree.Q<Button>("row");
			button.RemoveFromHierarchy();
			button.clicked += action;
			parent.Add(button);
			return button;
		}

		private static string DescribeSpan(double seconds)
		{
			if (seconds < 60d)
			{
				return string.Format("{0:N0}초", seconds);
			}

			if (seconds < 3600d)
			{
				return string.Format("{0:N0}분", seconds / 60d);
			}

			return string.Format("{0:N1}시간", seconds / 3600d);
		}
	}
}
