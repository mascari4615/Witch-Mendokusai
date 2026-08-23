using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using WitchMendokusai.DomainSDK.Contracts;
using WitchMendokusai.DomainSDK.Idle;
using BigNumberText = WitchMendokusai.Numerics.BigNumberText;

namespace WitchMendokusai
{
	/// <summary>
	/// V2 작전 화면 — 쿼터뷰 무대 위의 HUD (concept-v2, 사용자 방향 2026-08-23).
	///
	/// ★ 상주하는 것: 적 체력 · 우리 자리 넷(체력·부활) · 손패(카드+코스트) · 동사 몇.
	///   나머지(강화·장비·인형)는 <b>열고 닫는 장</b>이다 — 울티마 문법.
	///
	/// ★ 규칙은 한 줄도 없다 — 사진을 그리고 의도를 보낸다. 판정은 전부 코어.
	/// </summary>
	[RequireComponent(typeof(UIDocument))]
	public sealed class IdleBattleScreen : MonoBehaviour, IGameView<IdleSnapshot>
	{
		[Header("수치 — 비워 두면 코드 기본값")]
		[SerializeField] private IdleTuningSO tuningAsset;

		[Header("생김새")]
		[SerializeField] private StyleSheet styleSheet;

		[Header("무대 — 씬이 꽂아 준다")]
		[SerializeField] private IdleBattleStage stage;

		[Header("얼마나 자주 적나 (초)")]
		[SerializeField] private float saveIntervalSeconds = 10f;

		[Header("알림이 남는 시간 (초)")]
		[SerializeField] private float noteSeconds = 5f;

		private IdleSession session;
		private float sinceLastSave;

		// ── 위 띠 ───────────────────────────────────────────────────────────
		private Label stageLabel;
		private Label resourceLabel;
		private Label guideLabel;
		private Label noteLabel;
		private float noteLeft;

		private VisualElement enemyFill;
		private Label enemyLabel;
		private VisualElement waveDots;
		private readonly List<VisualElement> waveDotList = new List<VisualElement>();

		// ── 우리 자리 ───────────────────────────────────────────────────────
		private readonly List<VisualElement> seatCards = new List<VisualElement>();
		private readonly List<VisualElement> seatFills = new List<VisualElement>();
		private readonly List<Label> seatLabels = new List<Label>();

		// ── 손패 ────────────────────────────────────────────────────────────
		private Button[] cardButtons;
		private VisualElement costFill;
		private Label costLabel;

		// ── 동사 ────────────────────────────────────────────────────────────
		private Button retreatButton;
		private Button prestigeButton;
		private Button nextStageButton;
		private VisualElement failBanner;
		private Label failLabel;

		// ── 장 (열고 닫는) ──────────────────────────────────────────────────
		private VisualElement sheetHost;
		private Label sheetTitle;
		private VisualElement sheetBody;
		private int openSheet = -1;
		private readonly Button[] sheetOpeners = new Button[3];

		// 강화 장
		private Label damageLabel;
		private Button damageButton;
		private Label speedLabel;
		private Button speedButton;
		private Button bulkRaiseButton;
		private Label baseSummary;
		private Button bulkBuyButton;
		private readonly List<Button> producerButtons = new List<Button>();

		// 장비 장
		private Label gearSummary;
		private Label wornLabel;
		private VisualElement bagGrid;
		private readonly List<Button> bagCells = new List<Button>();
		private VisualElement mergeRows;
		private readonly List<Button> mergeButtons = new List<Button>();
		private VisualElement appraiseRows;
		private readonly List<Button> appraiseButtons = new List<Button>();

		// 인형 장
		private Button pullButton;
		private Label pullOdds;
		private Label codexLabel;
		private VisualElement partyRow;
		private readonly List<Button> partyButtons = new List<Button>();
		private VisualElement heroRows;
		private readonly List<Button> heroButtons = new List<Button>();
		private int seatBeingFilled = -1;

		private static readonly string[] SLOT_NAMES = { "머리", "몸", "손", "발" };
		private static readonly string[] SHEET_NAMES = { "강화", "장비", "인형" };

		public PresentationKind Kind => PresentationKind.UIOnly;

		private void OnEnable()
		{
			if (tuningAsset == null)
			{
				Debug.LogWarning("[IdleV2] 수치 에셋이 안 꽂혀 있다 — 코드 기본값으로 돈다.");
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
				Debug.LogWarning("[IdleV2] 무대가 안 꽂혀 있다 — HUD 만 뜬다. 씬 빌더로 다시 지어라.");
			}

			BuildHud(away);
			Render(session.Capture());
		}

		private void OnDisable()
		{
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
			float delta = Time.unscaledDeltaTime;

			// 보고 있는 동안이라 <b>위험이 흐른다</b> — 적이 때리고, 쓰러지고, 일어난다.
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

		// ── 짓기 ────────────────────────────────────────────────────────────

		private void BuildHud(IdleAwayReport away)
		{
			VisualElement root = GetComponent<UIDocument>().rootVisualElement;
			root.Clear();

			if (styleSheet != null)
			{
				root.styleSheets.Add(styleSheet);
			}
			else
			{
				Debug.LogWarning("[IdleV2] 스타일시트가 안 꽂혀 있다 — 화면이 꾸밈 없이 뜬다.");
			}

			VisualElement shell = new VisualElement();
			shell.AddToClassList("v2-root");
			root.Add(shell);

			// ★ 빈 곳 누르기 = 응원 한 대 — 큰 버튼은 무대 그 자체다.
			shell.RegisterCallback<PointerDownEvent>(OnTapped);

			BuildTop(shell);
			BuildFailBanner(shell);

			VisualElement bottom = new VisualElement();
			bottom.AddToClassList("v2-bottom");
			shell.Add(bottom);

			BuildLeft(bottom);
			BuildHand(bottom);
			BuildSheet(shell);

			if (away.HasAnything)
			{
				SayOnce(string.Format("자리 비운 {0} — 자원 +{1} · {2}마리 · 코스트 가득",
					DescribeSpan(away.CreditedSeconds),
					BigNumberText.Format(away.ResourceGained),
					BigNumberText.Format(away.KillsGained)), noteSeconds * 3f);
			}
		}

		private void BuildTop(VisualElement shell)
		{
			VisualElement top = new VisualElement();
			top.AddToClassList("v2-topbar");
			shell.Add(top);

			stageLabel = AddLabel(top, "v2-chip v2-chip--strong");
			resourceLabel = AddLabel(top, "v2-chip");

			// 적 체력 — 판의 주인공이라 화면 위 가운데.
			VisualElement enemy = new VisualElement();
			enemy.AddToClassList("v2-enemy");
			shell.Add(enemy);

			enemyLabel = AddLabel(enemy, "v2-enemy-label");

			VisualElement gauge = new VisualElement();
			gauge.AddToClassList("v2-enemy-gauge");
			enemy.Add(gauge);

			enemyFill = new VisualElement();
			enemyFill.AddToClassList("v2-enemy-fill");
			gauge.Add(enemyFill);

			// 웨이브 점 — 이 구역에 몇이 남았나 (막내가 보스).
			waveDots = new VisualElement();
			waveDots.AddToClassList("v2-wave");
			enemy.Add(waveDots);

			guideLabel = AddLabel(shell, "v2-guide");
			noteLabel = AddLabel(shell, "v2-note");
		}

		/// <summary>실패 배너 — 반복 중일 때만 뜬다 (V2 방향 5·6).</summary>
		private void BuildFailBanner(VisualElement shell)
		{
			failBanner = new VisualElement();
			failBanner.AddToClassList("v2-fail");
			failBanner.style.display = DisplayStyle.None;
			shell.Add(failBanner);

			failLabel = AddLabel(failBanner, "v2-fail-label");
			nextStageButton = AddButton(failBanner, "v2-button v2-button--next", NextStage);
		}

		private void BuildLeft(VisualElement bottom)
		{
			VisualElement left = new VisualElement();
			left.AddToClassList("v2-left");
			bottom.Add(left);

			// 우리 자리 넷 — 체력·부활이 여기 상주한다 (방향 3·7).
			VisualElement seats = new VisualElement();
			seats.AddToClassList("v2-seats");
			left.Add(seats);

			for (int seat = 0; seat < IdleSquad.SEAT_COUNT; seat++)
			{
				VisualElement card = new VisualElement();
				card.AddToClassList("v2-seat");
				seats.Add(card);
				seatCards.Add(card);

				seatLabels.Add(AddLabel(card, "v2-seat-name"));

				VisualElement bar = new VisualElement();
				bar.AddToClassList("v2-seat-gauge");
				card.Add(bar);

				VisualElement fill = new VisualElement();
				fill.AddToClassList("v2-seat-fill");
				bar.Add(fill);
				seatFills.Add(fill);
			}

			VisualElement verbs = new VisualElement();
			verbs.AddToClassList("v2-verbs");
			left.Add(verbs);

			for (int which = 0; which < SHEET_NAMES.Length; which++)
			{
				int captured = which;
				sheetOpeners[which] = AddButton(verbs, "v2-button", () => ToggleSheet(captured));
				sheetOpeners[which].text = SHEET_NAMES[which];
			}

			retreatButton = AddButton(verbs, "v2-button", Retreat);
			prestigeButton = AddButton(verbs, "v2-button v2-button--fold", Prestige);
		}

		private void BuildHand(VisualElement bottom)
		{
			VisualElement hand = new VisualElement();
			hand.AddToClassList("v2-hand");
			bottom.Add(hand);

			VisualElement cardRow = new VisualElement();
			cardRow.AddToClassList("v2-card-row");
			hand.Add(cardRow);

			cardButtons = new Button[IdleCards.CARD_COUNT];
			for (int index = 0; index < cardButtons.Length; index++)
			{
				IdleCardKind kind = (IdleCardKind)index;
				cardButtons[index] = AddButton(cardRow, "v2-card", () => Cast(kind));
			}

			VisualElement costRow = new VisualElement();
			costRow.AddToClassList("v2-cost-row");
			hand.Add(costRow);

			costLabel = AddLabel(costRow, "v2-cost-label");

			VisualElement gauge = new VisualElement();
			gauge.AddToClassList("v2-cost-gauge");
			costRow.Add(gauge);

			costFill = new VisualElement();
			costFill.AddToClassList("v2-cost-fill");
			gauge.Add(costFill);
		}

		/// <summary>장 하나에 세 페이지를 담는다 — 한 번에 하나만 열린다.</summary>
		private void BuildSheet(VisualElement shell)
		{
			sheetHost = new VisualElement();
			sheetHost.AddToClassList("v2-sheet");
			sheetHost.style.display = DisplayStyle.None;
			shell.Add(sheetHost);

			VisualElement head = new VisualElement();
			head.AddToClassList("v2-sheet-head");
			sheetHost.Add(head);

			sheetTitle = AddLabel(head, "v2-sheet-title");
			AddButton(head, "v2-button", CloseSheet).text = "닫기 ✕";

			ScrollView body = new ScrollView();
			body.AddToClassList("v2-sheet-body");
			sheetHost.Add(body);
			sheetBody = body.contentContainer;

			BuildUpgradePage();
			BuildGearPage();
			BuildHeroPage();
		}

		private VisualElement[] pages;

		private void BuildUpgradePage()
		{
			pages = new VisualElement[3];

			VisualElement page = AddPage();
			pages[0] = page;

			damageLabel = AddLabel(page, "v2-row-title");
			damageButton = AddButton(page, "v2-row-button", () => Raise(IdleUpgradeKind.Damage));
			speedLabel = AddLabel(page, "v2-row-title");
			speedButton = AddButton(page, "v2-row-button", () => Raise(IdleUpgradeKind.AttackSpeed));
			bulkRaiseButton = AddButton(page, "v2-row-button", RaiseMany);

			AddLabel(page, "v2-row-head").text = "기지 — 시간이 돈을 낸다";
			baseSummary = AddLabel(page, "v2-row-note");
			bulkBuyButton = AddButton(page, "v2-row-button", BuyMany);

			for (int kind = 0; kind < 8; kind++)
			{
				int captured = kind;
				producerButtons.Add(AddButton(page, "v2-row-button", () => BuyProducer(captured)));
			}
		}

		private void BuildGearPage()
		{
			VisualElement page = AddPage();
			pages[1] = page;

			gearSummary = AddLabel(page, "v2-row-note");
			wornLabel = AddLabel(page, "v2-row-title");

			bagGrid = new VisualElement();
			bagGrid.AddToClassList("v2-bag");
			page.Add(bagGrid);

			for (int index = 0; index < 40; index++)
			{
				int captured = index;
				Button cell = AddButton(bagGrid, "v2-bag-cell", () => Equip(captured));
				bagCells.Add(cell);
			}

			AddLabel(page, "v2-row-head").text = "합치기 — 같은 것 셋이 한 단계 위로";
			mergeRows = new VisualElement();
			page.Add(mergeRows);

			AddLabel(page, "v2-row-head").text = "감정 — 잠재를 굴린다";
			appraiseRows = new VisualElement();
			page.Add(appraiseRows);
		}

		private void BuildHeroPage()
		{
			VisualElement page = AddPage();
			pages[2] = page;

			pullButton = AddButton(page, "v2-row-button v2-row-button--strong", Pull);
			pullOdds = AddLabel(page, "v2-row-note");

			AddLabel(page, "v2-row-head").text = "파티 — 자리를 누르고 아래에서 고른다";
			partyRow = new VisualElement();
			partyRow.AddToClassList("v2-party");
			page.Add(partyRow);

			for (int slot = 0; slot < 3; slot++)
			{
				int captured = slot;
				partyButtons.Add(AddButton(partyRow, "v2-party-seat", () => BeginSeat(captured)));
			}

			codexLabel = AddLabel(page, "v2-row-title");
			heroRows = new VisualElement();
			page.Add(heroRows);
		}

		private VisualElement AddPage()
		{
			VisualElement page = new VisualElement();
			page.AddToClassList("v2-page");
			page.style.display = DisplayStyle.None;
			sheetBody.Add(page);
			return page;
		}

		// ── 그리기 ──────────────────────────────────────────────────────────

		public void Render(IdleSnapshot snapshot)
		{
			if (stageLabel == null)
			{
				return;
			}

			stageLabel.text = string.Format("{0}구역 · 등급 {1}/{2}{3}",
				snapshot.Stage, snapshot.MaxTierNow, snapshot.TierCeiling,
				snapshot.MaxTierNow >= snapshot.TierCeiling ? " (천장)" : string.Empty);

			resourceLabel.text = string.Format("재료 {0}  (+{1}/초{2})",
				BigNumberText.Format(snapshot.Resource),
				BigNumberText.Format(snapshot.IncomePerSecond),
				snapshot.SupplySecondsLeft > 0d
					? string.Format(" · 보급 ×{0:0.#} {1:0}초", session.Tuning.SupplyMultiplier, snapshot.SupplySecondsLeft)
					: string.Empty);

			guideLabel.text = NextStep(snapshot);

			RenderEnemy(snapshot);
			RenderSeats(snapshot);
			RenderHand(snapshot);
			RenderVerbs(snapshot);

			if (openSheet >= 0)
			{
				RenderSheet(snapshot);
			}
		}

		private void RenderEnemy(IdleSnapshot snapshot)
		{
			// ★ 상단 대형 바는 <b>보스 전용</b> (실조사 `refs/blue-archive.md` § 2·6).
			//   잡몹 체력은 <b>머리 위</b>에 뜬다 — 화면 위에 또 그리면 같은 값이 두 벌이 된다.
			bool boss = snapshot.KillsInStage >= snapshot.KillsPerStage - 1;

			enemyLabel.text = boss
				? string.Format("BATTLE BOSS — {0}구역   {1:P0}", snapshot.Stage, snapshot.TargetHealthRatio)
				: string.Format("{0}구역 · 남은 적 {1} · 적 초당 피해 {2}",
					snapshot.Stage,
					snapshot.KillsPerStage - snapshot.KillsInStage,
					BigNumberText.Format(snapshot.EnemyDamagePerSecond));

			enemyLabel.EnableInClassList("v2-enemy-label--boss", boss);

			enemyFill.parent.style.display = boss ? DisplayStyle.Flex : DisplayStyle.None;
			enemyFill.style.width = new StyleLength(new Length(
				(float)(snapshot.TargetHealthRatio * 100d), LengthUnit.Percent));

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
		}

		private void RenderSeats(IdleSnapshot snapshot)
		{
			for (int seat = 0; seat < seatCards.Count && seat < snapshot.Seats.Length; seat++)
			{
				IdleSeatView view = snapshot.Seats[seat];

				seatCards[seat].style.display = view.Taken ? DisplayStyle.Flex : DisplayStyle.None;

				if (view.Taken == false)
				{
					continue;
				}

				// ★ 체력 막대는 <b>머리 위</b>가 정본이다 (실조사). 여기 카드는 <b>이름표</b>만 —
				//   누가 나와 있고 누가 누웠는지, 쓰러졌으면 언제 일어나는지.
				string who = seat == 0 ? "나" : IdleHeroes.KindOf(view.HeroId).Name;

				seatLabels[seat].text = view.Standing
					? who
					: string.Format("{0} — 부활 {1:P0}", who, view.ReviveRatio);

				seatFills[seat].style.width = new StyleLength(new Length(
					(float)((view.Standing ? view.HealthRatio : view.ReviveRatio) * 100d), LengthUnit.Percent));
				seatFills[seat].EnableInClassList("v2-seat-fill--down", view.Standing == false);
			}
		}

		private void RenderHand(IdleSnapshot snapshot)
		{
			for (int index = 0; index < cardButtons.Length; index++)
			{
				IdleCardView card = snapshot.Cards[index];
				cardButtons[index].text = string.Format("{0}\n{1}코", NameOf(card.Kind), card.Cost);
				cardButtons[index].SetEnabled(card.CanCast);
				cardButtons[index].EnableInClassList("v2-card--ready", card.CanCast);
			}

			costLabel.text = string.Format("코스트 {0:0.#}/{1:0.#}", snapshot.Cost, snapshot.CostMax);
			costFill.style.width = new StyleLength(new Length(
				snapshot.CostMax > 0d ? (float)(snapshot.Cost / snapshot.CostMax * 100d) : 0f,
				LengthUnit.Percent));
		}

		private void RenderVerbs(IdleSnapshot snapshot)
		{
			bool canRetreat = snapshot.Stage > snapshot.BestFarmingStage;
			int going = canRetreat ? snapshot.BestFarmingStage : snapshot.BestStage;
			retreatButton.text = canRetreat
				? string.Format("◀ {0}구역", snapshot.BestFarmingStage)
				: string.Format("▶ {0}구역", snapshot.BestStage);
			retreatButton.SetEnabled(IdleModel.CanGoToStage(session.State, going));

			prestigeButton.text = snapshot.PrestigeAward > 0L
				? string.Format("환생 ↺ {0}", snapshot.PrestigeAward)
				: "환생";
			prestigeButton.SetEnabled(snapshot.PrestigeAward > 0L);
			prestigeButton.EnableInClassList("v2-button--ready", snapshot.PrestigeAward > 0L);

			// 실패 배너 — 반복 중일 때만.
			failBanner.style.display = snapshot.Repeating ? DisplayStyle.Flex : DisplayStyle.None;

			if (snapshot.Repeating)
			{
				failLabel.text = string.Format("전멸 — {0}구역을 반복하는 중. 채비가 되면 다시 내려간다",
					snapshot.Stage);
				nextStageButton.text = string.Format("{0}구역에 다시 도전 ▶", snapshot.Stage + 1);
			}

			// 할 일이 있는 장에는 불이 들어온다.
			sheetOpeners[0].EnableInClassList("v2-button--ready",
				IdleAdvice.HasSomethingToDo(snapshot, IdleTab.Upgrade)
				|| IdleAdvice.HasSomethingToDo(snapshot, IdleTab.Base));
			sheetOpeners[1].EnableInClassList("v2-button--ready",
				IdleAdvice.HasSomethingToDo(snapshot, IdleTab.Gear));
			sheetOpeners[2].EnableInClassList("v2-button--ready",
				IdleAdvice.HasSomethingToDo(snapshot, IdleTab.Hero));
		}

		private void RenderSheet(IdleSnapshot snapshot)
		{
			switch (openSheet)
			{
				case 0: RenderUpgradePage(snapshot); break;
				case 1: RenderGearPage(snapshot); break;
				default: RenderHeroPage(snapshot); break;
			}
		}

		private void RenderUpgradePage(IdleSnapshot snapshot)
		{
			DrawUpgrade(snapshot.Damage, damageLabel, damageButton, "공격력", "한 방 {0}");
			DrawUpgrade(snapshot.AttackSpeed, speedLabel, speedButton, "공격속도", "초당 {0}회");

			bool canRaise = IdleModel.CheapestRaisableAxis(session.State, session.Tuning, out IdleUpgradeKind _);
			bulkRaiseButton.text = canRaise ? "싼 축부터 몰아 올린다" : "올릴 수 있는 게 없다";
			bulkRaiseButton.SetEnabled(canRaise);

			baseSummary.text = string.Format("기지가 초당 {0} 를 낸다",
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
				producerButtons[kind].text = string.Format("{0}번 ×{1} · 초당 {2} — {3}",
					kind + 1, view.Owned,
					BigNumberText.Format(view.OutputTotal),
					BigNumberText.Format(view.NextCost));
				producerButtons[kind].SetEnabled(view.CanAfford);
			}
		}

		private void RenderGearPage(IdleSnapshot snapshot)
		{
			bool full = snapshot.Bag.Length >= snapshot.BagCapacity;
			gearSummary.text = string.Format("가방 {0}/{1}{2}",
				snapshot.Bag.Length, snapshot.BagCapacity,
				full ? "  ⚠ 꽉 찼다 — 합치거나 차야 새 장비가 들어온다" : string.Empty);
			gearSummary.EnableInClassList("v2-warn", full);

			string worn = "차고 있는 것 —";
			for (int slot = 0; slot < snapshot.Worn.Length && slot < SLOT_NAMES.Length; slot++)
			{
				IdleItem one = snapshot.Worn[slot];
				worn += string.Format("  {0} {1}", SLOT_NAMES[slot], one.IsEmpty ? "빔" : one.Tier + "등급");
			}

			wornLabel.text = worn;

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
				bagCells[index].text = string.Format("{0}\n{1}", one.Tier, SLOT_NAMES[(int)one.Slot].Substring(0, 1));
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

				labels.Add(string.Format("{0}등급 {1} ×{2} → {3}등급  ({4})",
					tier, SLOT_NAMES[(int)slot], counts[key], tier + 1, BigNumberText.Format(cost)));
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
					? string.Format("{0}등급 {1}개 — 잠재 없음", tier, BigNumberText.Format(count))
					: string.Format("{0}등급 {1}개 — 감정 {2}", tier, BigNumberText.Format(count),
						BigNumberText.Format(cost));
				appraiseButtons[tier - 1].SetEnabled(why == AppraiseBlock.None);
			}
		}

		private void RenderHeroPage(IdleSnapshot snapshot)
		{
			pullButton.text = snapshot.CanPull
				? string.Format("영웅 뽑기 — 자원 {0} + 환생석 {1} (가진 돌 {2})",
					BigNumberText.Format(snapshot.PullCost), snapshot.PullStoneCost, snapshot.Stones)
				: snapshot.Stones < snapshot.PullStoneCost
					? string.Format("환생석이 없다 (환생하면 생긴다 · 가진 돌 {0})", snapshot.Stones)
					: string.Format("자원 {0} 이 모자란다", BigNumberText.Format(snapshot.PullCost));
			pullButton.SetEnabled(snapshot.CanPull);

			pullOdds.text = string.Format("레전드 {0:P1} · 에픽 {1:P0} · 레어 {2:P0} · {3}번 안에 레전드 보장",
				snapshot.LegendChance, snapshot.EpicChance, snapshot.RareChance, snapshot.PullsToPity);

			for (int slot = 0; slot < partyButtons.Count; slot++)
			{
				int id = slot < snapshot.Party.Length ? snapshot.Party[slot] : -1;
				partyButtons[slot].text = id >= 0
					? IdleHeroes.KindOf(id).Name
					: (snapshot.Heroes.Length > 0 ? "비었다" : "뽑으면 여기");
				partyButtons[slot].EnableInClassList("v2-party-seat--picking", seatBeingFilled == slot);
			}

			codexLabel.text = string.Format("도감 {0}점 · 판 전체 ×{1:0.00}{2}",
				snapshot.CodexScore, snapshot.CodexMultiplier,
				seatBeingFilled >= 0 ? "   ▶ 아래에서 앉힐 얼굴을 고른다" : string.Empty);

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
				heroButtons[index].text = string.Format("{0}{1} · {2} — 보유 +{3:P0} ({4}/{5}){6}",
					hero.Name, Stars(hero.Stars),
					IdleHeroes.NameOfGrade(hero.Grade), hero.OwnedShare,
					hero.Copies, hero.CopiesForNextStar,
					hero.InParty ? "  ▶출전" : string.Empty);
			}
		}

		/// <summary>코어가 고른 한 걸음을 사람 말로 — V2 지리로.</summary>
		private string NextStep(IdleSnapshot snapshot)
		{
			if (snapshot.Repeating)
			{
				return "▶ 전멸했다 — 강화·장비를 손보고 다시 도전한다";
			}

			IdleAdviceResult advice = IdleAdvice.NextStep(snapshot);

			switch (advice.Step)
			{
				case IdleStep.Prestige:
					return string.Format("▶ 환생할 때다 — 낱장 {0}", (long)advice.Amount);

				case IdleStep.BuyProducer:
					return "▶ 강화 장 — 기지에 살 것이 있다";

				case IdleStep.Raise:
					return "▶ 강화 장 — 올릴 것이 있다";

				case IdleStep.Merge:
					return "▶ 장비 장 — 합칠 수 있다";

				case IdleStep.Wear:
					return "▶ 장비 장 — 가방에 더 좋은 것이 있다";

				case IdleStep.Pull:
					return "▶ 인형 장 — 뽑을 수 있다";

				case IdleStep.Seat:
					return "▶ 인형 장 — 자리가 비었다 (앉히는 데는 아무것도 안 든다)";

				case IdleStep.BagFull:
					return "▶ 장비 장 — 가방이 꽉 찼다";

				case IdleStep.Tap:
					return "▶ 무대를 눌러 응원한다";

				default:
					return advice.Amount > 0d && double.IsInfinity(advice.Amount) == false
						? string.Format("· 모으는 중 — {0} 뒤에 살 것이 생긴다", DescribeSpan(advice.Amount))
						: "· 모으는 중 — 코스트가 차면 카드를 낸다";
			}
		}

		// ── 의도 ────────────────────────────────────────────────────────────

		private void OnTapped(PointerDownEvent moment)
		{
			if (moment.target is Button)
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

		private void ToggleSheet(int which)
		{
			openSheet = openSheet == which ? -1 : which;
			sheetHost.style.display = openSheet >= 0 ? DisplayStyle.Flex : DisplayStyle.None;

			if (openSheet >= 0)
			{
				sheetTitle.text = SHEET_NAMES[openSheet];
			}

			for (int page = 0; page < pages.Length; page++)
			{
				pages[page].style.display = page == openSheet ? DisplayStyle.Flex : DisplayStyle.None;
			}

			if (openSheet != 2)
			{
				seatBeingFilled = -1;
			}

			Render(session.Capture());
		}

		private void CloseSheet()
		{
			openSheet = -1;
			sheetHost.style.display = DisplayStyle.None;
			seatBeingFilled = -1;
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
					SayOnce("일제 사격! — 모두 달려들었다", noteSeconds);
					break;

				case IdleCardKind.Supply:
					if (stage != null) { stage.OnSupply((float)session.Tuning.SupplySeconds); }
					SayOnce(string.Format("긴급 보급! — {0:0}초 동안 수입 ×{1:0.#}",
						session.Tuning.SupplySeconds, session.Tuning.SupplyMultiplier), noteSeconds);
					break;

				default:
					SayOnce(result.HasRoll
						? string.Format("비밀 감정 — {0}등급 → {1:P1}{2}",
							result.Roll.Tier, result.Roll.Value, result.Roll.Replaced ? " ★ 갈아 끼움" : string.Empty)
						: "비밀 감정 — 굴릴 것이 없다", noteSeconds);
					break;
			}

			WriteDown();
			Render(session.Capture());
		}

		private void NextStage()
		{
			if (session.Send(new IdleNextStageIntent()))
			{
				SayOnce("다시 내려간다 — 부대는 만전이다", noteSeconds);
				WriteDown();
			}

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
				SayOnce(string.Format("강화 — {0}번 올렸다", raised), noteSeconds);
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
				SayOnce(string.Format("기지 — {0}개 샀다", bought), noteSeconds);
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
				SayOnce(string.Format("{0}등급 셋을 합쳤다", tier), noteSeconds);
				WriteDown();
			}

			Render(session.Capture());
		}

		private void Appraise(int tier)
		{
			if (session.TryAppraise(tier, out PotentialRoll roll))
			{
				SayOnce(string.Format("{0}등급 감정 → {1:P1}{2}",
					roll.Tier, roll.Value, roll.Replaced ? " ★ 갈아 끼움" : string.Empty), noteSeconds);
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
				got.IsNew ? "  ★ 처음 본 얼굴" : string.Empty,
				got.ByPity ? "  (천장)" : string.Empty), noteSeconds * 2f);

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
				SayOnce("자리가 다 찼다 — 바꿀 자리를 먼저 누른다", noteSeconds);
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

		private void Retreat()
		{
			IdleSnapshot now = session.Capture();
			int target = now.Stage > now.BestFarmingStage ? now.BestFarmingStage : now.BestStage;

			session.Send(new IdleGoToStageIntent(target));
			WriteDown();
			Render(session.Capture());
		}

		private void Prestige()
		{
			if (session.Send(new IdlePrestigeIntent()))
			{
				SayOnce("환생 — 새 종이. 코스트는 그대로다", noteSeconds * 2f);
				WriteDown();
			}

			Render(session.Capture());
		}

		// ── 잔손 ────────────────────────────────────────────────────────────

		private void SayOnce(string what, float seconds)
		{
			noteLabel.text = what;
			noteLabel.style.opacity = 1f;
			noteLeft = seconds;
		}

		private static void DrawUpgrade(IdleUpgradeView view, Label label, Button button,
			string name, string valueFormat)
		{
			label.text = string.Format("{0} Lv.{1} — " + valueFormat, name, view.Level,
				BigNumberText.Format(view.CurrentValue));

			button.text = view.IsMaxed
				? "최대"
				: string.Format("올리기 — {0}", BigNumberText.Format(view.NextCost));
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
			Button button = new Button(action);
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
