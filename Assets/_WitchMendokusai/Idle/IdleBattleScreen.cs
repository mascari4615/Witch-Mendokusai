using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using WitchMendokusai.DomainSDK.Contracts;
using WitchMendokusai.DomainSDK.Idle;
using BigNumberText = WitchMendokusai.Numerics.BigNumberText;

namespace WitchMendokusai
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
	[RequireComponent(typeof(UIDocument))]
	public sealed class IdleBattleScreen : MonoBehaviour, IGameView<IdleSnapshot>
	{
		[Header("수치. 비워 두면 코드 기본값")]
		[SerializeField] private IdleTuningSO tuningAsset;

		[Header("생김새")]
		[SerializeField] private StyleSheet styleSheet;

		[Header("손으로 고치는 UXML. 비우면 코드가 짓는다 (사용자 2026-08-30: 고퀄리티는 사람 손길)")]
		[SerializeField] private VisualTreeAsset dollPageAsset;
		[SerializeField] private VisualTreeAsset itemPageAsset;
		[SerializeField] private VisualTreeAsset bagCellAsset;
		[SerializeField] private VisualTreeAsset forgeKindAsset;
		[SerializeField] private VisualTreeAsset battleHudAsset;
		[SerializeField] private VisualTreeAsset cardAsset;
		[SerializeField] private VisualTreeAsset waveDotAsset;

		[Header("무대. 씬이 꽂아 준다")]
		[SerializeField] private IdleBattleStage stage;

		[Header("얼마나 자주 적나 (초)")]
		[SerializeField] private float saveIntervalSeconds = 10f;

		[Header("알림이 남는 시간 (초)")]
		[SerializeField] private float noteSeconds = 5f;

		private IdleSession session;
		private float sinceLastSave;
		private bool skipSaveOnce;

		// 에디트 모드 미리보기 (사용자 2026-08-30: UI 수정은 Play 없이). 저장 읽기와 쓰기 없음. 임시 판 위 시뮬만
		private bool preview;
		private double previewClock;
		private bool previewTicked;

		/// <summary>미리보기 시뮬 진행 여부. 기본은 첫 틱 뒤 정지 (정적 장면). Dev Panel 이 켠다</summary>
		public static bool PreviewRunning { get; set; }

		// 짓기가 끝나야 그린다. 짓는 도중 Render 가 돌면 아직 없는 조각(맵 팝업)에서 죽는다 (실측 2026-08-30)
		private bool built;

		// ── 탭 ────────────────────────────────────────────────────────────
		private enum Tab { Doll = 0, Item = 1, Codex = 2, Shop = 3, Lab = 4, Dungeon = 5, Invest = 6 }

		private static readonly string[] TAB_NAMES = { "인형", "아이템", "도감", "상점", "연구소", "던전", "투자" };
		private static readonly string[] TAB_CAPTIONS = { "DOLLS", "ITEMS", "CODEX", "SHOP", "LAB", "DUNGEON", "INVEST" };
		private static readonly string[] SLOT_NAMES = { "머리", "몸", "손", "발" };

		/// <summary>전투 창이 차지하는 폭. 1200 / 1920 (layout.md §1)</summary>
		private const float BATTLE_SHARE = 1200f / 1920f;

		/// <summary>지금 보이는 탭. 나머지는 임시로 숨김 (사용자 2026-08-30, 개발 편의. 코드는 유지)</summary>
		private static readonly bool[] TAB_SHOWN = { true, true, false, false, false, false, false };

		// ── 전투 창 ───────────────────────────────────────────────────────
		private VisualElement battle;
		private VisualElement sceneCover;
		private Label sceneCoverLabel;

		private Label opCode;
		private Label opName;
		private VisualElement waveDots;
		private readonly List<VisualElement> waveDotList = new List<VisualElement>();
		private Label waveLabel;

		private Button stepBack;
		private Button stepForward;
		private Label stepLabel;
		private Button repeatButton;

		private Label goldChip;
		private Label pullChip;
		private Label prestigeChip;
		private Button splitButton;

		private Label logLabel;
		private Label noteLabel;
		private float noteLeft;

		private VisualElement enemyBar;
		private VisualElement enemyFill;
		private Label enemyLabel;

		private VisualElement failBanner;
		private Label failLabel;
		private Button nextStageButton;

		private Button[] cardButtons;
		private VisualElement costFill;
		private Label costLabel;

		private VisualElement floatingTabs;
		private readonly List<Button> floatingTabButtons = new List<Button>();

		// ── 관리 열 ───────────────────────────────────────────────────────
		private VisualElement side;
		private readonly List<Button> tabButtons = new List<Button>();
		private Button closeSideButton;
		private VisualElement panelHost;
		private Label panelTitle;
		private Label panelCaption;
		private VisualElement[] pages;
		private Tab openTab = Tab.Doll;
		private bool split = true;
		private bool sideOpen;

		// 인형
		private readonly List<Button> partyButtons = new List<Button>();
		private int seatBeingFilled = -1;
		private Label dollName;
		private Label damageLabel;
		private Button damageButton;
		private Label speedLabel;
		private Button speedButton;
		private Button bulkRaiseButton;
		private readonly List<Label> wornCells = new List<Label>();
		private VisualElement heroRows;
		private readonly List<Button> heroButtons = new List<Button>();

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

		// 연구소
		private Label prestigeSummary;
		private Button prestigeButton;

		// 투자
		private Label baseSummary;
		private Button bulkBuyButton;
		private readonly List<Button> producerButtons = new List<Button>();

		// 툴팁
		private Label tooltip;

		// 팝업
		private VisualElement mapPopup;
		private VisualElement mapRows;
		private readonly List<Button> mapButtons = new List<Button>();

		public PresentationKind Kind => PresentationKind.UIOnly;

		private void OnEnable()
		{
			if (tuningAsset == null)
			{
				Debug.LogWarning("[Idle] 수치 에셋이 안 꽂혀 있다. 코드 기본값으로 돈다.");
			}

			IdleTuning tuning = tuningAsset != null ? tuningAsset.ToTuning() : new IdleTuning();
			preview = Application.isPlaying == false;

			IdleState state = new IdleState();
			IdleAwayReport away = default;

			if (preview)
			{
				previewTicked = false;
				state = PreviewState(tuning);
				session = new IdleSession(tuning, state);
#if UNITY_EDITOR
				UnityEditor.EditorApplication.update -= PreviewTick;
				UnityEditor.EditorApplication.update += PreviewTick;
				previewClock = UnityEditor.EditorApplication.timeSinceStartup;
#endif
			}
			else
			{
				IdleSaveData? saved = IdleSaveStore.Load();
				if (saved.HasValue)
				{
					state.Load(saved.Value);
				}

				session = new IdleSession(tuning, state);
				session.CatchUp(IdleSaveStore.NowUnixSeconds(), out away);
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

		/// <summary>미리보기 판. 인형 셋(근, 중, 원), 4구역(원거리 적 등장), 골드 약간. 저장과 무관</summary>
		private static IdleState PreviewState(IdleTuning tuning)
		{
			IdleState state = new IdleState();
			IdleHeroes.EnsureStarter(state);

			int[] party = { 0, 3, 1 };
			for (int seat = 0; seat < party.Length && seat < state.Party.Length; seat++)
			{
				if (state.IndexOfHero(party[seat]) < 0)
				{
					state.Heroes.Add(new IdleHeroOwned(party[seat]));
				}

				state.Party[seat] = party[seat];
			}

			state.Stage = 4;
			state.BestStage = 4;
			state.ClearedStage = 3;
			state.Resource = 500d;
			state.EnsureSeatRoom(tuning);
			return state;
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

		private void OnDisable()
		{
#if UNITY_EDITOR
			UnityEditor.EditorApplication.update -= PreviewTick;
#endif
			if (preview)
			{
				session = null;
				return;
			}

			if (skipSaveOnce)
			{
				skipSaveOnce = false;
				session = null;
				return;
			}

			WriteDown();
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

			Tick(Time.unscaledDeltaTime);
		}

		private void Tick(float delta)
		{
			if (session == null)
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

			Render(snapshot);

			if (preview)
			{
				return;
			}

			sinceLastSave += delta;
			if (sinceLastSave >= saveIntervalSeconds)
			{
				WriteDown();
			}
		}

		private void WriteDown()
		{
			if (session == null)
			{
				return;
			}

			sinceLastSave = 0f;
			session.MarkSeen(IdleSaveStore.NowUnixSeconds());
			IdleSaveStore.Save(session.State.Save());
		}

		// ── 짓기 ──────────────────────────────────────────────────────────

		private void BuildAll(IdleAwayReport away)
		{
			VisualElement root = GetComponent<UIDocument>().rootVisualElement;
			root.Clear();
			built = false;

			if (styleSheet != null)
			{
				root.styleSheets.Add(styleSheet);
			}
			else
			{
				Debug.LogWarning("[Idle] 스타일시트가 안 꽂혀 있다. 화면이 꾸밈 없이 뜬다.");
			}

			VisualElement shell = new VisualElement();
			shell.AddToClassList("idle-root");
			root.Add(shell);

			BuildBattle(shell);
			BuildSide(shell);
			BuildMapPopup();

			tooltip = AddLabel(root, "idle-tooltip");
			tooltip.style.display = DisplayStyle.None;
			tooltip.pickingMode = PickingMode.Ignore;

			built = true;
			ApplySplit();

			if (away.HasAnything)
			{
				SayOnce(string.Format("자리 비운 {0}. 골드 +{1}, {2}마리, 코스트 가득",
					DescribeSpan(away.CreditedSeconds),
					BigNumberText.Format(away.ResourceGained),
					BigNumberText.Format(away.KillsGained)), noteSeconds * 3f);
			}
		}

		private void BuildBattle(VisualElement shell)
		{
			battle = new VisualElement();
			battle.AddToClassList("idle-battle");
			shell.Add(battle);

			// 빈 곳 누르기는 응원 한 대. 무대 그 자체가 큰 버튼
			battle.RegisterCallback<PointerDownEvent>(OnTapped);

			if (battleHudAsset != null)
			{
				BindBattleHud();
			}
			else
			{
				BuildBattleHud();
			}

			if (battleHudAsset != null)
			{
				BuildBattleExtras();
			}
		}

		private void BuildBattleHud()
		{

			// 상점, 연구소 씬 자리. 지금은 덮개 + 글자
			sceneCover = new VisualElement();
			sceneCover.AddToClassList("idle-scene-cover");
			sceneCover.style.display = DisplayStyle.None;
			battle.Add(sceneCover);
			sceneCoverLabel = AddLabel(sceneCover, "idle-scene-cover-label");
			AddButton(sceneCover, "idle-button", () => OpenTab(Tab.Doll)).text = "전투로";

			// 좌상. 작전 코드 + 웨이브. 누르면 맵 팝업
			VisualElement op = new VisualElement();
			AddClasses(op, "idle-box idle-op");
			battle.Add(op);
			op.RegisterCallback<ClickEvent>(_ => ToggleMap());

			VisualElement opRow = new VisualElement();
			opRow.AddToClassList("idle-op-row");
			op.Add(opRow);
			opCode = AddLabel(opRow, "idle-op-code");
			opName = AddLabel(opRow, "idle-op-name");

			VisualElement waveRow = new VisualElement();
			waveRow.AddToClassList("idle-op-row");
			op.Add(waveRow);
			waveDots = new VisualElement();
			waveDots.AddToClassList("idle-wave");
			waveRow.Add(waveDots);
			waveLabel = AddLabel(waveRow, "idle-cap");

			// 좌상 둘째 줄. 스테퍼 + 반복
			VisualElement stepRow = new VisualElement();
			stepRow.AddToClassList("idle-step-row");
			battle.Add(stepRow);

			VisualElement stepper = new VisualElement();
			AddClasses(stepper, "idle-box idle-stepper");
			stepRow.Add(stepper);
			stepBack = AddButton(stepper, "idle-step-button", () => StepStage(-1));
			stepBack.text = "◀";
			stepLabel = AddLabel(stepper, "idle-step-label");
			stepForward = AddButton(stepper, "idle-step-button", () => StepStage(1));
			stepForward.text = "▶";

			repeatButton = AddButton(stepRow, "idle-box idle-toggle", ToggleHold);

			// 우상. 재화 3 + 분할
			VisualElement chips = new VisualElement();
			chips.AddToClassList("idle-chips");
			battle.Add(chips);
			goldChip = AddLabel(chips, "idle-box idle-chip");
			pullChip = AddLabel(chips, "idle-box idle-chip");
			prestigeChip = AddLabel(chips, "idle-box idle-chip");
			splitButton = AddButton(chips, "idle-box idle-icon-button", ToggleSplit);
			splitButton.text = "분할";

			// 우상 둘째 줄. 배속, AUTO 자리 (코어 미구현. 자리만)
			VisualElement speedRow = new VisualElement();
			speedRow.AddToClassList("idle-speed-row");
			battle.Add(speedRow);
			Button speed = AddButton(speedRow, "idle-box idle-icon-button", null);
			speed.text = "1×";
			speed.SetEnabled(false);
			Button auto = AddButton(speedRow, "idle-box idle-icon-button", null);
			auto.text = "AUTO";
			auto.SetEnabled(false);

			// 우측. 로그 (지금은 안내 한 줄 + 알림 한 줄)
			VisualElement log = new VisualElement();
			AddClasses(log, "idle-box idle-log");
			battle.Add(log);
			AddLabel(log, "idle-cap").text = "LOG";
			logLabel = AddLabel(log, "idle-log-line");
			noteLabel = AddLabel(log, "idle-log-line idle-log-line--note");

			// 상단 중앙. 보스 바 (보스 때만)
			enemyBar = new VisualElement();
			AddClasses(enemyBar, "idle-box idle-enemy");
			battle.Add(enemyBar);
			enemyLabel = AddLabel(enemyBar, "idle-enemy-label");
			VisualElement gauge = new VisualElement();
			gauge.AddToClassList("idle-enemy-gauge");
			enemyBar.Add(gauge);
			enemyFill = new VisualElement();
			enemyFill.AddToClassList("idle-enemy-fill");
			gauge.Add(enemyFill);

			// 중앙. 실패 배너 (반복 중일 때만)
			failBanner = new VisualElement();
			AddClasses(failBanner, "idle-box idle-fail");
			failBanner.style.display = DisplayStyle.None;
			battle.Add(failBanner);
			failLabel = AddLabel(failBanner, "idle-fail-label");
			nextStageButton = AddButton(failBanner, "idle-button idle-button--strong", NextStage);

			// 하단 중앙. 손패 + 코스트
			VisualElement hand = new VisualElement();
			hand.AddToClassList("idle-hand");
			battle.Add(hand);

			VisualElement cards = new VisualElement();
			cards.name = "cards";
			hand.Add(cards);
			BindCardButtons(cards);

			VisualElement cost = new VisualElement();
			cost.AddToClassList("idle-cost");
			hand.Add(cost);
			costLabel = AddLabel(cost, "idle-cost-label");
			VisualElement costGauge = new VisualElement();
			costGauge.AddToClassList("idle-cost-gauge");
			cost.Add(costGauge);
			costFill = new VisualElement();
			costFill.AddToClassList("idle-cost-fill");
			costGauge.Add(costFill);
			AddLabel(cost, "idle-cap").text = "COST";

			// 우하. 풀화면일 때만 보이는 탭 7
			floatingTabs = new VisualElement();
			floatingTabs.AddToClassList("idle-floating-tabs");
			battle.Add(floatingTabs);
			for (int index = 0; index < TAB_NAMES.Length; index++)
			{
				Tab tab = (Tab)index;
				Button button = AddButton(floatingTabs, "idle-box idle-icon-button", () => OpenTab(tab));
				button.text = TAB_NAMES[index];
				button.style.display = TAB_SHOWN[index] ? DisplayStyle.Flex : DisplayStyle.None;
				floatingTabButtons.Add(button);
			}

			// 좌하. 디버그. 에디터와 개발 빌드에서만 (사용자 요청 2026-08-30)
			if (Application.isEditor || Debug.isDebugBuild)
			{
				Button wipe = AddButton(battle, "idle-box idle-icon-button idle-debug", WipeAndRestart);
				wipe.text = "데이터 초기화";
			}
		}

		private void BuildBattleExtras()
		{
			floatingTabs = new VisualElement();
			floatingTabs.AddToClassList("idle-floating-tabs");
			battle.Add(floatingTabs);
			for (int index = 0; index < TAB_NAMES.Length; index++)
			{
				Tab tab = (Tab)index;
				Button button = AddButton(floatingTabs, "idle-box idle-icon-button", () => OpenTab(tab));
				button.text = TAB_NAMES[index];
				button.style.display = TAB_SHOWN[index] ? DisplayStyle.Flex : DisplayStyle.None;
				floatingTabButtons.Add(button);
			}

			if (Application.isEditor || Debug.isDebugBuild)
			{
				Button wipe = AddButton(battle, "idle-box idle-icon-button idle-debug", WipeAndRestart);
				wipe.text = "데이터 초기화";
			}
		}

		private void BindBattleHud()
		{
			TemplateContainer tree = battleHudAsset.Instantiate();
			VisualElement frame = tree.Q<VisualElement>("hud");
			while (frame.childCount > 0)
			{
				battle.Add(frame[0]);
			}

			sceneCover = battle.Q<VisualElement>("scene-cover");
			sceneCover.style.display = DisplayStyle.None;
			sceneCoverLabel = battle.Q<Label>("scene-cover-label");
			battle.Q<Button>("scene-cover-button").clicked += () => OpenTab(Tab.Doll);
			VisualElement op = battle.Q<VisualElement>("op");
			op.RegisterCallback<ClickEvent>(_ => ToggleMap());
			opCode = battle.Q<Label>("op-code");
			opName = battle.Q<Label>("op-name");
			waveDots = battle.Q<VisualElement>("wave-dots");
			waveLabel = battle.Q<Label>("wave-label");
			stepBack = battle.Q<Button>("step-back");
			stepBack.clicked += () => StepStage(-1);
			stepLabel = battle.Q<Label>("step-label");
			stepForward = battle.Q<Button>("step-forward");
			stepForward.clicked += () => StepStage(1);
			repeatButton = battle.Q<Button>("repeat-button");
			repeatButton.clicked += ToggleHold;
			goldChip = battle.Q<Label>("gold-chip");
			pullChip = battle.Q<Label>("pull-chip");
			prestigeChip = battle.Q<Label>("prestige-chip");
			splitButton = battle.Q<Button>("split-button");
			splitButton.clicked += ToggleSplit;
			logLabel = battle.Q<Label>("log-label");
			noteLabel = battle.Q<Label>("note-label");
			enemyBar = battle.Q<VisualElement>("enemy-bar");
			enemyFill = battle.Q<VisualElement>("enemy-fill");
			enemyLabel = battle.Q<Label>("enemy-label");
			failBanner = battle.Q<VisualElement>("fail-banner");
			failBanner.style.display = DisplayStyle.None;
			failLabel = battle.Q<Label>("fail-label");
			nextStageButton = battle.Q<Button>("next-stage-button");
			nextStageButton.clicked += NextStage;
			BindCardButtons(battle.Q<VisualElement>("cards"));
			costLabel = battle.Q<Label>("cost-label");
			costFill = battle.Q<VisualElement>("cost-fill");
		}

		private void BindCardButtons(VisualElement cards)
		{
			cardButtons = new Button[IdleCards.HAND_SIZE];
			for (int index = 0; index < cardButtons.Length; index++)
			{
				int captured = index;
				cardButtons[index] = AddCardButton(cards, () => Cast(captured));
			}
		}

		private Button AddCardButton(VisualElement parent, System.Action clicked)
		{
			if (cardAsset == null)
			{
				return AddButton(parent, "idle-card", clicked);
			}

			TemplateContainer tree = cardAsset.Instantiate();
			Button button = tree.Q<Button>("card");
			button.RemoveFromHierarchy();
			button.clicked += clicked;
			parent.Add(button);
			return button;
		}

		private void BuildSide(VisualElement shell)
		{
			side = new VisualElement();
			side.AddToClassList("idle-side");
			shell.Add(side);

			VisualElement tabs = new VisualElement();
			tabs.AddToClassList("idle-tabs");
			side.Add(tabs);

			for (int index = 0; index < TAB_NAMES.Length; index++)
			{
				Tab tab = (Tab)index;
				Button button = AddButton(tabs, "idle-tab", () => OpenTab(tab));
				button.text = TAB_NAMES[index] + "\n" + TAB_CAPTIONS[index];
				button.style.display = TAB_SHOWN[index] ? DisplayStyle.Flex : DisplayStyle.None;
				tabButtons.Add(button);
			}

			closeSideButton = AddButton(tabs, "idle-tab idle-tab--close", CloseSide);
			closeSideButton.text = "×";

			VisualElement head = new VisualElement();
			head.AddToClassList("idle-panel-head");
			side.Add(head);
			panelTitle = AddLabel(head, "idle-panel-title");
			panelCaption = AddLabel(head, "idle-cap");

			ScrollView body = new ScrollView();
			body.AddToClassList("idle-panel-body");
			side.Add(body);
			panelHost = body.contentContainer;

			pages = new VisualElement[TAB_NAMES.Length];
			BuildDollPage();
			BuildItemPage();
			BuildCodexPage();
			BuildShopPage();
			BuildLabPage();
			BuildDungeonPage();
			BuildInvestPage();
		}

		private VisualElement AddPage(Tab tab)
		{
			VisualElement page = new VisualElement();
			page.AddToClassList("idle-page");
			page.style.display = DisplayStyle.None;
			panelHost.Add(page);
			pages[(int)tab] = page;
			return page;
		}

		/// <summary>인형 탭. 한 화면: 위 편성 6칸, 가운데 성장, 아래 장비 4칸 (layout.md §3).</summary>
		private void BuildDollPage()
		{
			VisualElement page = AddPage(Tab.Doll);

			if (dollPageAsset != null)
			{
				BindDollPage(page);
				return;
			}

			AddLabel(page, "idle-cap").text = "편성";
			VisualElement party = new VisualElement();
			party.AddToClassList("idle-party");
			page.Add(party);

			for (int slot = 0; slot < IdleHeroes.PARTY_SLOTS; slot++)
			{
				int captured = slot;
				Button seat = AddButton(party, "idle-party-seat", () => BeginSeat(captured));
				seat.EnableInClassList("idle-party-seat--sub", IdleHeroes.IsMainSlot(slot) == false);
				partyButtons.Add(seat);
			}

			dollName = AddLabel(page, "idle-row-head");

			AddLabel(page, "idle-cap").text = "강화";
			damageLabel = AddLabel(page, "idle-row-title");
			damageButton = AddButton(page, "idle-row-button", () => Raise(IdleUpgradeKind.Damage));
			speedLabel = AddLabel(page, "idle-row-title");
			speedButton = AddButton(page, "idle-row-button", () => Raise(IdleUpgradeKind.AttackSpeed));
			bulkRaiseButton = AddButton(page, "idle-row-button idle-row-button--strong", RaiseMany);

			AddLabel(page, "idle-cap").text = "장비";
			VisualElement worn = new VisualElement();
			worn.AddToClassList("idle-worn");
			page.Add(worn);
			for (int slot = 0; slot < SLOT_NAMES.Length; slot++)
			{
				int captured = slot;
				Label cell = AddLabel(worn, "idle-worn-cell");
				HookTooltip(cell, () => WornTip(captured));
				wornCells.Add(cell);
			}

			AddLabel(page, "idle-cap").text = "가진 인형";
			heroRows = new VisualElement();
			page.Add(heroRows);
		}

		/// <summary>인형 탭을 UXML 에서. 모양은 에셋, 코드는 이름으로 찾아 값과 클릭만</summary>
		private void BindDollPage(VisualElement page)
		{
			// 에셋의 바깥 틀(idle-side)은 UI Builder 미리보기용. 화면에는 안쪽만
			TemplateContainer tree = dollPageAsset.Instantiate();
			VisualElement frame = tree.Q<VisualElement>("page");
			while (frame.childCount > 0)
			{
				page.Add(frame[0]);
			}

			for (int slot = 0; slot < IdleHeroes.PARTY_SLOTS; slot++)
			{
				int captured = slot;
				Button seat = page.Q<Button>("seat-" + slot);
				seat.clicked += () => BeginSeat(captured);
				partyButtons.Add(seat);
			}

			dollName = page.Q<Label>("doll-name");
			damageLabel = page.Q<Label>("damage-label");
			damageButton = page.Q<Button>("damage-button");
			damageButton.clicked += () => Raise(IdleUpgradeKind.Damage);
			speedLabel = page.Q<Label>("speed-label");
			speedButton = page.Q<Button>("speed-button");
			speedButton.clicked += () => Raise(IdleUpgradeKind.AttackSpeed);
			bulkRaiseButton = page.Q<Button>("bulk-button");
			bulkRaiseButton.clicked += RaiseMany;

			for (int slot = 0; slot < SLOT_NAMES.Length; slot++)
			{
				int captured = slot;
				Label cell = page.Q<Label>("worn-" + slot);
				HookTooltip(cell, () => WornTip(captured));
				wornCells.Add(cell);
			}

			heroRows = page.Q<VisualElement>("hero-rows");
		}

		private void BuildItemPage()
		{
			VisualElement page = AddPage(Tab.Item);
			if (itemPageAsset != null)
			{
				BindItemPage(page);
				return;
			}

			VisualElement subs = new VisualElement();
			AddClasses(subs, "idle-subtabs");
			page.Add(subs);
			itemSubButtons[0] = AddButton(subs, "idle-subtab", () => OpenItemSub(0));
			itemSubButtons[0].text = "가방";
			itemSubButtons[1] = AddButton(subs, "idle-subtab", () => OpenItemSub(1));
			itemSubButtons[1].text = "공방";

			// 가방
			bagView = new VisualElement();
			page.Add(bagView);

			gearSummary = AddLabel(bagView, "idle-row-title");

			bagGrid = new VisualElement();
			bagGrid.AddToClassList("idle-bag");
			bagView.Add(bagGrid);

			for (int index = 0; index < 40; index++)
			{
				int captured = index;
				Button cell = AddButton(bagGrid, "idle-bag-cell", () => Equip(captured));
				HookTooltip(cell, () => BagTip(captured));
				bagCells.Add(cell);
			}

			bulkMergeButton = AddButton(bagView, "idle-row-button idle-row-button--strong", MergeAll);

			// 공방
			forgeView = new VisualElement();
			page.Add(forgeView);

			AddLabel(forgeView, "idle-cap").text = "합칠 것을 고른다";
			forgeKinds = new VisualElement();
			AddClasses(forgeKinds, "idle-forge-kinds");
			forgeView.Add(forgeKinds);

			VisualElement bench = new VisualElement();
			AddClasses(bench, "idle-forge-bench");
			forgeView.Add(bench);

			VisualElement grid = new VisualElement();
			AddClasses(grid, "idle-forge-grid");
			bench.Add(grid);
			for (int index = 0; index < 9; index++)
			{
				forgeCells.Add(AddLabel(grid, "idle-forge-cell"));
			}

			VisualElement outcome = new VisualElement();
			AddClasses(outcome, "idle-forge-outcome");
			bench.Add(outcome);
			AddLabel(outcome, "idle-forge-arrow").text = "→";
			forgeResult = AddLabel(outcome, "idle-forge-cell idle-forge-cell--result");

			forgeTitle = AddLabel(forgeView, "idle-row-title");
			forgeButton = AddButton(forgeView, "idle-row-button idle-row-button--strong idle-row-button--tall", MergeForge);

			// 감정(잠재)은 알파 뒤. 자리만, 숨김
			appraiseCap = AddLabel(page, "idle-cap");
			appraiseCap.text = "감정";
			appraiseCap.style.display = DisplayStyle.None;
			appraiseRows = new VisualElement();
			appraiseRows.style.display = DisplayStyle.None;
			page.Add(appraiseRows);

			OpenItemSub(0);
		}

		/// <summary>아이템 탭을 UXML 에서. 가방과 공방의 수량만 코어 사진으로 채운다</summary>
		private void BindItemPage(VisualElement page)
		{
			// 에셋의 바깥 틀(idle-side)은 UI Builder 미리보기용. 화면에는 안쪽만
			TemplateContainer tree = itemPageAsset.Instantiate();
			VisualElement frame = tree.Q<VisualElement>("page");
			while (frame.childCount > 0)
			{
				page.Add(frame[0]);
			}

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
				Button cell = AddBagCell(bagGrid, () => Equip(captured));
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
			if (bagCellAsset == null)
			{
				return AddButton(parent, "idle-bag-cell", clicked);
			}

			TemplateContainer tree = bagCellAsset.Instantiate();
			Button cell = tree.Q<Button>("bag-cell");
			cell.RemoveFromHierarchy();
			cell.clicked += clicked;
			parent.Add(cell);
			return cell;
		}

		private Button AddForgeKind(VisualElement parent, System.Action clicked)
		{
			if (forgeKindAsset == null)
			{
				return AddButton(parent, "idle-forge-kind", clicked);
			}

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
			VisualElement page = AddPage(Tab.Codex);
			codexLabel = AddLabel(page, "idle-row-title");
			codexRows = new VisualElement();
			page.Add(codexRows);
		}

		private void BuildShopPage()
		{
			VisualElement page = AddPage(Tab.Shop);

			VisualElement banner = new VisualElement();
			banner.AddToClassList("idle-banner");
			page.Add(banner);
			AddLabel(banner, "idle-banner-title").text = "인형 뽑기";
			AddLabel(banner, "idle-cap").text = "PICK UP";

			pullButton = AddButton(page, "idle-row-button idle-row-button--strong idle-row-button--tall", Pull);
			pullOdds = AddLabel(page, "idle-row-note");
			AddLabel(page, "idle-row-note").text = "현금 결제 없음. 뽑기 재화는 첫 클리어, 환생, 낮은 확률 드롭에서만.";
			AddLabel(page, "idle-cap").text = "무료 상자";
		}

		private void BuildLabPage()
		{
			VisualElement page = AddPage(Tab.Lab);
			prestigeSummary = AddLabel(page, "idle-row-title");
			prestigeButton = AddButton(page, "idle-row-button idle-row-button--strong idle-row-button--tall", Prestige);
		}

		private void BuildDungeonPage()
		{
			VisualElement page = AddPage(Tab.Dungeon);
			string[] names = { "재화 던전", "보스 던전", "장비 던전", "스킬 던전" };
			for (int index = 0; index < names.Length; index++)
			{
				Button row = AddButton(page, "idle-row-button", null);
				row.text = names[index] + ". 입장권 0/0 (알파 9번. 자리만)";
				row.SetEnabled(false);
			}
		}

		private void BuildInvestPage()
		{
			VisualElement page = AddPage(Tab.Invest);
			baseSummary = AddLabel(page, "idle-row-title");
			bulkBuyButton = AddButton(page, "idle-row-button idle-row-button--strong", BuyMany);

			for (int kind = 0; kind < 8; kind++)
			{
				int captured = kind;
				producerButtons.Add(AddButton(page, "idle-row-button", () => BuyProducer(captured)));
			}
		}

		private void BuildMapPopup()
		{
			mapPopup = new VisualElement();
			AddClasses(mapPopup, "idle-box idle-map");
			mapPopup.style.display = DisplayStyle.None;
			battle.Add(mapPopup);

			VisualElement head = new VisualElement();
			head.AddToClassList("idle-panel-head");
			mapPopup.Add(head);
			AddLabel(head, "idle-panel-title").text = "맵";
			AddLabel(head, "idle-cap").text = "MAP";
			AddButton(head, "idle-tab idle-tab--close", ToggleMap).text = "×";

			mapRows = new VisualElement();
			mapPopup.Add(mapRows);
		}

		// ── 그리기 ────────────────────────────────────────────────────────

		public void Render(IdleSnapshot snapshot)
		{
			if (built == false)
			{
				return;
			}

			opCode.text = "S-" + snapshot.Stage;
			opName.text = string.Format("등급 {0}/{1}", snapshot.MaxTierNow, snapshot.TierCeiling);

			stepLabel.text = string.Format("{0}구역", snapshot.Stage);
			stepBack.SetEnabled(IdleModel.CanGoToStage(session.State, snapshot.Stage - 1));
			stepForward.SetEnabled(snapshot.Stage < snapshot.BestStage
				&& IdleModel.CanGoToStage(session.State, snapshot.Stage + 1));

			repeatButton.text = snapshot.HoldingStage ? "반복 ●" : "반복 ○";
			repeatButton.EnableInClassList("idle-toggle--on", snapshot.HoldingStage);

			goldChip.text = string.Format("골드 {0}  +{1}/s",
				BigNumberText.Format(snapshot.Resource), BigNumberText.Format(snapshot.IncomePerSecond));
			pullChip.text = string.Format("뽑기 {0}", snapshot.Stones);
			prestigeChip.text = string.Format("환생 조각 {0}", snapshot.PrestigePoints);

			logLabel.text = NextStep(snapshot);

			RenderEnemy(snapshot);
			RenderHand(snapshot);
			RenderFail(snapshot);
			RenderTabBadges(snapshot);

			if (mapPopup.style.display == DisplayStyle.Flex)
			{
				RenderMap(snapshot);
			}

			if (split || sideOpen)
			{
				RenderPage(snapshot);
			}
		}

		private void RenderEnemy(IdleSnapshot snapshot)
		{
			// 상단 대형 바는 보스 전용 (실조사 refs/blue-archive.md § 2, 6). 잡몹 체력은 머리 위
			bool boss = snapshot.KillsInStage >= snapshot.KillsPerStage - 1;
			enemyBar.style.display = boss ? DisplayStyle.Flex : DisplayStyle.None;

			if (boss)
			{
				enemyLabel.text = string.Format("BOSS S-{0}  {1:P0}", snapshot.Stage, snapshot.TargetHealthRatio);
				enemyFill.style.width = new StyleLength(new Length(
					(float)(snapshot.TargetHealthRatio * 100d), LengthUnit.Percent));
			}

			if (waveDotList.Count != snapshot.KillsPerStage)
			{
				waveDots.Clear();
				waveDotList.Clear();

				for (int at = 0; at < snapshot.KillsPerStage; at++)
				{
					VisualElement dot = AddWaveDot();
					dot.EnableInClassList("idle-wave-dot--boss", at == snapshot.KillsPerStage - 1);
					waveDots.Add(dot);
					waveDotList.Add(dot);
				}
			}

			for (int at = 0; at < waveDotList.Count; at++)
			{
				waveDotList[at].EnableInClassList("idle-wave-dot--done", at < snapshot.KillsInStage);
			}

			waveLabel.text = string.Format("WAVE {0}/{1}", snapshot.KillsInStage, snapshot.KillsPerStage);
		}

		private VisualElement AddWaveDot()
		{
			if (waveDotAsset == null)
			{
				VisualElement dot = new VisualElement();
				dot.AddToClassList("idle-wave-dot");
				return dot;
			}

			TemplateContainer tree = waveDotAsset.Instantiate();
			VisualElement made = tree.Q<VisualElement>("wave-dot");
			made.RemoveFromHierarchy();
			return made;
		}

		private void RenderHand(IdleSnapshot snapshot)
		{
			for (int index = 0; index < cardButtons.Length; index++)
			{
				IdleCardView card = snapshot.Cards[index];
				cardButtons[index].text = string.Format("{0}\n{1}", card.Cost, NameOf(card.Kind));
				cardButtons[index].SetEnabled(card.CanCast);
				cardButtons[index].EnableInClassList("idle-card--ready", card.CanCast);
			}

			costLabel.text = string.Format("{0:0}/{1:0}", snapshot.Cost, snapshot.CostMax);
			costFill.style.width = new StyleLength(new Length(
				snapshot.CostMax > 0d ? (float)(snapshot.Cost / snapshot.CostMax * 100d) : 0f,
				LengthUnit.Percent));
		}

		private void RenderFail(IdleSnapshot snapshot)
		{
			failBanner.style.display = snapshot.Repeating ? DisplayStyle.Flex : DisplayStyle.None;

			if (snapshot.Repeating)
			{
				failLabel.text = string.Format("전멸. {0}구역을 반복하는 중. 채비가 되면 다시 내려간다", snapshot.Stage);
				nextStageButton.text = string.Format("{0}구역에 다시 도전 ▶", snapshot.Stage + 1);
			}
		}

		private void RenderTabBadges(IdleSnapshot snapshot)
		{
			bool doll = IdleAdvice.HasSomethingToDo(snapshot, IdleTab.Hero)
				|| IdleAdvice.HasSomethingToDo(snapshot, IdleTab.Upgrade);
			bool item = IdleAdvice.HasSomethingToDo(snapshot, IdleTab.Gear);
			bool shop = snapshot.CanPull;
			bool lab = snapshot.PrestigeAward > 0L;
			bool invest = IdleAdvice.HasSomethingToDo(snapshot, IdleTab.Base);

			SetBadge(Tab.Doll, doll);
			SetBadge(Tab.Item, item);
			SetBadge(Tab.Shop, shop);
			SetBadge(Tab.Lab, lab);
			SetBadge(Tab.Invest, invest);

			for (int index = 0; index < tabButtons.Count; index++)
			{
				tabButtons[index].EnableInClassList("idle-tab--on", index == (int)openTab && (split || sideOpen));
			}
		}

		private void SetBadge(Tab tab, bool on)
		{
			tabButtons[(int)tab].EnableInClassList("idle-tab--badge", on);
			floatingTabButtons[(int)tab].EnableInClassList("idle-tab--badge", on);
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
				default: break;
			}
		}

		private void RenderDollPage(IdleSnapshot snapshot)
		{
			for (int slot = 0; slot < partyButtons.Count; slot++)
			{
				int id = slot < snapshot.Party.Length ? snapshot.Party[slot] : -1;
				string tag = IdleHeroes.IsMainSlot(slot) ? "전투" : "지원";
				partyButtons[slot].text = id >= 0
					? tag + "\n" + IdleHeroes.KindOf(id).Name
					: tag + "\n+";
				partyButtons[slot].EnableInClassList("idle-party-seat--picking", seatBeingFilled == slot);
			}

			dollName.text = seatBeingFilled >= 0 ? "아래에서 인형을 고른다" : string.Empty;

			DrawUpgrade(snapshot.Damage, damageLabel, damageButton, "공격력");
			DrawUpgrade(snapshot.AttackSpeed, speedLabel, speedButton, "공격 속도");

			bool canRaise = IdleModel.CheapestRaisableAxis(session.State, session.Tuning, out IdleUpgradeKind _);
			bulkRaiseButton.text = "전부 올리기";
			bulkRaiseButton.SetEnabled(canRaise);

			for (int slot = 0; slot < wornCells.Count && slot < snapshot.Worn.Length; slot++)
			{
				IdleItem one = snapshot.Worn[slot];
				wornCells[slot].text = SLOT_NAMES[slot] + "\n" + (one.IsEmpty ? "없음" : one.Tier + "단계");
				wornCells[slot].EnableInClassList("idle-worn-cell--empty", one.IsEmpty);
				SetTierClass(wornCells[slot], one.IsEmpty ? 0 : one.Tier);
			}

			if (heroButtons.Count != snapshot.Heroes.Length)
			{
				heroRows.Clear();
				heroButtons.Clear();

				for (int index = 0; index < snapshot.Heroes.Length; index++)
				{
					int id = snapshot.Heroes[index].Id;
					heroButtons.Add(AddButton(heroRows, "idle-row-button", () => ChooseHero(id)));
				}
			}

			for (int index = 0; index < heroButtons.Count && index < snapshot.Heroes.Length; index++)
			{
				IdleHeroView hero = snapshot.Heroes[index];
				heroButtons[index].text = string.Format("{0}{1}{2}",
					hero.Name, Stars(hero.Stars),
					hero.InParty ? "   편성 중" : string.Empty);
			}
		}

		private void RenderItemPage(IdleSnapshot snapshot)
		{
			bool full = snapshot.Bag.Length >= snapshot.BagCapacity;
			gearSummary.text = string.Format("가방 {0}/{1}{2}",
				snapshot.Bag.Length, snapshot.BagCapacity,
				full ? "  꽉 찼다. 합치거나 차야 새 장비가 들어온다" : string.Empty);
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
					bagCells[index].SetEnabled(false);
					SetTierClass(bagCells[index], 0);
					continue;
				}

				IdleItem one = snapshot.Bag[index];
				bagCells[index].text = string.Format("{0}\n{1}단계", SLOT_NAMES[(int)one.Slot], one.Tier);
				bagCells[index].SetEnabled(true);
				SetTierClass(bagCells[index], one.Tier);
			}

			bulkMergeButton.text = string.Format("{0}개씩 전부 합치기", snapshot.MergeCount);
			bulkMergeButton.SetEnabled(IdleAdvice.MergeableCount(snapshot) > 0);

			RenderForge(snapshot);
			RenderAppraise(snapshot);
		}

		/// <summary>공방. 가방에 있는 종류(부위, 단계)를 칩으로 늘어놓고, 고른 종류를 3×3 에 채운다</summary>
		private void RenderForge(IdleSnapshot snapshot)
		{
			int room = (snapshot.TierCeiling + 2) * IdleGear.SLOT_COUNT;
			int[] counts = new int[room];

			for (int index = 0; index < snapshot.Bag.Length; index++)
			{
				IdleItem one = snapshot.Bag[index];
				int key = one.Tier * IdleGear.SLOT_COUNT + (int)one.Slot;
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
				int tier = key / IdleGear.SLOT_COUNT;
				IdleItemSlot slot = (IdleItemSlot)(key % IdleGear.SLOT_COUNT);
				forgeKindButtons[index].text = string.Format("{0} {1}단계 ×{2}", SLOT_NAMES[(int)slot], tier, counts[key]);
				SetTierClass(forgeKindButtons[index], tier);
				forgeKindButtons[index].EnableInClassList("idle-forge-kind--on", forgeTier == tier && forgeSlot == slot);
			}

			int forgeKey = forgeTier * IdleGear.SLOT_COUNT + (int)forgeSlot;
			int have = forgeTier > 0 && forgeKey < counts.Length ? counts[forgeKey] : 0;
			int shown = have > snapshot.MergeCount ? snapshot.MergeCount : have;

			for (int index = 0; index < forgeCells.Count; index++)
			{
				bool filled = index < shown;
				forgeCells[index].text = filled ? SLOT_NAMES[(int)forgeSlot] + "\n" + forgeTier + "단계" : string.Empty;
				SetTierClass(forgeCells[index], filled ? forgeTier : 0);
			}

			bool ready = forgeTier > 0 && have >= snapshot.MergeCount;
			forgeResult.text = forgeTier > 0 ? SLOT_NAMES[(int)forgeSlot] + "\n" + (forgeTier + 1) + "단계" : string.Empty;
			SetTierClass(forgeResult, forgeTier > 0 ? forgeTier + 1 : 0);
			forgeResult.EnableInClassList("idle-forge-cell--ready", ready);

			forgeTitle.text = forgeTier > 0
				? string.Format("{0} {1}단계  {2}/{3}", SLOT_NAMES[(int)forgeSlot], forgeTier, have, snapshot.MergeCount)
				: string.Format("같은 부위, 같은 단계 {0}개가 한 단계 위로", snapshot.MergeCount);
			forgeButton.text = "합치기";
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
			forgeTier = key / IdleGear.SLOT_COUNT;
			forgeSlot = (IdleItemSlot)(key % IdleGear.SLOT_COUNT);
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
					appraiseButtons.Add(AddButton(appraiseRows, "idle-row-button", () => Appraise(captured)));
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
					codexLabels.Add(AddLabel(codexRows, "idle-row-title"));
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
						IdleHeroes.NameOfGrade(kind.Grade), IdleHeroes.NameOfAxis(kind.Axis))
					: string.Format("???  {0}", IdleHeroes.NameOfGrade(kind.Grade));
				codexLabels[id].EnableInClassList("idle-row-title--dim", owned == false);
			}
		}

		private void RenderShopPage(IdleSnapshot snapshot)
		{
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

			bool canBuy = IdleBase.CheapestAffordable(session.State, session.Tuning) >= 0;
			bulkBuyButton.text = canBuy ? "싼 것부터 몰아 산다" : "살 수 있는 게 없다";
			bulkBuyButton.SetEnabled(canBuy);

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
					mapButtons.Add(AddButton(mapRows, "idle-row-button", () => GoToStage(target)));
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
			if (snapshot.Repeating)
			{
				return "전멸. 인형과 아이템을 손보고 다시 도전한다";
			}

			IdleAdviceResult advice = IdleAdvice.NextStep(snapshot);

			switch (advice.Step)
			{
				case IdleStep.Prestige: return string.Format("연구소. 환생할 때다 (+{0})", (long)advice.Amount);
				case IdleStep.BuyProducer: return "투자. 살 것이 있다";
				case IdleStep.Raise: return "인형. 올릴 것이 있다";
				case IdleStep.Merge: return "아이템. 합칠 수 있다";
				case IdleStep.Wear: return "아이템. 가방에 더 좋은 것이 있다";
				case IdleStep.Pull: return "상점. 뽑을 수 있다";
				case IdleStep.Seat: return "인형. 편성 칸이 비었다";
				case IdleStep.BagFull: return "아이템. 가방이 꽉 찼다";
				case IdleStep.Tap: return "무대를 눌러 응원한다";
				default:
					return advice.Amount > 0d && double.IsInfinity(advice.Amount) == false
						? string.Format("모으는 중. {0} 뒤에 살 것이 생긴다", DescribeSpan(advice.Amount))
						: "모으는 중. 코스트가 차면 카드를 낸다";
			}
		}

		// ── 화면 상태 ─────────────────────────────────────────────────────

		private void OpenTab(Tab tab)
		{
			openTab = tab;
			seatBeingFilled = -1;
			sideOpen = true;

			for (int index = 0; index < pages.Length; index++)
			{
				pages[index].style.display = index == (int)tab ? DisplayStyle.Flex : DisplayStyle.None;
			}

			panelTitle.text = TAB_NAMES[(int)tab];
			panelCaption.text = TAB_CAPTIONS[(int)tab];

			// 상점, 연구소는 왼쪽 씬이 바뀐다 (layout.md §2). 지금은 덮개
			bool altScene = tab == Tab.Shop || tab == Tab.Lab;
			sceneCover.style.display = altScene ? DisplayStyle.Flex : DisplayStyle.None;
			sceneCoverLabel.text = tab == Tab.Shop ? "SHOP 3D SCENE 자리" : "LAB 3D SCENE 자리";
			battle.EnableInClassList("idle-battle--alt", altScene);

			ApplySplit();
			Render(session.Capture());
		}

		private void CloseSide()
		{
			sideOpen = false;
			sceneCover.style.display = DisplayStyle.None;
			battle.EnableInClassList("idle-battle--alt", false);
			ApplySplit();
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
			bool showSide = split || sideOpen;
			side.style.display = showSide ? DisplayStyle.Flex : DisplayStyle.None;
			side.EnableInClassList("idle-side--drawer", split == false);
			closeSideButton.style.display = split ? DisplayStyle.None : DisplayStyle.Flex;
			floatingTabs.style.display = split ? DisplayStyle.None : DisplayStyle.Flex;
			battle.EnableInClassList("idle-battle--full", split == false);
			splitButton.text = split ? "풀화면" : "분할";

			// 무대 카메라는 전투 창 폭만. 아니면 부대가 화면 전체 가운데(관리 열 밑)에 위치
			Camera main = Camera.main;
			if (main != null)
			{
				main.rect = split ? new Rect(0f, 0f, BATTLE_SHARE, 1f) : new Rect(0f, 0f, 1f, 1f);
			}

			if (showSide)
			{
				for (int index = 0; index < pages.Length; index++)
				{
					pages[index].style.display = index == (int)openTab ? DisplayStyle.Flex : DisplayStyle.None;
				}

				panelTitle.text = TAB_NAMES[(int)openTab];
				panelCaption.text = TAB_CAPTIONS[(int)openTab];
			}
		}

		private void ToggleMap()
		{
			bool open = mapPopup.style.display != DisplayStyle.Flex;
			mapPopup.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
			Render(session.Capture());
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

			skipSaveOnce = true;
			IdleSaveStore.Wipe();
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
			if (session.TryCastCard(handIndex, out IdleCardResult result) == false)
			{
				return;
			}

			IdleCardKind kind = result.Kind;

			switch (kind)
			{
				case IdleCardKind.Volley:
					if (stage != null) { stage.OnVolley(); }
					SayOnce("일제 사격. 모두 달려들었다", noteSeconds);
					break;

				case IdleCardKind.Supply:
					if (stage != null) { stage.OnSupply((float)session.Tuning.SupplySeconds); }
					SayOnce(string.Format("긴급 보급. {0:0}초 동안 수입 ×{1:0.#}",
						session.Tuning.SupplySeconds, session.Tuning.SupplyMultiplier), noteSeconds);
					break;

				default:
					SayOnce(result.HasRoll
						? string.Format("비밀 감정. T{0} → {1:P1}{2}",
							result.Roll.Tier, result.Roll.Value, result.Roll.Replaced ? " 갈아 끼움" : string.Empty)
						: "비밀 감정. 굴릴 것이 없다", noteSeconds);
					break;
			}

			WriteDown();
			Render(session.Capture());
		}

		private void NextStage()
		{
			if (session.Send(new IdleNextStageIntent()))
			{
				SayOnce("다시 내려간다. 부대는 만전이다", noteSeconds);
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

		private void Raise(IdleUpgradeKind kind)
		{
			session.Send(new IdleRaiseUpgradeIntent(kind));
			Render(session.Capture());
		}

		private void RaiseMany()
		{
			int raised = session.RaiseAsManyAsAfforded();
			if (raised > 0)
			{
				SayOnce(string.Format("강화. {0}번 올렸다", raised), noteSeconds);
				WriteDown();
			}

			Render(session.Capture());
		}

		private void BuyProducer(int kind)
		{
			session.Send(new IdleBuyProducerIntent(kind));
			Render(session.Capture());
		}

		private void BuyMany()
		{
			int bought = session.BuyAsManyProducersAsAfforded();
			if (bought > 0)
			{
				SayOnce(string.Format("투자. {0}개 샀다", bought), noteSeconds);
				WriteDown();
			}

			Render(session.Capture());
		}

		private void Equip(int bagIndex)
		{
			session.Send(new IdleEquipIntent(bagIndex));
			WriteDown();
			Render(session.Capture());
		}

		private void Merge(int tier, IdleItemSlot slot)
		{
			if (session.Send(new IdleMergeIntent(tier, slot)))
			{
				SayOnce(string.Format("{0} {1}단계 → {2}단계", SLOT_NAMES[(int)slot], tier, tier + 1), noteSeconds);
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
				for (int slot = 0; slot < IdleGear.SLOT_COUNT; slot++)
				{
					while (session.Send(new IdleMergeIntent(tier, (IdleItemSlot)slot)))
					{
						merged++;
					}
				}
			}

			if (merged > 0)
			{
				SayOnce(string.Format("{0}번 합쳤다", merged), noteSeconds);
				WriteDown();
			}

			Render(session.Capture());
		}

		private void Appraise(int tier)
		{
			if (session.TryAppraise(tier, out PotentialRoll roll))
			{
				SayOnce(string.Format("T{0} 감정 → {1:P1}{2}",
					roll.Tier, roll.Value, roll.Replaced ? " 갈아 끼움" : string.Empty), noteSeconds);
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
				IdleHeroes.NameOfGrade(got.Grade), kind.Name,
				got.IsNew ? ". 처음 본 얼굴" : string.Empty,
				got.ByPity ? " (천장)" : string.Empty), noteSeconds * 2f);

			WriteDown();
			Render(session.Capture());
		}

		private void BeginSeat(int slot)
		{
			seatBeingFilled = seatBeingFilled == slot ? -1 : slot;
			Render(session.Capture());
		}

		private void ChooseHero(int id)
		{
			int slot = seatBeingFilled;

			if (slot < 0)
			{
				slot = FirstEmptySeat();
			}

			if (slot < 0)
			{
				SayOnce("자리가 다 찼다. 바꿀 칸을 먼저 누른다", noteSeconds);
				Render(session.Capture());
				return;
			}

			session.Send(new IdleSetPartyIntent(slot, id));
			seatBeingFilled = -1;
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
				SayOnce("환생. 새 종이. 코스트는 그대로다", noteSeconds * 2f);
				WriteDown();
			}

			Render(session.Capture());
		}

		// ── 툴팁 ───────────────────────────────────────────────────────────

		/// <summary>마우스를 올리면 뜨는 설명. PC 우선 (layout.md §1). 글은 부르는 쪽이 만든다</summary>
		private void HookTooltip(VisualElement target, System.Func<string> text)
		{
			target.RegisterCallback<PointerEnterEvent>(_ => ShowTooltip(text()));
			target.RegisterCallback<PointerMoveEvent>(moment => MoveTooltip(moment.position));
			target.RegisterCallback<PointerLeaveEvent>(_ => tooltip.style.display = DisplayStyle.None);
		}

		private void ShowTooltip(string text)
		{
			if (string.IsNullOrEmpty(text))
			{
				tooltip.style.display = DisplayStyle.None;
				return;
			}

			tooltip.text = text;
			tooltip.style.display = DisplayStyle.Flex;
		}

		private void MoveTooltip(Vector2 at)
		{
			VisualElement root = tooltip.parent;
			float x = at.x + 18f;
			float y = at.y + 18f;

			if (root != null && x + 320f > root.resolvedStyle.width)
			{
				x = at.x - 330f;
			}

			if (root != null && y + 120f > root.resolvedStyle.height)
			{
				y = at.y - 130f;
			}

			tooltip.style.left = x;
			tooltip.style.top = y;
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
				SLOT_NAMES[(int)one.Slot], one.Tier,
				IdleGear.MultiplierOfItem(one, session.Tuning),
				wearing.IsEmpty ? "없음" : wearing.Tier + "단계 ×" + IdleGear.MultiplierOfItem(wearing, session.Tuning).ToString("0.00"));
		}

		private string WornTip(int slot)
		{
			IdleSnapshot now = session.Capture();
			IdleItem one = now.Worn[slot];
			return one.IsEmpty
				? SLOT_NAMES[slot] + "\n비었다. 아이템 탭에서 찬다"
				: string.Format("{0} {1}단계\n효과 ×{2:0.00}", SLOT_NAMES[slot], one.Tier, IdleGear.MultiplierOfItem(one, session.Tuning));
		}

		/// <summary>단계 색. 클래스 idle-tier-N, 색은 USS (울티마식 단계 고유색. 사용자 2026-08-30)</summary>
		private static void SetTierClass(VisualElement element, int tier)
		{
			for (int at = 1; at <= 8; at++)
			{
				element.EnableInClassList("idle-tier-" + at, at == tier);
			}
		}

		// ── 잔손 ──────────────────────────────────────────────────────────

		private void SayOnce(string what, float seconds)
		{
			noteLabel.text = what;
			noteLabel.style.opacity = 1f;
			noteLeft = seconds;
		}

		private static void DrawUpgrade(IdleUpgradeView view, Label label, Button button, string name)
		{
			label.text = string.Format("{0} {1}  Lv.{2}", name, BigNumberText.Format(view.CurrentValue), view.Level);

			button.text = view.IsMaxed
				? "최대"
				: string.Format("올리기  {0} 골드", BigNumberText.Format(view.NextCost));
			button.SetEnabled(view.CanAfford);
		}

		private static string Stars(int stars)
		{
			return stars <= 0 ? string.Empty : " " + new string('★', stars);
		}

		private static string NameOf(IdleCardKind kind)
		{
			switch (kind)
			{
				case IdleCardKind.Volley: return "일제 사격";
				case IdleCardKind.Supply: return "긴급 보급";
				default: return "비밀 감정";
			}
		}

		private static void AddClasses(VisualElement element, string classNames)
		{
			foreach (string one in classNames.Split(' '))
			{
				element.AddToClassList(one);
			}
		}

		private static Label AddLabel(VisualElement parent, string classNames)
		{
			Label label = new Label(string.Empty);
			foreach (string one in classNames.Split(' '))
			{
				label.AddToClassList(one);
			}

			parent.Add(label);
			return label;
		}

		private static Button AddButton(VisualElement parent, string classNames, System.Action action)
		{
			Button button = action != null ? new Button(action) : new Button();
			foreach (string one in classNames.Split(' '))
			{
				button.AddToClassList(one);
			}

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
