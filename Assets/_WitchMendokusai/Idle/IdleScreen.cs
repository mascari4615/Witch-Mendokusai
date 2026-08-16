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
			Upgrade = 0,
			Gear = 1,
			Fold = 2,
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
		private ProgressBar healthBar;
		private readonly List<VisualElement> killDots = new List<VisualElement>();
		private Label arenaCaption;

		private readonly List<Button> tabButtons = new List<Button>();
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

			ShowTab(Tab.Upgrade);
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
			AddTab(tabs, "강화", Tab.Upgrade);
			AddTab(tabs, "장비", Tab.Gear);
			AddTab(tabs, "접기", Tab.Fold);

			upgradePage = AddPage(panel);
			gearPage = AddPage(panel);
			foldPage = AddPage(panel);

			BuildUpgradePage();
			BuildGearPage();
			BuildFoldPage();
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

		private void BuildGearPage()
		{
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
				appraiseButtons[tier - 1].text = tier < 2
					? string.Format("◆{0}  {1}개 — 잠재 없음", tier, BigNumberText.Format(count))
					: string.Format("◆{0}  {1}개 — 감정 ({2})", tier, BigNumberText.Format(count),
						NameOf(IdlePotentials.GradeFor(tier)));
				appraiseButtons[tier - 1].SetEnabled(tier >= 2 && count > 0L);
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
