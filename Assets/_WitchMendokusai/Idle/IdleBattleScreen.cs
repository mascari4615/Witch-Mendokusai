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
	/// ★ 상주하는 것은 <b>무대와 손패</b>뿐이다. 전투는 무대(<see cref="IdleBattleStage"/>)가
	///   그리고, 개입은 카드(코스트)로 모인다 — 블아 문법의 idle 재해석.
	///
	/// ★ 이 조각(2A)의 경계 — 관리는 몰아 사기·몰아 올리기·물러나기·환생 네 동사만 싣는다.
	///   장 4종(기지·강화·장비·인형) 이식은 다음 조각(2B). 지나가는 것(별똥)은
	///   카드층으로 흡수 예정이라 여기 안 그린다 (concept-v2 § 다음 4).
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

		private Label stageLabel;
		private Label resourceLabel;
		private Label guideLabel;
		private Label noteLabel;
		private float noteLeft;

		private Button[] cardButtons;
		private VisualElement costFill;
		private Label costLabel;

		private Button bulkBuyButton;
		private Button bulkRaiseButton;
		private Button retreatButton;
		private Button prestigeButton;

		public PresentationKind Kind => PresentationKind.UIOnly;

		private void OnEnable()
		{
			if (tuningAsset == null)
			{
				Debug.LogWarning("[IdleV2] 수치 에셋이 안 꽂혀 있다 — 코드 기본값으로 돈다."
					+ " 인스펙터에서 고친 숫자는 하나도 안 먹는다.");
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

			session.Advance(delta);
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

			VisualElement top = new VisualElement();
			top.AddToClassList("v2-topbar");
			shell.Add(top);

			stageLabel = AddLabel(top, "v2-chip v2-chip--strong");
			resourceLabel = AddLabel(top, "v2-chip");

			guideLabel = AddLabel(shell, "v2-guide");
			noteLabel = AddLabel(shell, "v2-note");

			VisualElement bottom = new VisualElement();
			bottom.AddToClassList("v2-bottom");
			shell.Add(bottom);

			VisualElement verbs = new VisualElement();
			verbs.AddToClassList("v2-verbs");
			bottom.Add(verbs);

			bulkBuyButton = AddButton(verbs, "v2-button", BuyMany);
			bulkRaiseButton = AddButton(verbs, "v2-button", RaiseMany);
			retreatButton = AddButton(verbs, "v2-button", Retreat);
			prestigeButton = AddButton(verbs, "v2-button v2-button--fold", Prestige);

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

			if (away.HasAnything)
			{
				SayOnce(string.Format("자리 비운 {0} — 자원 +{1} · {2}마리 · 코스트 가득",
					DescribeSpan(away.CreditedSeconds),
					BigNumberText.Format(away.ResourceGained),
					BigNumberText.Format(away.KillsGained)), noteSeconds * 3f);
			}
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

			bool canBuy = IdleBase.CheapestAffordable(session.State, session.Tuning) >= 0;
			bulkBuyButton.text = canBuy ? "기지 — 싼 것부터 산다" : "기지 — 살 것 없음";
			bulkBuyButton.SetEnabled(canBuy);
			bulkBuyButton.EnableInClassList("v2-button--ready", canBuy);

			bool canRaise = IdleModel.CheapestRaisableAxis(session.State, session.Tuning, out IdleUpgradeKind _);
			bulkRaiseButton.text = canRaise ? "강화 — 싼 축부터 올린다" : "강화 — 올릴 것 없음";
			bulkRaiseButton.SetEnabled(canRaise);
			bulkRaiseButton.EnableInClassList("v2-button--ready", canRaise);

			bool canRetreat = snapshot.Stage > snapshot.BestFarmingStage;
			int going = canRetreat ? snapshot.BestFarmingStage : snapshot.BestStage;
			retreatButton.text = canRetreat
				? string.Format("◀ {0}구역으로 물러나 번다", snapshot.BestFarmingStage)
				: string.Format("▶ 가장 깊은 {0}구역으로", snapshot.BestStage);
			retreatButton.SetEnabled(IdleModel.CanGoToStage(session.State, going));

			prestigeButton.text = snapshot.PrestigeAward > 0L
				? string.Format("환생 ↺ — 낱장 {0}", snapshot.PrestigeAward)
				: string.Format("환생 — {0}구역부터 값어치", snapshot.PrestigeNextStage);
			prestigeButton.SetEnabled(snapshot.PrestigeAward > 0L);
			prestigeButton.EnableInClassList("v2-button--ready", snapshot.PrestigeAward > 0L);
		}

		/// <summary>코어가 고른 한 걸음을 사람 말로 — V2 지리로 옮긴다.</summary>
		private string NextStep(IdleSnapshot snapshot)
		{
			IdleAdviceResult advice = IdleAdvice.NextStep(snapshot);

			switch (advice.Step)
			{
				case IdleStep.Prestige:
					return string.Format("▶ 환생할 때다 — 낱장 {0} (천장도 오른다)", (long)advice.Amount);

				case IdleStep.BuyProducer:
					return "▶ 기지 — 살 것이 있다";

				case IdleStep.Raise:
					return "▶ 강화 — 올릴 것이 있다";

				case IdleStep.Tap:
					return "▶ 무대를 눌러 응원한다 — 지금은 손이 제일 빠르다";

				case IdleStep.BagFull:
					return "▶ 가방이 꽉 찼다 — 장비 정리는 다음 조각(2B)에서 온다";

				default:
					return advice.Amount > 0d && double.IsInfinity(advice.Amount) == false
						? string.Format("· 모으는 중 — {0} 뒤에 살 것이 생긴다", DescribeSpan(advice.Amount))
						: "· 모으는 중 — 코스트가 차면 카드를 낸다";
			}
		}

		// ── 의도 ────────────────────────────────────────────────────────────

		private void OnTapped(PointerDownEvent moment)
		{
			// 버튼을 누른 것은 응원이 아니다 — 버블링으로 같이 세지 않게 거른다.
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
						? string.Format("비밀 감정 — ◆{0} → {1:P1}{2}",
							result.Roll.Tier, result.Roll.Value, result.Roll.Replaced ? " ★ 갈아 끼움" : string.Empty)
						: "비밀 감정 — 굴릴 것이 없다", noteSeconds);
					break;
			}

			WriteDown();
			Render(session.Capture());
		}

		private void BuyMany()
		{
			int bought = session.BuyAsManyProducersAsAfforded();
			if (bought > 0)
			{
				SayOnce(string.Format("기지 — {0}개를 한 번에 샀다", bought), noteSeconds);
				WriteDown();
			}

			Render(session.Capture());
		}

		private void RaiseMany()
		{
			int raised = session.RaiseAsManyAsAfforded();
			if (raised > 0)
			{
				SayOnce(string.Format("강화 — {0}번을 한 번에 올렸다", raised), noteSeconds);
				WriteDown();
			}

			Render(session.Capture());
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
