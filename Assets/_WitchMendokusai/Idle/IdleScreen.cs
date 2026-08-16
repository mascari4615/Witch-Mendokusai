using UnityEngine;
using UnityEngine.UIElements;
using WitchMendokusai.DomainSDK.Contracts;
using WitchMendokusai.DomainSDK.Idle;
// 네임스페이스를 통째로 들이면 Vector2 가 UnityEngine 것과 겹친다 — 쓸 것만 별칭으로 들인다.
using BigNumberText = WitchMendokusai.Numerics.BigNumberText;

namespace WitchMendokusai
{
	/// <summary>
	/// 방치 게임을 <b>실제로 켜서 하는</b> 화면 (TASK-WM-406).
	///
	/// ★ 에디터 창(<c>IdlePlaygroundWindow</c>)과 같은 코어를 쓰고, 같은 계약을 구현한다.
	///   다른 것은 <b>그릇</b>뿐이다 — 저쪽은 에디터 창, 이쪽은 빌드에 실려 나가는 씬.
	///   그래서 이 파일에 게임 규칙이 한 줄도 없다: 사진을 받아 그리고, 의도를 보낸다.
	///
	/// ★ 본편 <c>UIRoot</c> 에 붙지 않는다. 붙이면 방치형 하나 켜자고 본편 부팅이 통째로 따라온다.
	///   따로 낼 수 있어야 하는 게임이라(2027-02 목표) 씬도 UI 도 자기 것만 쥔다.
	///
	/// ★ 색·여백·글자 크기는 여기 없다 — USS 에 있다(<c>WitchMendokusai/CLAUDE.md</c> § 코드로 짓는 UIToolkit).
	///   이 클래스는 USS 클래스 이름만 안다.
	/// </summary>
	[RequireComponent(typeof(UIDocument))]
	public sealed class IdleScreen : MonoBehaviour, IGameView<IdleSnapshot>
	{
		[Header("수치 — 비워 두면 코드 기본값")]
		[SerializeField] private IdleTuningSO tuningAsset;

		[Header("생김새")]
		[SerializeField] private StyleSheet styleSheet;

		[Header("얼마나 자주 적나 (초)")]
		[SerializeField] private float saveIntervalSeconds = 10f;

		private IdleSession session;
		private float sinceLastSave;

		private Label stageLabel;
		private Label resourceLabel;
		private Label incomeLabel;
		private Label killsLabel;
		private ProgressBar targetBar;
		private Label offlineLabel;
		private Button holdButton;
		private Label potentialLabel;
		private Label rollLabel;
		private VisualElement dropsPanel;
		private readonly System.Collections.Generic.List<Button> appraiseButtons = new System.Collections.Generic.List<Button>();
		private Button prestigeButton;
		private Button damageButton;
		private Button speedButton;
		private Label damageLevelLabel;
		private Label speedLevelLabel;

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

			// ★ 화면을 짓기 <b>전에</b> 자리 비운 몫을 쳐준다 — 그래야 첫 그림이 이미 받은 뒤의 판이다.
			double away = session.CatchUp(IdleSaveStore.NowUnixSeconds());

			BuildInterface(away);
			Render(session.Capture());
		}

		private void OnDisable()
		{
			WriteDown();
		}

		private void OnApplicationPause(bool paused)
		{
			// 손전화는 여기서 끝난다 — OnDisable 이 안 올 수 있다.
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
			session.Advance(Time.unscaledDeltaTime);
			Render(session.Capture());

			sinceLastSave += Time.unscaledDeltaTime;
			if (sinceLastSave >= saveIntervalSeconds)
			{
				WriteDown();
			}
		}

		/// <summary>지금을 적어 둔다 — 「언제 봤나」까지 같이 찍어야 자리 비운 몫이 이어진다.</summary>
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

		private void BuildInterface(double awaySeconds)
		{
			VisualElement root = GetComponent<UIDocument>().rootVisualElement;
			root.Clear();

			if (styleSheet != null)
			{
				root.styleSheets.Add(styleSheet);
			}

			VisualElement panel = new VisualElement();
			panel.AddToClassList("idle-panel");
			root.Add(panel);

			stageLabel = AddLabel(panel, "idle-stage");
			resourceLabel = AddLabel(panel, "idle-resource");
			incomeLabel = AddLabel(panel, "idle-income");
			killsLabel = AddLabel(panel, "idle-kills");

			targetBar = new ProgressBar();
			targetBar.lowValue = 0f;
			targetBar.highValue = 1f;
			targetBar.AddToClassList("idle-target");
			panel.Add(targetBar);

			// ★ 이 게임에서 사람이 하는 둘째 종류의 결정 — 여기 머물까, 더 내려갈까.
			//   실측(6시간): 머물면 540개(1등급) · 내려가면 26개(2등급). 많이냐 좋은 것이냐.
			holdButton = new Button(ToggleHold);
			holdButton.AddToClassList("idle-hold-button");
			panel.Add(holdButton);

			offlineLabel = AddLabel(panel, "idle-offline");
			offlineLabel.style.display = awaySeconds > 0d ? DisplayStyle.Flex : DisplayStyle.None;
			if (awaySeconds > 0d)
			{
				offlineLabel.text = string.Format("자리를 비운 {0} 동안도 잡아 뒀다", DescribeSpan(awaySeconds));
			}

			damageLevelLabel = AddLabel(panel, "idle-upgrade-title");
			damageButton = AddButton(panel, IdleUpgradeKind.Damage);

			speedLevelLabel = AddLabel(panel, "idle-upgrade-title");
			speedButton = AddButton(panel, IdleUpgradeKind.AttackSpeed);

			// ★ 감정 칸 — 이 게임에서 <b>사람이 주사위를 굴리는 유일한 자리</b>다.
			//   코어에 있는데 화면에 없으면 빌드로는 그 고리를 못 돈다.
			potentialLabel = AddLabel(panel, "idle-upgrade-title");
			dropsPanel = new VisualElement();
			panel.Add(dropsPanel);
			rollLabel = AddLabel(panel, "idle-roll");

			prestigeButton = new Button(Prestige);
			prestigeButton.AddToClassList("idle-prestige-button");
			panel.Add(prestigeButton);
		}

		/// <summary>
		/// 등급마다 「몇 개 · 감정」 한 줄. 천장이 오르면 줄이 늘어나므로 <b>필요할 때만 다시 짓는다</b> —
		/// 매 프레임 다시 지으면 누르는 도중에 버튼이 사라진다.
		/// </summary>
		private void RebuildDropRows(int tierCount)
		{
			dropsPanel.Clear();
			appraiseButtons.Clear();

			for (int tier = 1; tier <= tierCount; tier++)
			{
				int captured = tier;

				Button button = new Button(() => Appraise(captured));
				button.AddToClassList("idle-appraise-button");
				dropsPanel.Add(button);
				appraiseButtons.Add(button);
			}
		}

		private void Appraise(int tier)
		{
			if (session.TryAppraise(tier, out PotentialRoll roll))
			{
				rollLabel.text = string.Format("{0}등급 감정 → {1} {2:P1}{3}",
					roll.Tier, NameOf(roll.Grade), roll.Value, roll.Replaced ? "  ★ 갈아 끼웠다" : "");
				WriteDown();
			}

			Render(session.Capture());
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

		private static Label AddLabel(VisualElement parent, string className)
		{
			Label label = new Label(string.Empty);
			label.AddToClassList(className);
			parent.Add(label);
			return label;
		}

		private Button AddButton(VisualElement parent, IdleUpgradeKind kind)
		{
			Button button = new Button(() => Send(kind));
			button.AddToClassList("idle-upgrade-button");
			parent.Add(button);
			return button;
		}

		/// <summary>버튼이 하는 일은 이것뿐 — 의도를 보낸다. 받아들일지는 코어가 정한다.</summary>
		private void Send(IdleUpgradeKind kind)
		{
			session.Send(new IdleRaiseUpgradeIntent(kind));
			Render(session.Capture());
		}

		private void ToggleHold()
		{
			session.Send(new IdleHoldStageIntent(session.State.HoldingStage == false));
			WriteDown();
			Render(session.Capture());
		}

		private void Prestige()
		{
			if (session.Send(new IdlePrestigeIntent()))
			{
				// 판을 접었으면 바로 적어 둔다 — 여기서 죽으면 점수가 통째로 날아간다.
				WriteDown();
			}

			Render(session.Capture());
		}

		public void Render(IdleSnapshot snapshot)
		{
			if (resourceLabel == null)
			{
				return;
			}

			// ★ 문구를 고쳤다 (실측 2026-08-16). 「더 내려가도 안 열린다」는 <b>등급</b> 얘기인데
			//   사람은 그걸 「접어라」로 읽는다. 그런데 재 보니 천장에서 바로 접는 습관은
			//   판5에 102단계, 버티는 습관은 <b>363단계</b>였다 — 접기 남발이 훨씬 손해다.
			//   화면이 나쁜 수를 권하고 있었던 셈이라, <b>점수는 계속 쌓인다</b>를 같이 적는다.
			bool atCeiling = snapshot.MaxTierNow >= snapshot.TierCeiling;
			stageLabel.text = atCeiling
				? string.Format("{0}단계  ({1}/{2})   등급 {3} — 천장 (등급은 그만, 점수는 계속 쌓인다)",
					snapshot.Stage, snapshot.KillsInStage, snapshot.KillsPerStage, snapshot.MaxTierNow)
				: string.Format("{0}단계  ({1}/{2})   등급 {3} / 천장 {4}",
					snapshot.Stage, snapshot.KillsInStage, snapshot.KillsPerStage,
					snapshot.MaxTierNow, snapshot.TierCeiling);
			resourceLabel.text = string.Format("자원 {0}", BigNumberText.Format(snapshot.Resource));
			incomeLabel.text = string.Format("초당 {0}", BigNumberText.Format(snapshot.IncomePerSecond));
			killsLabel.text = string.Format("처치 {0}", BigNumberText.Format(snapshot.Kills));

			targetBar.value = (float)snapshot.TargetHealthRatio;
			targetBar.title = string.Format("대상 체력 {0:P0}", snapshot.TargetHealthRatio);

			if (appraiseButtons.Count != snapshot.DroppedByTier.Length)
			{
				RebuildDropRows(snapshot.DroppedByTier.Length);
			}

			holdButton.text = snapshot.HoldingStage
				? string.Format("여기서 사냥 중 — {0}단계 (많이 떨군다)", snapshot.Stage)
				: string.Format("계속 내려가는 중 (좋은 게 떨어진다 · 상한 {0}등급)", snapshot.TierCeiling);

			potentialLabel.text = snapshot.BestPotentialValue > 0d
				? string.Format("잠재 {0} {1:P1}", NameOf(snapshot.BestPotentialGrade), snapshot.BestPotentialValue)
				: "잠재 없음 — 2등급부터 감정할 수 있다";

			for (int tier = 1; tier <= appraiseButtons.Count; tier++)
			{
				long count = snapshot.DroppedByTier[tier - 1];
				bool appraisable = tier >= 2 && count > 0L;

				appraiseButtons[tier - 1].text = tier < 2
					? string.Format("{0}등급 {1}개 — 잠재 없음", tier, count)
					: string.Format("{0}등급 {1}개 — 감정 ({2})", tier, count, NameOf(IdlePotentials.GradeFor(tier)));
				appraiseButtons[tier - 1].SetEnabled(appraisable);
			}

			// ★ 접으면 무엇이 오르는지 <b>세 가지를 다</b> 적는다 — 배수만 보이면 접을 이유가 얇다.
			string standing = string.Format("{0}점 · {1}배 · 자리 비움 {2}",
				snapshot.PrestigePoints,
				BigNumberText.Format(snapshot.PrestigeMultiplier),
				DescribeSpan(snapshot.MaxOfflineSeconds));

			prestigeButton.text = snapshot.PrestigeAward > 0L
				? string.Format("다시 시작 — {0}점 얻는다  ({1})", snapshot.PrestigeAward, standing)
				: string.Format("다시 시작 — 더 내려가야 한다  ({0})", standing);
			prestigeButton.SetEnabled(snapshot.PrestigeAward > 0L);

			DrawUpgrade(snapshot.Damage, damageLevelLabel, damageButton, "공격력", "한 방 {0}");
			DrawUpgrade(snapshot.AttackSpeed, speedLevelLabel, speedButton, "공격속도", "초당 {0}회");
		}

		private static void DrawUpgrade(IdleUpgradeView view, Label levelLabel, Button button, string title, string valueFormat)
		{
			levelLabel.text = string.Format("{0} Lv.{1}  ({2})", title, view.Level, string.Format(valueFormat, BigNumberText.Format(view.CurrentValue)));
			button.text = view.IsMaxed
				? string.Format("{0} 올리기 — 최대", title)
				: string.Format("{0} 올리기 — {1}", title, BigNumberText.Format(view.NextCost));
			button.SetEnabled(view.CanAfford);
		}

		/// <summary>「8시간」처럼 사람이 읽는 말로. 초를 그대로 보여 주면 아무도 안 읽는다.</summary>
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
