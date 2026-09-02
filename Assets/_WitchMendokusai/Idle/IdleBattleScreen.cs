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
		[SerializeField] private VisualTreeAsset codexPageAsset;
		[SerializeField] private VisualTreeAsset shopPageAsset;
		[SerializeField] private VisualTreeAsset labPageAsset;
		[SerializeField] private VisualTreeAsset dungeonPageAsset;
		[SerializeField] private VisualTreeAsset investPageAsset;
		[SerializeField] private VisualTreeAsset producerRowAsset;
		[SerializeField] private VisualTreeAsset gearPopupAsset;

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

		private static readonly string[] TAB_NAMES = { "인형", "아이템", "도감", "상점", "연구소", "던전", "투자" };
		private static readonly string[] TAB_CAPTIONS = { "DOLLS", "ITEMS", "CODEX", "SHOP", "LAB", "DUNGEON", "INVEST" };
		private static readonly string[] SLOT_NAMES = { "머리", "몸", "손", "발" };
		private static readonly string[] STAT_NAMES = { "공격력", "공격 속도", "최대 체력", "방어력", "치명타 확률", "치명타 피해" };
		private static readonly int[] STAT_AMOUNTS = { 1, 10, 100 };

		/// <summary>전투 창이 차지하는 폭. 1200 / 1920 (layout.md §1)</summary>
		private const float BATTLE_SHARE = 1200f / 1920f;

		/// <summary>지금 보이는 탭. 나머지는 임시로 숨김 (사용자 2026-08-30, 개발 편의. 코드는 유지)</summary>
		private static readonly bool[] TAB_SHOWN = { true, true, false, true, false, false, false };

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
		private Button[] dungeonRows;
		private Label[] queueChips;
		private int volleyHandIndex = -1;
		private VisualElement costFill;
		private Label costLabel;

		private VisualElement floatingTabs;
		private readonly List<Button> floatingTabButtons = new List<Button>();

		// ── 관리 열 ───────────────────────────────────────────────────────
		/// <summary>UI 뿌리. 폭을 재서 무대 카메라를 맞춘다</summary>
		private VisualElement root;

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

		/// <summary>장비를 볼 인형의 편성 자리 (2026-08-31 인형별 장비). 찬 편성 칸을 누르면 바뀐다</summary>
		private int gearSeat;

		/// <summary>그 자리의 인형 번호. 빈 자리면 -1</summary>
		private int gearHeroId => session != null && gearSeat >= 0 && gearSeat < session.State.Party.Length
			? session.State.Party[gearSeat]
			: -1;

		/// <summary>한 인형의 장비 넷. 매 프레임 새 배열을 안 만들려고 들고 있는다</summary>
		private readonly IdleItem[] gearOfHero = new IdleItem[IdleGear.SLOT_COUNT];
		private Label dollName;
		private readonly Label[] statLabels = new Label[6];
		private readonly Button[,] statButtons = new Button[6, 3];
		private Button speedCycleButton;
		private Button autoCastButton;
		private readonly List<Button> wornCells = new List<Button>();
		private Button openHeroPopupButton;
		private VisualElement heroPopup;
		private VisualElement heroGrid;
		private readonly List<Button> heroChoiceButtons = new List<Button>();

		// 장비 고르기 팝업 (사용자 2026-08-31). 인형이 여럿이라 가방에서 바로 장착하면 대상이 불명
		private VisualElement gearPopup;
		private Label gearTitle;
		private Label gearWorn;
		private VisualElement gearRows;
		private readonly List<Button> gearRowButtons = new List<Button>();
		private int gearSlot = -1;

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

			// 배치 빌드에서는 아무것도 안 세운다 (실측 2026-09-01: 20회 연속 빌드 실패).
			// 씬 검사(IdleSceneBuilder.Verify)가 씬을 열면 [ExecuteAlways] 때문에 여기가 돌고,
			// -nographics 배치에는 카메라도 패널도 없음. 빌드가 Unknown 으로 사망
			if (Application.isBatchMode)
			{
				return;
			}

			IdleTuning tuning = tuningAsset != null ? tuningAsset.ToTuning() : new IdleTuning();
			preview = Application.isPlaying == false;

			IdleState state = new IdleState();
			IdleAwayReport away = default;

			if (preview)
			{
#if UNITY_EDITOR
				previewTicked = false;
#endif
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

		/// <summary>안 꽂힌 화면 에셋 이름. 전부 있으면 거짓</summary>
		private bool MissingAsset(out string what)
		{
			what = string.Empty;

			if (battleHudAsset == null) { what = "battleHudAsset"; }
			else if (cardAsset == null) { what = "cardAsset"; }
			else if (waveDotAsset == null) { what = "waveDotAsset"; }
			else if (dollPageAsset == null) { what = "dollPageAsset"; }
			else if (itemPageAsset == null) { what = "itemPageAsset"; }
			else if (bagCellAsset == null) { what = "bagCellAsset"; }
			else if (forgeKindAsset == null) { what = "forgeKindAsset"; }
			else if (codexPageAsset == null) { what = "codexPageAsset"; }
			else if (shopPageAsset == null) { what = "shopPageAsset"; }
			else if (labPageAsset == null) { what = "labPageAsset"; }
			else if (dungeonPageAsset == null) { what = "dungeonPageAsset"; }
			else if (investPageAsset == null) { what = "investPageAsset"; }
			else if (producerRowAsset == null) { what = "producerRowAsset"; }
			else if (gearPopupAsset == null) { what = "gearPopupAsset"; }

			return what.Length > 0;
		}

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
			// UIDocument OnEnable 전 호출 방어. 이전 판 완료 상태로 빈 라벨에 닿지 않게
			built = false;
			UIDocument document = GetComponent<UIDocument>();
			if (document == null || document.rootVisualElement == null)
			{
				return;
			}

			this.root = document.rootVisualElement;
			VisualElement root = this.root;
			root.Clear();

			// 창 크기가 바뀌면 무대 폭도 다시 (모바일 회전, PC 창 조절)
			root.RegisterCallback<GeometryChangedEvent>(_ =>
			{
				AimCamera();
				ApplySafeArea();
			});
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
			BuildGearPopup();
			BuildHeroPopup();
			BuildAwayPopup(away);

			tooltip = AddLabel(root, "idle-tooltip");
			tooltip.style.display = DisplayStyle.None;
			tooltip.pickingMode = PickingMode.Ignore;

			built = true;
			ApplySplit();

		}

		private void BuildBattle(VisualElement shell)
		{
			battle = new VisualElement();
			battle.AddToClassList("idle-battle");
			shell.Add(battle);

			// 빈 곳 누르기는 응원 한 대. 무대 그 자체가 큰 버튼
			battle.RegisterCallback<PointerDownEvent>(OnTapped);

			BindBattleHud();
			BuildBattleExtras();
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
			BindCardQueue(battle.Q<VisualElement>("card-queue"));

			// 배속과 자동은 전투 HUD 것. 인형 탭에서 찾다가 플레이어 판이 NullReference 로
			// 죽는 자리 (실측 2026-09-01. 에디터는 예외를 콘솔에만 적어 초록으로 보임)
			speedCycleButton = battle.Q<Button>("speed-cycle-button");
			speedCycleButton.clicked += CycleSpeed;
			autoCastButton = battle.Q<Button>("auto-cast-button");
			autoCastButton.clicked += ToggleAutoCast;
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

		/// <summary>
		/// 줄 선 카드 칩. 다음에 올라올 순서 (gap-2026-08-23 P1)
		///
		/// ★ 이게 없으면 순환이 무작위와 구별이 안 된다. 맨 앞 하나는 색을 달리 해 <c>바로 다음</c> 표시
		/// </summary>
		private void BindCardQueue(VisualElement queue)
		{
			Label cap = new Label("다음");
			cap.AddToClassList("idle-queue-cap");
			queue.Add(cap);

			queueChips = new Label[IdleCards.QUEUE_SIZE];

			for (int index = 0; index < queueChips.Length; index++)
			{
				Label chip = new Label();
				chip.AddToClassList("idle-queue-chip");
				chip.EnableInClassList("idle-queue-chip--next", index == 0);
				queue.Add(chip);
				queueChips[index] = chip;
			}
		}

		private Button AddCardButton(VisualElement parent, System.Action clicked)
		{
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

		/// <summary>인형 탭 (layout.md §3). 모양은 UXML, 여기는 값과 클릭만</summary>
		private void BuildDollPage()
		{
			BindDollPage(AddPage(Tab.Doll));
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
			for (int stat = 0; stat < STAT_NAMES.Length; stat++)
			{
				int capturedStat = stat;
				statLabels[stat] = page.Q<Label>("stat-label-" + stat);

				for (int amount = 0; amount < STAT_AMOUNTS.Length; amount++)
				{
					int capturedAmount = STAT_AMOUNTS[amount];
					Button button = page.Q<Button>("stat-" + stat + "-x" + capturedAmount);
					button.clicked += () => Raise((IdleUpgradeKind)capturedStat, capturedAmount);
					statButtons[stat, amount] = button;
				}
			}

			for (int slot = 0; slot < SLOT_NAMES.Length; slot++)
			{
				int captured = slot;
				Button cell = page.Q<Button>("worn-" + slot);
				cell.clicked += () => OpenGear(captured);
				HookTooltip(cell, () => WornTip(captured));
				wornCells.Add(cell);
			}

			openHeroPopupButton = page.Q<Button>("open-hero-popup");
			openHeroPopupButton.clicked += () => OpenHeroPopup(gearSeat);
		}

		/// <summary>아이템 탭 (layout.md §3). 가방과 공방. 모양은 UXML</summary>
		private void BuildItemPage()
		{
			BindItemPage(AddPage(Tab.Item));
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

		/// <summary>UXML 한 장을 페이지에 옮긴다. 바깥 틀(idle-side)은 UI Builder 미리보기용</summary>
		private VisualElement OpenPage(Tab tab, VisualTreeAsset asset)
		{
			VisualElement page = AddPage(tab);
			TemplateContainer tree = asset.Instantiate();
			VisualElement frame = tree.Q<VisualElement>("page");

			while (frame.childCount > 0)
			{
				page.Add(frame[0]);
			}

			return page;
		}

		private void BuildCodexPage()
		{
			VisualElement page = OpenPage(Tab.Codex, codexPageAsset);
			codexLabel = page.Q<Label>("codex-label");
			codexRows = page.Q<VisualElement>("codex-rows");
		}

		private void BuildShopPage()
		{
			VisualElement page = OpenPage(Tab.Shop, shopPageAsset);
			pullButton = page.Q<Button>("pull-button");
			pullButton.clicked += Pull;
			pullOdds = page.Q<Label>("pull-odds");
			bagButton = page.Q<Button>("bag-button");
			bagButton.clicked += BuyBag;
			bagNote = page.Q<Label>("bag-note");
		}

		private void BuildLabPage()
		{
			VisualElement page = OpenPage(Tab.Lab, labPageAsset);
			prestigeSummary = page.Q<Label>("prestige-summary");
			prestigeButton = page.Q<Button>("prestige-button");
			prestigeButton.clicked += Prestige;
		}

		/// <summary>던전 넷 (economy.md). 알파 9번이라 지금은 눌리지 않는다</summary>
		private void BuildDungeonPage()
		{
			VisualElement page = OpenPage(Tab.Dungeon, dungeonPageAsset);

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
		private static string NameOf(IdleDungeonKind kind)
		{
			switch (kind)
			{
				case IdleDungeonKind.Gold: return "재화 던전";
				case IdleDungeonKind.Boss: return "보스 던전";
				case IdleDungeonKind.Gear: return "장비 던전";
				default: return "스킬 던전";
			}
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
			VisualElement page = OpenPage(Tab.Invest, investPageAsset);
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

		/// <summary>장비 고르기 팝업. 관리 열 위에 뜬다</summary>
		private void BuildGearPopup()
		{
			TemplateContainer tree = gearPopupAsset.Instantiate();
			gearPopup = tree.Q<VisualElement>("popup");
			gearPopup.RemoveFromHierarchy();
			gearPopup.style.display = DisplayStyle.None;
			side.Add(gearPopup);

			gearTitle = gearPopup.Q<Label>("gear-title");
			gearWorn = gearPopup.Q<Label>("gear-worn");
			gearPopup.Q<Button>("gear-close").clicked += CloseGear;
			gearRows = gearPopup.Q<VisualElement>("gear-rows");
		}

		private void BuildHeroPopup()
		{
			heroPopup = new VisualElement();
			AddClasses(heroPopup, "idle-box idle-choice-popup");
			heroPopup.style.display = DisplayStyle.None;
			side.Add(heroPopup);

			VisualElement head = new VisualElement();
			head.AddToClassList("idle-panel-head");
			heroPopup.Add(head);
			Label title = AddLabel(head, "idle-panel-title");
			title.text = "인형 선택";
			Button close = AddButton(head, "idle-tab idle-tab--close", CloseHeroPopup);
			close.text = "X";

			heroGrid = new VisualElement();
			heroGrid.AddToClassList("idle-choice-grid");
			heroPopup.Add(heroGrid);
		}

		private void BuildAwayPopup(IdleAwayReport away)
		{
			if (away.HasAnything == false)
			{
				return;
			}

			VisualElement shade = new VisualElement { name = "away-popup" };
			shade.AddToClassList("idle-away-shade");
			shade.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
			root.Add(shade);

			VisualElement card = new VisualElement();
			AddClasses(card, "idle-box idle-away-card");
			shade.Add(card);

			Label caption = AddLabel(card, "idle-cap idle-away-cap");
			caption.text = "WELCOME BACK";
			Label title = AddLabel(card, "idle-away-title");
			title.text = "돌아온 보상";
			Label span = AddLabel(card, "idle-away-span");
			span.text = string.Format("{0} 동안 작전이 계속됐습니다", DescribeSpan(away.CreditedSeconds));

			VisualElement rewards = new VisualElement();
			rewards.AddToClassList("idle-away-rewards");
			card.Add(rewards);
			AddAwayReward(rewards, "골드", BigNumberText.Format(away.ResourceGained));
			AddAwayReward(rewards, "처치", BigNumberText.Format(away.KillsGained));
			AddAwayReward(rewards, "스테이지", BigNumberText.Format(away.StagesGained));
			AddAwayReward(rewards, "장비", BigNumberText.Format(away.ItemsGained));

			if (away.HitCap)
			{
				Label cap = AddLabel(card, "idle-away-warning");
				cap.text = string.Format("오프라인 상한 {0}. 넘긴 {1}은 보상에 포함되지 않았습니다.",
					DescribeSpan(away.CapSeconds), DescribeSpan(away.LostSeconds));
			}

			Button close = AddButton(card, "idle-away-close", () => shade.RemoveFromHierarchy());
			close.name = "away-close";
			close.text = "확인하고 계속";
		}

		private static void AddAwayReward(VisualElement parent, string name, string amount)
		{
			VisualElement row = new VisualElement();
			row.AddToClassList("idle-away-reward");
			parent.Add(row);
			AddLabel(row, "idle-away-reward-name").text = name;
			AddLabel(row, "idle-away-reward-value").text = "+" + amount;
		}

		private void OpenHeroPopup(int slot)
		{
			if (slot < 0 || slot >= session.State.Party.Length)
			{
				slot = 0;
			}

			seatBeingFilled = slot;
			gearSeat = slot;
			CloseGear();
			heroPopup.style.display = DisplayStyle.Flex;
			Render(session.Capture());
		}

		private void CloseHeroPopup()
		{
			seatBeingFilled = -1;
			heroPopup.style.display = DisplayStyle.None;
		}

		private void RenderHeroPopup(IdleSnapshot snapshot)
		{
			if (heroPopup.style.display != DisplayStyle.Flex)
			{
				return;
			}

			while (heroChoiceButtons.Count < snapshot.Heroes.Length)
			{
				int captured = heroChoiceButtons.Count;
				int heroId = snapshot.Heroes[captured].Id;
				Button choice = AddButton(heroGrid, "idle-choice-card", () => ChooseHero(heroId));
				heroChoiceButtons.Add(choice);
			}

			for (int index = 0; index < heroChoiceButtons.Count; index++)
			{
				Button choice = heroChoiceButtons[index];
				bool shown = index < snapshot.Heroes.Length;
				choice.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;

				if (shown == false)
				{
					continue;
				}

				IdleHeroView hero = snapshot.Heroes[index];
				choice.text = string.Format("{0}{1}\nLv.{2}  {3}", hero.Name, Stars(hero.Stars), hero.Level,
					IdleHeroes.NameOfAxis(hero.Axis));
				int current = seatBeingFilled >= 0 && seatBeingFilled < snapshot.Party.Length
					? snapshot.Party[seatBeingFilled]
					: -1;
				choice.EnableInClassList("idle-choice-card--selected", current == hero.Id);
			}
		}

		/// <summary>이 부위에 낄 수 있는 가방 아이템만 보여준다</summary>
		private void OpenGear(int slot)
		{
			if (gearHeroId < 0)
			{
				SayOnce("먼저 편성 칸에서 인형을 고른다", noteSeconds);
				return;
			}

			gearSlot = slot;
			gearPopup.style.display = DisplayStyle.Flex;
			Render(session.Capture());
		}

		private void CloseGear()
		{
			gearSlot = -1;
			gearPopup.style.display = DisplayStyle.None;
		}

		private void RenderGear(IdleSnapshot snapshot)
		{
			if (gearSlot < 0)
			{
				return;
			}

			int wearer = gearHeroId;
			gearTitle.text = wearer >= 0
				? IdleHeroes.KindOf(wearer).Name + " " + SLOT_NAMES[gearSlot]
				: SLOT_NAMES[gearSlot];

			IdleItem worn = wearer >= 0 ? IdleGear.WornOf(session.State, wearer, gearSlot) : default;
			gearWorn.text = worn.IsEmpty
				? "지금 낀 것 없음"
				: string.Format("지금 {0}단계{1}", worn.Tier, worn.IsRaw ? string.Empty : " 감정됨");

			int shown = 0;

			for (int index = 0; index < snapshot.Bag.Length; index++)
			{
				IdleItem one = snapshot.Bag[index];
				if ((int)one.Slot != gearSlot)
				{
					continue;
				}

				Button row = RowAt(shown);
				int captured = index;
				row.userData = captured;
				row.text = string.Format("{0}단계{1}", one.Tier,
					one.IsRaw ? string.Empty : string.Format("  잠재 {0:P0}", one.PotentialValue));
				SetTierClass(row, one.Tier);
				row.style.display = DisplayStyle.Flex;
				shown++;
			}

			for (int at = shown; at < gearRowButtons.Count; at++)
			{
				gearRowButtons[at].style.display = DisplayStyle.None;
			}

			if (shown == 0)
			{
				Button row = RowAt(0);
				row.userData = -1;
				row.text = "가방에 이 부위 장비가 없다";
				row.style.display = DisplayStyle.Flex;
			}
		}

		/// <summary>팝업 줄 하나. 모자라면 새로 만든다</summary>
		private Button RowAt(int at)
		{
			while (gearRowButtons.Count <= at)
			{
				Button made = AddButton(gearRows, "idle-choice-card idle-gear-card", null);
				int captured = gearRowButtons.Count;
				made.clicked += () => PickGear(captured);
				gearRowButtons.Add(made);
			}

			return gearRowButtons[at];
		}

		private void PickGear(int rowIndex)
		{
			if (rowIndex < 0 || rowIndex >= gearRowButtons.Count)
			{
				return;
			}

			object held = gearRowButtons[rowIndex].userData;
			if (held is int bagIndex && bagIndex >= 0)
			{
				Equip(bagIndex);
				CloseGear();
			}
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
				RenderGear(snapshot);
				RenderHeroPopup(snapshot);
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

			for (int index = 0; index < queueChips.Length; index++)
			{
				queueChips[index].text = NameOf(snapshot.Queued[index]);
			}

			speedCycleButton.text = string.Format("{0:0}x", snapshot.Speed);
			speedCycleButton.EnableInClassList("idle-icon-button--on", snapshot.Speed > 1d);
			autoCastButton.EnableInClassList("idle-icon-button--on", snapshot.AutoCast);

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
				case Tab.Dungeon: RenderDungeons(snapshot); break;
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
				partyButtons[slot].EnableInClassList("idle-party-seat--geared", seatBeingFilled < 0 && gearSeat == slot);
			}

			int wearer = gearHeroId;
			dollName.text = wearer >= 0 ? IdleHeroes.KindOf(wearer).Name + " 성장" : "빈 자리";
			openHeroPopupButton.text = wearer >= 0 ? "인형 바꾸기" : "인형 선택";

			for (int stat = 0; stat < STAT_NAMES.Length; stat++)
			{
				IdleUpgradeKind kind = (IdleUpgradeKind)stat;
				IdleUpgradeView one = session.ViewHeroStat(wearer, kind, 1);
				statLabels[stat].text = string.Format("{0}  {1}  Lv.{2}",
					STAT_NAMES[stat], StatValueText(kind, one.CurrentValue), one.Level);

				for (int amount = 0; amount < STAT_AMOUNTS.Length; amount++)
				{
					int count = STAT_AMOUNTS[amount];
					IdleUpgradeView purchase = session.ViewHeroStat(wearer, kind, count);
					Button button = statButtons[stat, amount];
					button.text = purchase.IsMaxed
						? "최대"
						: string.Format("×{0}\n{1}", count, BigNumberText.Format(purchase.NextCost));
					button.SetEnabled(wearer >= 0 && purchase.CanAfford);
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
				wornCells[slot].text = SLOT_NAMES[slot] + "\n" + (one.IsEmpty ? "없음" : one.Tier + "단계");
				wornCells[slot].EnableInClassList("idle-worn-cell--empty", one.IsEmpty);
				wornCells[slot].SetEnabled(wearer >= 0);
				SetTierClass(wornCells[slot], one.IsEmpty ? 0 : one.Tier);
			}

		}

		/// <summary>배속을 다음 자리로 (gap-2026-08-23 P1-6). 보고 있는 동안만</summary>
		private void CycleSpeed()
		{
			if (session == null)
			{
				return;
			}

			session.CycleSpeed();
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
				forgeKindButtons[index].text = string.Format("{0}단계 ×{1}", tier, counts[key]);
				SetTierClass(forgeKindButtons[index], tier);
				forgeKindButtons[index].EnableInClassList("idle-forge-kind--on", forgeTier == tier);
			}

			int forgeKey = forgeTier;
			int have = forgeTier > 0 && forgeKey < counts.Length ? counts[forgeKey] : 0;
			int shown = have > snapshot.MergeCount ? snapshot.MergeCount : have;

			for (int index = 0; index < forgeCells.Count; index++)
			{
				bool filled = index < shown;
				forgeCells[index].text = filled ? forgeTier + "단계" : string.Empty;
				SetTierClass(forgeCells[index], filled ? forgeTier : 0);
			}

			bool ready = forgeTier > 0 && have >= snapshot.MergeCount;
			forgeResult.text = forgeTier > 0 ? "랜덤\n" + (forgeTier + 1) + "단계" : string.Empty;
			SetTierClass(forgeResult, forgeTier > 0 ? forgeTier + 1 : 0);
			forgeResult.EnableInClassList("idle-forge-cell--ready", ready);

			forgeTitle.text = forgeTier > 0
				? string.Format("{0}단계  {1}/{2}", forgeTier, have, snapshot.MergeCount)
				: string.Format("같은 단계 {0}개가 랜덤 장비 한 단계 위로", snapshot.MergeCount);
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

			float share = BATTLE_SHARE;
			float sideWidth = side != null ? side.resolvedStyle.width : float.NaN;

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
			bool showSide = split || sideOpen;
			side.style.display = showSide ? DisplayStyle.Flex : DisplayStyle.None;
			side.EnableInClassList("idle-side--drawer", split == false);
			closeSideButton.style.display = split ? DisplayStyle.None : DisplayStyle.Flex;
			floatingTabs.style.display = split ? DisplayStyle.None : DisplayStyle.Flex;
			battle.EnableInClassList("idle-battle--full", split == false);
			splitButton.text = split ? "풀화면" : "분할";

			AimCamera();
			ApplySafeArea();

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
			if (volleyHandIndex >= 0)
			{
				if (stage != null && stage.TryPickFoe(moment.position, out long foeIndex)
					&& session.TryCastCardAt(volleyHandIndex, foeIndex, out IdleCardResult result))
				{
					stage.OnVolley();
					volleyHandIndex = -1;
					SayOnce("일제 사격. 목표를 집중 타격했다", noteSeconds);
					WriteDown();
					Render(session.Capture());
				}

				return;
			}

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
				volleyHandIndex = handIndex;
				SayOnce("일제 사격. 적 하나를 선택하세요", noteSeconds);
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

		private void Raise(IdleUpgradeKind kind, int amount)
		{
			if (gearHeroId < 0)
			{
				return;
			}

			if (session.Send(new IdleRaiseUpgradeIntent(gearHeroId, kind, amount)))
			{
				WriteDown();
			}
			Render(session.Capture());
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
				while (session.Send(new IdleMergeIntent(tier, IdleItemSlot.Head)))
				{
					merged++;
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
				SayOnce("환생. 새 종이. 코스트는 그대로다", noteSeconds * 2f);
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
			target.RegisterCallback<PointerDownEvent>(moment =>
			{
				// 손가락이나 펜만. 마우스는 호버 담당
				if (moment.pointerType != UnityEngine.UIElements.PointerType.mouse)
				{
					ShowTooltip(text());
					MoveTooltip(moment.position);
				}
			});

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

		private static string StatValueText(IdleUpgradeKind kind, double value)
		{
			switch (kind)
			{
				case IdleUpgradeKind.AttackSpeed:
					return string.Format("{0:0.##}/초", value);
				case IdleUpgradeKind.Defense:
				case IdleUpgradeKind.CriticalChance:
					return string.Format("{0:P1}", value);
				case IdleUpgradeKind.CriticalDamage:
					return string.Format("×{0:0.00}", value);
				default:
					return BigNumberText.Format(value);
			}
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
