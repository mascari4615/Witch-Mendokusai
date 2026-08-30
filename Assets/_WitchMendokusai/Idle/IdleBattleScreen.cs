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
	[RequireComponent(typeof(UIDocument))]
	public sealed class IdleBattleScreen : MonoBehaviour, IGameView<IdleSnapshot>
	{
		[Header("수치. 비워 두면 코드 기본값")]
		[SerializeField] private IdleTuningSO tuningAsset;

		[Header("생김새")]
		[SerializeField] private StyleSheet styleSheet;

		[Header("무대. 씬이 꽂아 준다")]
		[SerializeField] private IdleBattleStage stage;

		[Header("얼마나 자주 적나 (초)")]
		[SerializeField] private float saveIntervalSeconds = 10f;

		[Header("알림이 남는 시간 (초)")]
		[SerializeField] private float noteSeconds = 5f;

		private IdleSession session;
		private float sinceLastSave;
		private bool skipSaveOnce;

		// ── 탭 ────────────────────────────────────────────────────────────
		private enum Tab { Doll, Item, Codex, Shop, Lab, Dungeon, Invest }

		private static readonly string[] TAB_NAMES = { "인형", "아이템", "도감", "상점", "연구소", "던전", "투자" };
		private static readonly string[] TAB_CAPTIONS = { "DOLLS", "ITEMS", "CODEX", "SHOP", "LAB", "DUNGEON", "INVEST" };
		private static readonly string[] SLOT_NAMES = { "머리", "몸", "손", "발" };

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

		// 아이템
		private Label gearSummary;
		private VisualElement bagGrid;
		private readonly List<Button> bagCells = new List<Button>();
		private VisualElement mergeRows;
		private readonly List<Button> mergeButtons = new List<Button>();
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

		// 팝업
		private VisualElement mapPopup;
		private VisualElement mapRows;
		private readonly List<Button> mapButtons = new List<Button>();

		public PresentationKind Kind => PresentationKind.UIOnly;

		private void OnEnable()
		{
			if (tuningAsset == null)
			{
				Debug.LogWarning("[IdleV2] 수치 에셋이 안 꽂혀 있다. 코드 기본값으로 돈다.");
			}

			IdleTuning tuning = tuningAsset != null ? tuningAsset.ToTuning() : new IdleTuning();

			IdleState state = new IdleState();
			IdleSaveData? saved = IdleSaveStore.Load();
			if (saved.HasValue)
			{
				state.Load(saved.Value);
			}

			session = new IdleSession(tuning, state);
			session.CatchUp(IdleSaveStore.NowUnixSeconds(), out IdleAwayReport away);

			if (stage != null)
			{
				stage.Build();
			}
			else
			{
				Debug.LogWarning("[IdleV2] 무대가 안 꽂혀 있다. HUD 만 뜬다. 씬 빌더로 다시 지어라.");
			}

			split = PlayerPrefs.GetInt("idle.split", 1) == 1;

			BuildAll(away);
			Render(session.Capture());
		}

		private void OnDisable()
		{
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
			if (session == null)
			{
				return;
			}

			float delta = Time.unscaledDeltaTime;

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

			if (styleSheet != null)
			{
				root.styleSheets.Add(styleSheet);
			}
			else
			{
				Debug.LogWarning("[IdleV2] 스타일시트가 안 꽂혀 있다. 화면이 꾸밈 없이 뜬다.");
			}

			VisualElement shell = new VisualElement();
			shell.AddToClassList("v2-root");
			root.Add(shell);

			BuildBattle(shell);
			BuildSide(shell);
			BuildMapPopup();
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
			battle.AddToClassList("v2-battle");
			shell.Add(battle);

			// 빈 곳 누르기는 응원 한 대. 무대 그 자체가 큰 버튼
			battle.RegisterCallback<PointerDownEvent>(OnTapped);

			// 상점, 연구소 씬 자리. 지금은 덮개 + 글자
			sceneCover = new VisualElement();
			sceneCover.AddToClassList("v2-scene-cover");
			sceneCover.style.display = DisplayStyle.None;
			battle.Add(sceneCover);
			sceneCoverLabel = AddLabel(sceneCover, "v2-scene-cover-label");
			AddButton(sceneCover, "v2-button", () => OpenTab(Tab.Doll)).text = "전투로";

			// 좌상. 작전 코드 + 웨이브. 누르면 맵 팝업
			VisualElement op = new VisualElement();
			op.AddToClassList("v2-box v2-op");
			battle.Add(op);
			op.RegisterCallback<ClickEvent>(_ => ToggleMap());

			VisualElement opRow = new VisualElement();
			opRow.AddToClassList("v2-op-row");
			op.Add(opRow);
			opCode = AddLabel(opRow, "v2-op-code");
			opName = AddLabel(opRow, "v2-op-name");

			VisualElement waveRow = new VisualElement();
			waveRow.AddToClassList("v2-op-row");
			op.Add(waveRow);
			waveDots = new VisualElement();
			waveDots.AddToClassList("v2-wave");
			waveRow.Add(waveDots);
			waveLabel = AddLabel(waveRow, "v2-cap");

			// 좌상 둘째 줄. 스테퍼 + 반복
			VisualElement stepRow = new VisualElement();
			stepRow.AddToClassList("v2-step-row");
			battle.Add(stepRow);

			VisualElement stepper = new VisualElement();
			stepper.AddToClassList("v2-box v2-stepper");
			stepRow.Add(stepper);
			stepBack = AddButton(stepper, "v2-step-button", () => StepStage(-1));
			stepBack.text = "◀";
			stepLabel = AddLabel(stepper, "v2-step-label");
			stepForward = AddButton(stepper, "v2-step-button", () => StepStage(1));
			stepForward.text = "▶";

			repeatButton = AddButton(stepRow, "v2-box v2-toggle", ToggleHold);

			// 우상. 재화 3 + 분할
			VisualElement chips = new VisualElement();
			chips.AddToClassList("v2-chips");
			battle.Add(chips);
			goldChip = AddLabel(chips, "v2-box v2-chip");
			pullChip = AddLabel(chips, "v2-box v2-chip");
			prestigeChip = AddLabel(chips, "v2-box v2-chip");
			splitButton = AddButton(chips, "v2-box v2-icon-button", ToggleSplit);
			splitButton.text = "분할";

			// 우상 둘째 줄. 배속, AUTO 자리 (코어 미구현. 자리만)
			VisualElement speedRow = new VisualElement();
			speedRow.AddToClassList("v2-speed-row");
			battle.Add(speedRow);
			Button speed = AddButton(speedRow, "v2-box v2-icon-button", null);
			speed.text = "1×";
			speed.SetEnabled(false);
			Button auto = AddButton(speedRow, "v2-box v2-icon-button", null);
			auto.text = "AUTO";
			auto.SetEnabled(false);

			// 우측. 로그 (지금은 안내 한 줄 + 알림 한 줄)
			VisualElement log = new VisualElement();
			log.AddToClassList("v2-box v2-log");
			battle.Add(log);
			AddLabel(log, "v2-cap").text = "LOG";
			logLabel = AddLabel(log, "v2-log-line");
			noteLabel = AddLabel(log, "v2-log-line v2-log-line--note");

			// 상단 중앙. 보스 바 (보스 때만)
			enemyBar = new VisualElement();
			enemyBar.AddToClassList("v2-box v2-enemy");
			battle.Add(enemyBar);
			enemyLabel = AddLabel(enemyBar, "v2-enemy-label");
			VisualElement gauge = new VisualElement();
			gauge.AddToClassList("v2-enemy-gauge");
			enemyBar.Add(gauge);
			enemyFill = new VisualElement();
			enemyFill.AddToClassList("v2-enemy-fill");
			gauge.Add(enemyFill);

			// 중앙. 실패 배너 (반복 중일 때만)
			failBanner = new VisualElement();
			failBanner.AddToClassList("v2-box v2-fail");
			failBanner.style.display = DisplayStyle.None;
			battle.Add(failBanner);
			failLabel = AddLabel(failBanner, "v2-fail-label");
			nextStageButton = AddButton(failBanner, "v2-button v2-button--strong", NextStage);

			// 하단 중앙. 손패 + 코스트
			VisualElement hand = new VisualElement();
			hand.AddToClassList("v2-hand");
			battle.Add(hand);

			cardButtons = new Button[IdleCards.CARD_COUNT];
			for (int index = 0; index < cardButtons.Length; index++)
			{
				IdleCardKind kind = (IdleCardKind)index;
				cardButtons[index] = AddButton(hand, "v2-card", () => Cast(kind));
			}

			VisualElement cost = new VisualElement();
			cost.AddToClassList("v2-cost");
			hand.Add(cost);
			costLabel = AddLabel(cost, "v2-cost-label");
			VisualElement costGauge = new VisualElement();
			costGauge.AddToClassList("v2-cost-gauge");
			cost.Add(costGauge);
			costFill = new VisualElement();
			costFill.AddToClassList("v2-cost-fill");
			costGauge.Add(costFill);
			AddLabel(cost, "v2-cap").text = "COST";

			// 우하. 풀화면일 때만 보이는 탭 7
			floatingTabs = new VisualElement();
			floatingTabs.AddToClassList("v2-floating-tabs");
			battle.Add(floatingTabs);
			for (int index = 0; index < TAB_NAMES.Length; index++)
			{
				Tab tab = (Tab)index;
				Button button = AddButton(floatingTabs, "v2-box v2-icon-button", () => OpenTab(tab));
				button.text = TAB_NAMES[index];
				floatingTabButtons.Add(button);
			}

			// 좌하. 디버그. 에디터와 개발 빌드에서만 (사용자 요청 2026-08-30)
			if (Application.isEditor || Debug.isDebugBuild)
			{
				Button wipe = AddButton(battle, "v2-box v2-icon-button v2-debug", WipeAndRestart);
				wipe.text = "데이터 초기화";
			}
		}

		private void BuildSide(VisualElement shell)
		{
			side = new VisualElement();
			side.AddToClassList("v2-side");
			shell.Add(side);

			VisualElement tabs = new VisualElement();
			tabs.AddToClassList("v2-tabs");
			side.Add(tabs);

			for (int index = 0; index < TAB_NAMES.Length; index++)
			{
				Tab tab = (Tab)index;
				Button button = AddButton(tabs, "v2-tab", () => OpenTab(tab));
				button.text = TAB_NAMES[index] + "\n" + TAB_CAPTIONS[index];
				tabButtons.Add(button);
			}

			closeSideButton = AddButton(tabs, "v2-tab v2-tab--close", CloseSide);
			closeSideButton.text = "×";

			VisualElement head = new VisualElement();
			head.AddToClassList("v2-panel-head");
			side.Add(head);
			panelTitle = AddLabel(head, "v2-panel-title");
			panelCaption = AddLabel(head, "v2-cap");

			ScrollView body = new ScrollView();
			body.AddToClassList("v2-panel-body");
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
			page.AddToClassList("v2-page");
			page.style.display = DisplayStyle.None;
			panelHost.Add(page);
			pages[(int)tab] = page;
			return page;
		}

		/// <summary>인형 탭. 한 화면: 위 편성 6칸, 가운데 성장, 아래 장비 4칸 (layout.md §3).</summary>
		private void BuildDollPage()
		{
			VisualElement page = AddPage(Tab.Doll);

			AddLabel(page, "v2-cap").text = "편성. MAIN 3 전장 / SUB 3 보조";
			VisualElement party = new VisualElement();
			party.AddToClassList("v2-party");
			page.Add(party);

			for (int slot = 0; slot < IdleHeroes.PARTY_SLOTS; slot++)
			{
				int captured = slot;
				Button seat = AddButton(party, "v2-party-seat", () => BeginSeat(captured));
				seat.EnableInClassList("v2-party-seat--sub", IdleHeroes.IsMainSlot(slot) == false);
				partyButtons.Add(seat);
			}

			dollName = AddLabel(page, "v2-row-head");

			AddLabel(page, "v2-cap").text = "성장 (지금은 판 전체 강화. 인형별 레벨은 다음 조각)";
			damageLabel = AddLabel(page, "v2-row-title");
			damageButton = AddButton(page, "v2-row-button", () => Raise(IdleUpgradeKind.Damage));
			speedLabel = AddLabel(page, "v2-row-title");
			speedButton = AddButton(page, "v2-row-button", () => Raise(IdleUpgradeKind.AttackSpeed));
			bulkRaiseButton = AddButton(page, "v2-row-button v2-row-button--strong", RaiseMany);

			AddLabel(page, "v2-cap").text = "장비 4. 칸을 누르면 가방에서 고른다 (지금은 아이템 탭에서 장착)";
			VisualElement worn = new VisualElement();
			worn.AddToClassList("v2-worn");
			page.Add(worn);
			for (int slot = 0; slot < SLOT_NAMES.Length; slot++)
			{
				Label cell = AddLabel(worn, "v2-worn-cell");
				wornCells.Add(cell);
			}

			AddLabel(page, "v2-cap").text = "보유. 누르면 고른 칸에 앉힌다";
			heroRows = new VisualElement();
			page.Add(heroRows);
		}

		private void BuildItemPage()
		{
			VisualElement page = AddPage(Tab.Item);

			gearSummary = AddLabel(page, "v2-row-title");

			bagGrid = new VisualElement();
			bagGrid.AddToClassList("v2-bag");
			page.Add(bagGrid);

			for (int index = 0; index < 40; index++)
			{
				int captured = index;
				Button cell = AddButton(bagGrid, "v2-bag-cell", () => Equip(captured));
				bagCells.Add(cell);
			}

			AddLabel(page, "v2-cap").text = "공방. 같은 것을 모으면 한 단계 위로";
			mergeRows = new VisualElement();
			page.Add(mergeRows);

			AddLabel(page, "v2-cap").text = "감정. 잠재를 굴린다 (알파 뒤)";
			appraiseRows = new VisualElement();
			page.Add(appraiseRows);
		}

		private void BuildCodexPage()
		{
			VisualElement page = AddPage(Tab.Codex);
			codexLabel = AddLabel(page, "v2-row-title");
			codexRows = new VisualElement();
			page.Add(codexRows);
		}

		private void BuildShopPage()
		{
			VisualElement page = AddPage(Tab.Shop);

			VisualElement banner = new VisualElement();
			banner.AddToClassList("v2-banner");
			page.Add(banner);
			AddLabel(banner, "v2-banner-title").text = "인형 뽑기";
			AddLabel(banner, "v2-cap").text = "PICK UP. 배너와 연출은 다음 조각";

			pullButton = AddButton(page, "v2-row-button v2-row-button--strong v2-row-button--tall", Pull);
			pullOdds = AddLabel(page, "v2-row-note");
			AddLabel(page, "v2-row-note").text = "현금 결제 없음. 뽑기 재화는 첫 클리어, 환생, 낮은 확률 드롭에서만.";
			AddLabel(page, "v2-cap").text = "하루 1회 무료 상자 (알파. 자리만)";
		}

		private void BuildLabPage()
		{
			VisualElement page = AddPage(Tab.Lab);
			prestigeSummary = AddLabel(page, "v2-row-title");
			AddLabel(page, "v2-row-note").text = "연구 트리와 골드 유틸은 다음 조각. 지금은 환생만.";
			prestigeButton = AddButton(page, "v2-row-button v2-row-button--strong v2-row-button--tall", Prestige);
		}

		private void BuildDungeonPage()
		{
			VisualElement page = AddPage(Tab.Dungeon);
			string[] names = { "재화 던전", "보스 던전", "장비 던전", "스킬 던전" };
			for (int index = 0; index < names.Length; index++)
			{
				Button row = AddButton(page, "v2-row-button", null);
				row.text = names[index] + ". 입장권 0/0 (알파 9번. 자리만)";
				row.SetEnabled(false);
			}
		}

		private void BuildInvestPage()
		{
			VisualElement page = AddPage(Tab.Invest);
			baseSummary = AddLabel(page, "v2-row-title");
			bulkBuyButton = AddButton(page, "v2-row-button v2-row-button--strong", BuyMany);

			for (int kind = 0; kind < 8; kind++)
			{
				int captured = kind;
				producerButtons.Add(AddButton(page, "v2-row-button", () => BuyProducer(captured)));
			}
		}

		private void BuildMapPopup()
		{
			mapPopup = new VisualElement();
			mapPopup.AddToClassList("v2-box v2-map");
			mapPopup.style.display = DisplayStyle.None;
			battle.Add(mapPopup);

			VisualElement head = new VisualElement();
			head.AddToClassList("v2-panel-head");
			mapPopup.Add(head);
			AddLabel(head, "v2-panel-title").text = "맵";
			AddLabel(head, "v2-cap").text = "MAP";
			AddButton(head, "v2-tab v2-tab--close", ToggleMap).text = "×";

			mapRows = new VisualElement();
			mapPopup.Add(mapRows);
		}

		// ── 그리기 ────────────────────────────────────────────────────────

		public void Render(IdleSnapshot snapshot)
		{
			if (opCode == null)
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
			repeatButton.EnableInClassList("v2-toggle--on", snapshot.HoldingStage);

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
					VisualElement dot = new VisualElement();
					dot.AddToClassList("v2-wave-dot");
					dot.EnableInClassList("v2-wave-dot--boss", at == snapshot.KillsPerStage - 1);
					waveDots.Add(dot);
					waveDotList.Add(dot);
				}
			}

			for (int at = 0; at < waveDotList.Count; at++)
			{
				waveDotList[at].EnableInClassList("v2-wave-dot--done", at < snapshot.KillsInStage);
			}

			waveLabel.text = string.Format("WAVE {0}/{1}", snapshot.KillsInStage, snapshot.KillsPerStage);
		}

		private void RenderHand(IdleSnapshot snapshot)
		{
			for (int index = 0; index < cardButtons.Length; index++)
			{
				IdleCardView card = snapshot.Cards[index];
				cardButtons[index].text = string.Format("{0}\n{1}", card.Cost, NameOf(card.Kind));
				cardButtons[index].SetEnabled(card.CanCast);
				cardButtons[index].EnableInClassList("v2-card--ready", card.CanCast);
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
				tabButtons[index].EnableInClassList("v2-tab--on", index == (int)openTab && (split || sideOpen));
			}
		}

		private void SetBadge(Tab tab, bool on)
		{
			tabButtons[(int)tab].EnableInClassList("v2-tab--badge", on);
			floatingTabButtons[(int)tab].EnableInClassList("v2-tab--badge", on);
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
				string tag = IdleHeroes.IsMainSlot(slot) ? "MAIN" : "SUB";
				partyButtons[slot].text = id >= 0
					? tag + "\n" + IdleHeroes.KindOf(id).Name
					: tag + "\n비었다";
				partyButtons[slot].EnableInClassList("v2-party-seat--picking", seatBeingFilled == slot);
			}

			dollName.text = seatBeingFilled >= 0
				? string.Format("{0}번 칸에 앉힐 인형을 아래에서 고른다", seatBeingFilled + 1)
				: "칸을 누르면 그 자리에 앉힐 인형을 고른다";

			DrawUpgrade(snapshot.Damage, damageLabel, damageButton, "공격력", "한 방 {0}");
			DrawUpgrade(snapshot.AttackSpeed, speedLabel, speedButton, "공격속도", "초당 {0}회");

			bool canRaise = IdleModel.CheapestRaisableAxis(session.State, session.Tuning, out IdleUpgradeKind _);
			bulkRaiseButton.text = canRaise ? "싼 축부터 몰아 올린다" : "올릴 수 있는 게 없다";
			bulkRaiseButton.SetEnabled(canRaise);

			for (int slot = 0; slot < wornCells.Count && slot < snapshot.Worn.Length; slot++)
			{
				IdleItem one = snapshot.Worn[slot];
				wornCells[slot].text = SLOT_NAMES[slot] + "\n" + (one.IsEmpty ? "빔" : "T" + one.Tier);
				wornCells[slot].EnableInClassList("v2-worn-cell--empty", one.IsEmpty);
			}

			if (heroButtons.Count != snapshot.Heroes.Length)
			{
				heroRows.Clear();
				heroButtons.Clear();

				for (int index = 0; index < snapshot.Heroes.Length; index++)
				{
					int id = snapshot.Heroes[index].Id;
					heroButtons.Add(AddButton(heroRows, "v2-row-button", () => ChooseHero(id)));
				}
			}

			for (int index = 0; index < heroButtons.Count && index < snapshot.Heroes.Length; index++)
			{
				IdleHeroView hero = snapshot.Heroes[index];
				heroButtons[index].text = string.Format("{0}{1}  {2}  보유 +{3:P0}{4}",
					hero.Name, Stars(hero.Stars), IdleHeroes.NameOfGrade(hero.Grade), hero.OwnedShare,
					hero.InParty ? "  ▶ 편성" : string.Empty);
			}
		}

		private void RenderItemPage(IdleSnapshot snapshot)
		{
			bool full = snapshot.Bag.Length >= snapshot.BagCapacity;
			gearSummary.text = string.Format("가방 {0}/{1}{2}",
				snapshot.Bag.Length, snapshot.BagCapacity,
				full ? "  꽉 찼다. 합치거나 차야 새 장비가 들어온다" : string.Empty);
			gearSummary.EnableInClassList("v2-warn", full);

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
					continue;
				}

				IdleItem one = snapshot.Bag[index];
				bagCells[index].text = string.Format("{0}\nT{1}", SLOT_NAMES[(int)one.Slot], one.Tier);
				bagCells[index].SetEnabled(true);
			}

			RenderMerge(snapshot);
			RenderAppraise(snapshot);
		}

		private void RenderMerge(IdleSnapshot snapshot)
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

			List<string> labels = new List<string>();
			List<int> tiers = new List<int>();
			List<IdleItemSlot> slots = new List<IdleItemSlot>();
			List<bool> afford = new List<bool>();

			for (int key = 0; key < counts.Length; key++)
			{
				if (counts[key] < snapshot.MergeCount)
				{
					continue;
				}

				int tier = key / IdleGear.SLOT_COUNT;
				IdleItemSlot slot = (IdleItemSlot)(key % IdleGear.SLOT_COUNT);
				double cost = IdleGear.MergeCost(tier, session.Tuning);

				labels.Add(string.Format("{0} T{1} ×{2} → T{3}  ({4})",
					SLOT_NAMES[(int)slot], tier, counts[key], tier + 1, BigNumberText.Format(cost)));
				tiers.Add(tier);
				slots.Add(slot);
				afford.Add(snapshot.Resource >= cost);
			}

			if (mergeButtons.Count != labels.Count)
			{
				mergeRows.Clear();
				mergeButtons.Clear();

				for (int index = 0; index < labels.Count; index++)
				{
					int tier = tiers[index];
					IdleItemSlot slot = slots[index];
					mergeButtons.Add(AddButton(mergeRows, "v2-row-button", () => Merge(tier, slot)));
				}
			}

			for (int index = 0; index < mergeButtons.Count && index < labels.Count; index++)
			{
				mergeButtons[index].text = labels[index];
				mergeButtons[index].SetEnabled(afford[index]);
			}
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
					appraiseButtons.Add(AddButton(appraiseRows, "v2-row-button", () => Appraise(captured)));
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
					codexLabels.Add(AddLabel(codexRows, "v2-row-title"));
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
				codexLabels[id].EnableInClassList("v2-row-title--dim", owned == false);
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
					mapButtons.Add(AddButton(mapRows, "v2-row-button", () => GoToStage(target)));
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
				mapButtons[index].EnableInClassList("v2-row-button--strong", here);
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
			battle.EnableInClassList("v2-battle--alt", altScene);

			ApplySplit();
			Render(session.Capture());
		}

		private void CloseSide()
		{
			sideOpen = false;
			sceneCover.style.display = DisplayStyle.None;
			battle.EnableInClassList("v2-battle--alt", false);
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
			side.EnableInClassList("v2-side--drawer", split == false);
			closeSideButton.style.display = split ? DisplayStyle.None : DisplayStyle.Flex;
			floatingTabs.style.display = split ? DisplayStyle.None : DisplayStyle.Flex;
			battle.EnableInClassList("v2-battle--full", split == false);
			splitButton.text = split ? "풀화면" : "분할";

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
				if (at.ClassListContains("v2-box"))
				{
					return true;
				}
			}

			return false;
		}

		private void Cast(IdleCardKind kind)
		{
			if (session.TryCastCard(kind, out IdleCardResult result) == false)
			{
				return;
			}

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
				SayOnce(string.Format("T{0} 을 합쳤다", tier), noteSeconds);
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

		// ── 잔손 ──────────────────────────────────────────────────────────

		private void SayOnce(string what, float seconds)
		{
			noteLabel.text = what;
			noteLabel.style.opacity = 1f;
			noteLeft = seconds;
		}

		private static void DrawUpgrade(IdleUpgradeView view, Label label, Button button,
			string name, string valueFormat)
		{
			label.text = string.Format("{0} Lv.{1}. " + valueFormat, name, view.Level,
				BigNumberText.Format(view.CurrentValue));

			button.text = view.IsMaxed
				? "최대"
				: string.Format("올리기. {0}", BigNumberText.Format(view.NextCost));
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
