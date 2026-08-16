using UnityEngine;
using UnityEngine.UIElements;
using WitchMendokusai.DomainSDK.Contracts;
using WitchMendokusai.DomainSDK.Idle;

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

		public void Render(IdleSnapshot snapshot)
		{
			if (resourceLabel == null)
			{
				return;
			}

			stageLabel.text = string.Format("{0}단계  ({1}/{2})", snapshot.Stage, snapshot.KillsInStage, snapshot.KillsPerStage);
			resourceLabel.text = string.Format("자원 {0:N0}", snapshot.Resource);
			incomeLabel.text = string.Format("초당 {0:N2}", snapshot.IncomePerSecond);
			killsLabel.text = string.Format("처치 {0:N0}", snapshot.Kills);

			targetBar.value = (float)snapshot.TargetHealthRatio;
			targetBar.title = string.Format("대상 체력 {0:P0}", snapshot.TargetHealthRatio);

			DrawUpgrade(snapshot.Damage, damageLevelLabel, damageButton, "공격력", "한 방 {0:N2}");
			DrawUpgrade(snapshot.AttackSpeed, speedLevelLabel, speedButton, "공격속도", "초당 {0:N2}회");
		}

		private static void DrawUpgrade(IdleUpgradeView view, Label levelLabel, Button button, string title, string valueFormat)
		{
			levelLabel.text = string.Format("{0} Lv.{1}  ({2})", title, view.Level, string.Format(valueFormat, view.CurrentValue));
			button.text = view.IsMaxed
				? string.Format("{0} 올리기 — 최대", title)
				: string.Format("{0} 올리기 — {1:N0}", title, view.NextCost);
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
