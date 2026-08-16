using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using WitchMendokusai.DomainSDK.Contracts;
using WitchMendokusai.DomainSDK.Idle;
// 네임스페이스를 통째로 들이면 Vector2 가 UnityEngine 것과 겹친다 — 쓸 것만 별칭으로 들인다.
using BigNumberText = WitchMendokusai.Numerics.BigNumberText;

namespace WitchMendokusai
{
	/// <summary>
	/// 방치 게임 화면 (TASK-WM-406).
	///
	/// ★ 이 파일에 게임 규칙이 한 줄도 없다 — 사진을 받아 그리고, 의도를 보낸다.
	///   에디터 창과 같은 코어·같은 계약이고 다른 것은 그릇뿐이다.
	///
	/// ★ 짜임 (사용자 컨펌 2026-08-16): 위 요약 띠 · 아래 두 칸(판 · 조작).
	///   조작은 <b>탭</b>으로 묶는다 — 세로로 늘어놓으면 버튼이 늘 때마다 화면이 길어지고
	///   지금 무엇을 하는 중인지가 안 보인다.
	///
	/// ★ 보이는 것은 <b>기하학적 도형</b>이다(사용자 방향: 세계관 정하기 전).
	///   규칙 하나로 읽힌다 — <b>변의 수 = 등급</b>. 1등급 삼각형 … 8등급 십각형.
	///   숫자를 안 읽어도 변만 세면 등급을 안다.
	///
	/// ★ 해상도 — 고정 픽셀을 최소로 쓰고 flex 로 늘린다. 좁으면 두 칸이 한 칸으로 접힌다.
	/// </summary>
	[RequireComponent(typeof(UIDocument))]
	public sealed class IdleScreen : MonoBehaviour, IGameView<IdleSnapshot>
	{
		private enum Tab
		{
			Base = 0,
			Upgrade = 1,
			Gear = 2,
			Fold = 3,
		}

		[Header("수치 — 비워 두면 코드 기본값")]
		[SerializeField] private IdleTuningSO tuningAsset;

		[Header("생김새")]
		[SerializeField] private StyleSheet styleSheet;

		[Header("얼마나 자주 적나 (초)")]
		[SerializeField] private float saveIntervalSeconds = 10f;

		private IdleSession session;
		private float sinceLastSave;
		private long lastKills;

		private Label stageLabel;
		private Label resourceLabel;
		private Label topNoteLabel;

		private IdleShapeElement targetShape;
		private IdleBurstElement burst;
		private readonly List<IdleShapeElement> allies = new List<IdleShapeElement>();

		/// <summary>목록에 붙은 작은 도형들 — 같이 돌려야 화면이 살아 있다.</summary>
		private readonly List<IdleShapeElement> decor = new List<IdleShapeElement>();
		private ProgressBar healthBar;
		private readonly List<VisualElement> killDots = new List<VisualElement>();
		private Label arenaCaption;

		private readonly List<Button> tabButtons = new List<Button>();
		private VisualElement basePage;
		private readonly List<Button> producerButtons = new List<Button>();
		private readonly List<IdleShapeElement> producerShapes = new List<IdleShapeElement>();
		private Label baseSummary;

		private VisualElement upgradePage;
		private VisualElement gearPage;
		private VisualElement foldPage;

		private Label damageTitle;
		private Label damageValue;
		private Button damageButton;
		private Label speedTitle;
		private Label speedValue;
		private Button speedButton;
		private Button retreatButton;
		private Button holdButton;

		private Label potentialLabel;
		private Label wornLabel;
		private VisualElement mergeRows;
		private readonly List<Button> mergeButtons = new List<Button>();
		private VisualElement bagRows;
		private readonly List<Button> bagButtons = new List<Button>();
		private readonly List<IdleShapeElement> bagShapes = new List<IdleShapeElement>();
		private VisualElement dropRows;
		private readonly List<Button> appraiseButtons = new List<Button>();
		private Label rollNote;

		private Label foldSummary;
		private Button prestigeButton;

		public PresentationKind Kind => PresentationKind.UIOnly;

		private void OnEnable()
		{
			IdleTuning tuning = tuningAsset != null ? tuningAsset.ToTuning() : new IdleTuning();

			IdleState state = new IdleState();
			IdleSaveData? saved = IdleSaveStore.Load();
			if (saved.HasValue)
			{
				state.Load(saved.Value);
			}

			session = new IdleSession(tuning, state);

			// ★ 화면을 짓기 전에 자리 비운 몫을 쳐준다 — 첫 그림이 이미 받은 뒤의 판이라야 한다.
			double away = session.CatchUp(IdleSaveStore.NowUnixSeconds());

			BuildInterface(away);
			lastKills = session.State.Kills;
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
			IdleSnapshot snapshot = session.Capture();

			// ★ 잡힌 순간을 눈으로 보여준다 — 자동 전투일수록 「일이 일어났다」는 신호가 필요하다.
			if (snapshot.Kills > lastKills)
			{
				lastKills = snapshot.Kills;
				burst.Fire(snapshot.MaxTierNow, TierColor(snapshot.MaxTierNow));
				targetShape.Hit();
			}

			targetShape.Advance(delta, 0.08f);
			burst.Advance(delta);
			for (int index = 0; index < allies.Count; index++)
			{
				allies[index].Advance(delta, 0.35f + index * 0.1f);
			}

			for (int index = 0; index < decor.Count; index++)
			{
				decor[index].Advance(delta, 0.05f);
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

		private void BuildInterface(double awaySeconds)
		{
			VisualElement root = GetComponent<UIDocument>().rootVisualElement;
			root.Clear();

			if (styleSheet != null)
			{
				root.styleSheets.Add(styleSheet);
			}

			VisualElement shell = new VisualElement();
			shell.AddToClassList("idle-root");
			root.Add(shell);

			BuildTopBar(shell, awaySeconds);

			VisualElement body = new VisualElement();
			body.AddToClassList("idle-body");
			shell.Add(body);

			BuildArena(body);
			BuildPanel(body);

			ShowTab(Tab.Base);
		}

		private void BuildTopBar(VisualElement parent, double awaySeconds)
		{
			VisualElement bar = new VisualElement();
			bar.AddToClassList("idle-topbar");
			parent.Add(bar);

			stageLabel = AddLabel(bar, "idle-top-stage");
			resourceLabel = AddLabel(bar, "idle-top-resource");
			topNoteLabel = AddLabel(bar, "idle-top-note");

			if (awaySeconds > 0d)
			{
				topNoteLabel.text = string.Format("자리 비운 {0} 동안도 잡아 뒀다", DescribeSpan(awaySeconds));
			}
		}

		private void BuildArena(VisualElement parent)
		{
			VisualElement arena = new VisualElement();
			arena.AddToClassList("idle-arena");
			parent.Add(arena);

			VisualElement box = new VisualElement();
			box.AddToClassList("idle-stage-box");
			arena.Add(box);

			targetShape = new IdleShapeElement();
			targetShape.AddToClassList("idle-shape");
			box.Add(targetShape);

			burst = new IdleBurstElement();
			burst.AddToClassList("idle-shape");
			box.Add(burst);

			healthBar = new ProgressBar();
			healthBar.lowValue = 0f;
			healthBar.highValue = 1f;
			healthBar.AddToClassList("idle-health");
			arena.Add(healthBar);

			VisualElement dots = new VisualElement();
			dots.AddToClassList("idle-kills-dots");
			arena.Add(dots);
			killDots.Clear();

			VisualElement allyRow = new VisualElement();
			allyRow.AddToClassList("idle-ally-row");
			arena.Add(allyRow);
			allies.Clear();

			for (int one = 0; one < 3; one++)
			{
				IdleShapeElement ally = new IdleShapeElement();
				ally.AddToClassList("idle-ally");
				ally.Tier = 1;
				ally.Body = new Color(0.72f, 0.78f, 0.52f);
				allyRow.Add(ally);
				allies.Add(ally);
			}

			arenaCaption = AddLabel(arena, "idle-arena-caption");
		}

		private void BuildPanel(VisualElement parent)
		{
			VisualElement panel = new VisualElement();
			panel.AddToClassList("idle-panel");
			parent.Add(panel);

			VisualElement tabs = new VisualElement();
			tabs.AddToClassList("idle-tabs");
			panel.Add(tabs);

			tabButtons.Clear();
			AddTab(tabs, "기지", Tab.Base);
			AddTab(tabs, "강화", Tab.Upgrade);
			AddTab(tabs, "장비", Tab.Gear);
			AddTab(tabs, "접기", Tab.Fold);

			basePage = AddPage(panel);
			upgradePage = AddPage(panel);
			gearPage = AddPage(panel);
			foldPage = AddPage(panel);

			BuildBasePage();
			BuildUpgradePage();
			BuildGearPage();
			BuildFoldPage();
		}

		/// <summary>
		/// 기지 — <b>시간이 자원을 낸다</b>. 이 층이 없으면 감정도 합치기도 강화도 못 한다.
		/// </summary>
		private void BuildBasePage()
		{
			baseSummary = AddLabel(basePage, "idle-row-value");

			producerButtons.Clear();
			producerShapes.Clear();

			for (int kind = 0; kind < 8; kind++)
			{
				int captured = kind;
				producerButtons.Add(AddShapeRow(basePage, kind + 1, () => BuyProducer(captured),
					out IdleShapeElement shape));
				producerShapes.Add(shape);
			}
		}

		private void BuildUpgradePage()
		{
			damageTitle = AddLabel(upgradePage, "idle-row-title");
			damageValue = AddLabel(upgradePage, "idle-row-value");
			damageButton = AddButton(upgradePage, "idle-button idle-button--strong",
				() => Send(IdleUpgradeKind.Damage));

			speedTitle = AddLabel(upgradePage, "idle-row-title");
			speedValue = AddLabel(upgradePage, "idle-row-value");
			speedButton = AddButton(upgradePage, "idle-button idle-button--strong",
				() => Send(IdleUpgradeKind.AttackSpeed));

			AddDivider(upgradePage);

			retreatButton = AddButton(upgradePage, "idle-button", Retreat);
			holdButton = AddButton(upgradePage, "idle-button", ToggleHold);
		}

		/// <summary>
		/// 장비 — 모험이 가져온 것. 차고, 합치고, 감정한다.
		///
		/// ★ 셋 다 <b>자원</b>이 든다(감정·합치기). 그게 기지와 모험을 같은 저울에 올리는 자리다.
		/// </summary>
		private void BuildGearPage()
		{
			wornLabel = AddLabel(gearPage, "idle-row-title");
			wornLabel.style.whiteSpace = WhiteSpace.Normal;

			AddDivider(gearPage);
			AddLabel(gearPage, "idle-row-value").text = "가방 — 눌러서 찬다";

			bagRows = new VisualElement();
			gearPage.Add(bagRows);

			AddDivider(gearPage);
			AddLabel(gearPage, "idle-row-value").text = "합치기 — 같은 부위·등급 셋이 한 단계 위로 (잠재는 사라진다)";

			mergeRows = new VisualElement();
			gearPage.Add(mergeRows);

			AddDivider(gearPage);
			potentialLabel = AddLabel(gearPage, "idle-row-title");

			dropRows = new VisualElement();
			gearPage.Add(dropRows);

			rollNote = AddLabel(gearPage, "idle-note");
		}

		private void BuildFoldPage()
		{
			foldSummary = AddLabel(foldPage, "idle-row-title");
			foldSummary.style.whiteSpace = WhiteSpace.Normal;

			prestigeButton = AddButton(foldPage, "idle-button idle-button--strong", Prestige);
		}

		// ── 그리기 ──────────────────────────────────────────────────────────

		public void Render(IdleSnapshot snapshot)
		{
			if (stageLabel == null)
			{
				return;
			}

			bool atCeiling = snapshot.MaxTierNow >= snapshot.TierCeiling;

			stageLabel.text = string.Format("{0}단계 · 등급 {1}/{2}{3}",
				snapshot.Stage, snapshot.MaxTierNow, snapshot.TierCeiling,
				atCeiling ? " (천장)" : string.Empty);

			resourceLabel.text = string.Format("{0}  ({1}/초)",
				BigNumberText.Format(snapshot.Resource), BigNumberText.Format(snapshot.IncomePerSecond));

			targetShape.Tier = snapshot.MaxTierNow;
			targetShape.Body = TierColor(snapshot.MaxTierNow);
			targetShape.Fill = (float)snapshot.TargetHealthRatio;

			healthBar.value = (float)snapshot.TargetHealthRatio;
			healthBar.title = string.Format("{0:P0}", snapshot.TargetHealthRatio);

			DrawKillDots(snapshot);

			arenaCaption.text = snapshot.HoldingStage
				? string.Format("여기서 사냥 중 — 많이 떨군다 (변 {0}개 = {0} 등급까지)", snapshot.MaxTierNow + 2)
				: string.Format("내려가는 중 — 좋은 게 떨어진다 (변 {0}개 = {1}등급)",
					snapshot.MaxTierNow + 2, snapshot.MaxTierNow);

			RenderBasePage(snapshot);
			RenderUpgradePage(snapshot);
			RenderGearPage(snapshot);
			RenderFoldPage(snapshot);
		}

		private void DrawKillDots(IdleSnapshot snapshot)
		{
			VisualElement dots = killDots.Count > 0 ? killDots[0].parent : null;
			if (dots == null)
			{
				dots = arenaCaption.parent.Q(className: "idle-kills-dots");
			}

			if (dots == null)
			{
				return;
			}

			if (killDots.Count != snapshot.KillsPerStage)
			{
				dots.Clear();
				killDots.Clear();

				for (int one = 0; one < snapshot.KillsPerStage; one++)
				{
					VisualElement dot = new VisualElement();
					dot.AddToClassList("idle-dot");
					dots.Add(dot);
					killDots.Add(dot);
				}
			}

			for (int index = 0; index < killDots.Count; index++)
			{
				killDots[index].EnableInClassList("idle-dot--done", index < snapshot.KillsInStage);
			}
		}

		private void RenderBasePage(IdleSnapshot snapshot)
		{
			baseSummary.text = string.Format("기지가 초당 {0} 를 낸다 — 자원은 여기서만 나온다",
				BigNumberText.Format(snapshot.IncomePerSecond));

			for (int kind = 0; kind < producerButtons.Count; kind++)
			{
				Button button = producerButtons[kind];

				if (kind >= snapshot.Producers.Length)
				{
					button.style.display = DisplayStyle.None;
					continue;
				}

				IdleProducerView view = snapshot.Producers[kind];

				// 아직 이른 것은 숨긴다 — 처음부터 여덟 줄이면 뭘 할지가 안 보인다.
				// 줄 전체(도형 포함)를 숨긴다 — 버튼만 숨기면 도형이 혼자 남는다.
				button.parent.style.display = view.Hidden ? DisplayStyle.None : DisplayStyle.Flex;

				button.text = string.Format("{0} {1}   x{2}  ·  초당 {3}   —   {4}",
					ShapeMark(kind + 1),
					kind + 1,
					view.Owned,
					BigNumberText.Format(view.OutputTotal),
					BigNumberText.Format(view.NextCost));

				button.SetEnabled(view.CanAfford);
			}
		}

		private void RenderUpgradePage(IdleSnapshot snapshot)
		{
			DrawUpgrade(snapshot.Damage, damageTitle, damageValue, damageButton, "공격력", "한 방 {0}");
			DrawUpgrade(snapshot.AttackSpeed, speedTitle, speedValue, speedButton, "공격속도", "초당 {0}회");

			bool canRetreat = snapshot.Stage > snapshot.BestFarmingStage;
			retreatButton.text = canRetreat
				? string.Format("◀ {0}단계로 물러나 번다", snapshot.BestFarmingStage)
				: string.Format("▶ 가장 깊은 {0}단계로", snapshot.BestStage);
			retreatButton.SetEnabled(snapshot.Stage != (canRetreat ? snapshot.BestFarmingStage : snapshot.BestStage));

			holdButton.text = snapshot.HoldingStage ? "⏸ 여기 머무는 중" : "▽ 계속 내려가는 중";
		}

		private void RenderGearPage(IdleSnapshot snapshot)
		{
			RenderWorn(snapshot);
			RenderBag(snapshot);
			RenderMerge(snapshot);

			if (appraiseButtons.Count != snapshot.DroppedByTier.Length)
			{
				RebuildDropRows(snapshot.DroppedByTier.Length);
			}

			potentialLabel.text = snapshot.BestPotentialValue > 0d
				? string.Format("잠재 {0} {1:P1}", NameOf(snapshot.BestPotentialGrade), snapshot.BestPotentialValue)
				: "잠재 없음 — 2등급부터 감정할 수 있다";

			for (int tier = 1; tier <= appraiseButtons.Count; tier++)
			{
				long count = snapshot.DroppedByTier[tier - 1];
				// ★ 감정 값을 <b>버튼에 적는다</b> — 자원이 든다는 게 안 보이면 두 층이 물린 줄 모른다.
				double cost = IdleGear.AppraiseCost(tier, session.Tuning);

				appraiseButtons[tier - 1].text = tier < 2
					? string.Format("{0}{1}  {2}개 — 잠재 없음", ShapeMark(tier), tier, BigNumberText.Format(count))
					: string.Format("{0}{1}  {2}개 — 감정 {3} ({4})", ShapeMark(tier), tier,
						BigNumberText.Format(count), BigNumberText.Format(cost),
						NameOf(IdlePotentials.GradeFor(tier)));

				appraiseButtons[tier - 1].SetEnabled(tier >= 2 && count > 0L && snapshot.Resource >= cost);
			}
		}

		/// <summary>차고 있는 넷 — 부위마다 올리는 축이 다르다.</summary>
		private void RenderWorn(IdleSnapshot snapshot)
		{
			System.Text.StringBuilder text = new System.Text.StringBuilder();
			string[] names = { "머리(공격력)", "몸(기지)", "손(속도)", "발(떨구기)" };

			for (int slot = 0; slot < snapshot.Worn.Length && slot < names.Length; slot++)
			{
				IdleItem one = snapshot.Worn[slot];
				text.Append(names[slot]).Append(" ");

				if (one.IsEmpty)
				{
					text.AppendLine("— 비어 있음");
					continue;
				}

				text.Append(ShapeMark(one.Tier)).Append(one.Tier);
				if (one.PotentialValue > 0d)
				{
					text.AppendFormat("  {0} {1:P1}", NameOf(one.Grade), one.PotentialValue);
				}

				text.AppendLine();
			}

			wornLabel.text = text.ToString().TrimEnd();
		}

		/// <summary>가방 — 눌러서 찬다. 칸이 차면 더 안 들어온다(그게 정리하라는 신호다).</summary>
		private void RenderBag(IdleSnapshot snapshot)
		{
			if (bagButtons.Count != snapshot.Bag.Length)
			{
				bagRows.Clear();
				bagButtons.Clear();
				bagShapes.Clear();

				for (int index = 0; index < snapshot.Bag.Length; index++)
				{
					int captured = index;
					bagButtons.Add(AddShapeRow(bagRows, snapshot.Bag[index].Tier, () => Equip(captured),
						out IdleShapeElement shape));
					bagShapes.Add(shape);
				}
			}

			string[] slots = { "머리", "몸", "손", "발" };

			for (int index = 0; index < bagButtons.Count; index++)
			{
				IdleItem one = snapshot.Bag[index];
				if (index < bagShapes.Count)
				{
					bagShapes[index].Tier = one.Tier;
					bagShapes[index].Body = TierColor(one.Tier);
				}

				bagButtons[index].text = string.Format("{0}{1} {2}{3}",
					ShapeMark(one.Tier), one.Tier,
					slots[(int)one.Slot],
					one.PotentialValue > 0d ? string.Format("  {0:P1}", one.PotentialValue) : string.Empty);
			}
		}

		/// <summary>합칠 수 있는 조합만 보여준다.</summary>
		private void RenderMerge(IdleSnapshot snapshot)
		{
			int[] counts = new int[64];
			string[] slots = { "머리", "몸", "손", "발" };

			for (int index = 0; index < snapshot.Bag.Length; index++)
			{
				IdleItem one = snapshot.Bag[index];
				int key = one.Tier * 4 + (int)one.Slot;
				if (key >= 0 && key < counts.Length)
				{
					counts[key]++;
				}
			}

			List<string> labels = new List<string>();
			List<int> tiers = new List<int>();
			List<IdleItemSlot> which = new List<IdleItemSlot>();

			for (int key = 0; key < counts.Length; key++)
			{
				if (counts[key] < 3)
				{
					continue;
				}

				int tier = key / 4;
				IdleItemSlot slot = (IdleItemSlot)(key % 4);

				labels.Add(string.Format("{0}{1} {2} x{3} → {4}{5}",
					ShapeMark(tier), tier, slots[(int)slot], counts[key], ShapeMark(tier + 1), tier + 1));
				tiers.Add(tier);
				which.Add(slot);
			}

			if (mergeButtons.Count != labels.Count)
			{
				mergeRows.Clear();
				mergeButtons.Clear();

				for (int index = 0; index < labels.Count; index++)
				{
					int tier = tiers[index];
					IdleItemSlot slot = which[index];
					mergeButtons.Add(AddButton(mergeRows, "idle-appraise-button", () => Merge(tier, slot)));
				}
			}

			for (int index = 0; index < mergeButtons.Count && index < labels.Count; index++)
			{
				mergeButtons[index].text = labels[index];
			}
		}

		private void RenderFoldPage(IdleSnapshot snapshot)
		{
			foldSummary.text = string.Format(
				"모은 점수 {0} · 지금 배수 {1}\n자리 비워도 되는 시간 {2}\n\n접으면 셋이 오른다 — 공격 배수 · 등급 천장 · 비워도 되는 시간.\n이미 지나온 길은 다시 안 판다.",
				snapshot.PrestigePoints,
				BigNumberText.Format(snapshot.PrestigeMultiplier),
				DescribeSpan(snapshot.MaxOfflineSeconds));

			prestigeButton.text = snapshot.PrestigeAward > 0L
				? string.Format("다시 시작 — {0}점 얻는다", snapshot.PrestigeAward)
				: "다시 시작 — 더 내려가야 한다";
			prestigeButton.SetEnabled(snapshot.PrestigeAward > 0L);
		}

		// ── 의도 ────────────────────────────────────────────────────────────

		private void Send(IdleUpgradeKind kind)
		{
			session.Send(new IdleRaiseUpgradeIntent(kind));
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

		private void BuyProducer(int kind)
		{
			if (session.Send(new IdleBuyProducerIntent(kind)) && kind < producerShapes.Count)
			{
				// 산 것이 <b>반응한다</b> — 눌렀는데 아무 일도 안 일어나면 눌린 줄 모른다.
				producerShapes[kind].Hit();
			}

			Render(session.Capture());
		}

		private void ToggleHold()
		{
			session.Send(new IdleHoldStageIntent(session.State.HoldingStage == false));
			WriteDown();
			Render(session.Capture());
		}

		private void Appraise(int tier)
		{
			if (session.TryAppraise(tier, out PotentialRoll roll))
			{
				rollNote.text = string.Format("◆{0} 감정 → {1} {2:P1}{3}",
					roll.Tier, NameOf(roll.Grade), roll.Value, roll.Replaced ? "   ★ 갈아 끼웠다" : string.Empty);
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
				burst.Fire(tier + 1, TierColor(tier + 1));

				rollNote.text = string.Format("{0}{1} 셋을 합쳐 {2}{3} 하나 — 잠재는 사라졌다",
					ShapeMark(tier), tier, ShapeMark(tier + 1), tier + 1);
				WriteDown();
			}

			Render(session.Capture());
		}

		private void Prestige()
		{
			if (session.Send(new IdlePrestigeIntent()))
			{
				lastKills = session.State.Kills;
				WriteDown();
			}

			Render(session.Capture());
		}

		// ── 잔손 ────────────────────────────────────────────────────────────

		private void RebuildDropRows(int tierCount)
		{
			dropRows.Clear();
			appraiseButtons.Clear();

			for (int tier = 1; tier <= tierCount; tier++)
			{
				int captured = tier;
				appraiseButtons.Add(AddButton(dropRows, "idle-button", () => Appraise(captured)));
			}
		}

		private void AddTab(VisualElement parent, string text, Tab which)
		{
			Button button = new Button(() => ShowTab(which));
			button.text = text;
			button.AddToClassList("idle-tab");
			parent.Add(button);
			tabButtons.Add(button);
		}

		private void ShowTab(Tab which)
		{
			for (int index = 0; index < tabButtons.Count; index++)
			{
				tabButtons[index].EnableInClassList("idle-tab--on", index == (int)which);
			}

			basePage.EnableInClassList("idle-hidden", which != Tab.Base);
			upgradePage.EnableInClassList("idle-hidden", which != Tab.Upgrade);
			gearPage.EnableInClassList("idle-hidden", which != Tab.Gear);
			foldPage.EnableInClassList("idle-hidden", which != Tab.Fold);
		}

		private static VisualElement AddPage(VisualElement parent)
		{
			VisualElement page = new VisualElement();
			parent.Add(page);
			return page;
		}

		private static Label AddLabel(VisualElement parent, string className)
		{
			Label label = new Label(string.Empty);
			label.AddToClassList(className);
			parent.Add(label);
			return label;
		}

		/// <summary>
		/// 도형이 붙은 줄 — 왼쪽에 <b>변의 수 = 등급</b>인 도형, 오른쪽에 누를 것.
		///
		/// ★ 글자로만 두면 판에 도는 도형과 목록이 <b>다른 언어</b>가 된다.
		///   같은 규칙을 두 군데서 쓰면 한 번 배우고 계속 읽는다.
		/// </summary>
		private Button AddShapeRow(VisualElement parent, int tier, System.Action action,
			out IdleShapeElement shape)
		{
			VisualElement row = new VisualElement();
			row.AddToClassList("idle-shape-row");
			parent.Add(row);

			shape = new IdleShapeElement();
			shape.AddToClassList("idle-row-shape");
			shape.Tier = tier;
			shape.Body = TierColor(tier);
			row.Add(shape);
			decor.Add(shape);

			Button button = new Button(action);
			button.AddToClassList("idle-button");
			button.AddToClassList("idle-row-button");
			row.Add(button);

			return button;
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

		private static void AddDivider(VisualElement parent)
		{
			VisualElement line = new VisualElement();
			line.AddToClassList("idle-divider");
			parent.Add(line);
		}

		private static void DrawUpgrade(IdleUpgradeView view, Label title, Label value, Button button,
			string name, string valueFormat)
		{
			title.text = string.Format("{0}  Lv.{1}", name, view.Level);
			value.text = string.Format(valueFormat, BigNumberText.Format(view.CurrentValue));

			button.text = view.IsMaxed
				? "최대"
				: string.Format("올리기 — {0}", BigNumberText.Format(view.NextCost));
			button.SetEnabled(view.CanAfford);
		}

		/// <summary>변의 수로 등급을 적는다 — 도형과 같은 규칙을 글자에도.</summary>
		private static string ShapeMark(int tier)
		{
			switch (tier)
			{
				case 1: return "△";
				case 2: return "◇";
				case 3: return "⬠";
				case 4: return "⬡";
				default: return "◍";
			}
		}

		/// <summary>등급마다 색이 달라진다 — 변을 세기 전에 색으로 먼저 눈치챈다.</summary>
		private static Color TierColor(int tier)
		{
			float hue = Mathf.Repeat(0.58f + (tier - 1) * 0.085f, 1f);
			return Color.HSVToRGB(hue, 0.45f, 0.92f);
		}

		private static string NameOf(PotentialGrade grade)
		{
			switch (grade)
			{
				case PotentialGrade.Rare: return "레어";
				case PotentialGrade.Epic: return "에픽";
				case PotentialGrade.Unique: return "유니크";
				case PotentialGrade.Legendary: return "레전드리";
				default: return "없음";
			}
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
