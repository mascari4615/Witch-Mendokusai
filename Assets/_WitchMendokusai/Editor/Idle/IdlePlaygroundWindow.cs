using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using WitchMendokusai.DomainSDK.Idle;
using WitchMendokusai.DomainSDK.Contracts;

namespace WitchMendokusai
{
	/// <summary>
	/// 방치 코어를 <b>지금 당장 만져 보는</b> 창 (TASK-WM-406).
	///
	/// ★ 이 창은 표현 넷(3D · 2D · UI · 글자) 중 <b>UI 하나</b>다 — <see cref="IGameView{TSnapshot}"/> 를 구현한다.
	///   코어를 직접 만지지 않는다: 사진(<see cref="IdleSnapshot"/>)을 받아 그리고,
	///   바꾸고 싶으면 의도(<see cref="IdleRaiseUpgradeIntent"/>)를 보낸다.
	///   같은 세션에 다른 창을 붙이면 그게 다른 표현이 된다 — 코어는 손도 안 댄다.
	///
	/// ★ 씬도 Play 도 없다 — 코어가 Unity 를 모르니 에디터 창만으로 게임이 돈다.
	///   그게 「코어만으로도 게임은 돌아간다」의 증거이기도 하다.
	///
	/// ★ 숫자는 <see cref="IdleTuningSO"/> 에서 온다 — 비워 두면 코드 기본값으로 돈다.
	/// </summary>
	public sealed class IdlePlaygroundWindow : EditorWindow, IGameView<IdleSnapshot>
	{
		private const double TICK_SECONDS = 0.1d;

		[SerializeField] private IdleTuningSO tuningAsset;

		private IdleSession session;
		private double lastTickTime;
		private double speedMultiplier = 1d;

		private Label resourceLabel;
		private Label incomeLabel;
		private Label killsLabel;
		private ProgressBar targetBar;
		private Button damageButton;
		private Button speedButton;
		private Label damageLevelLabel;
		private Label speedLevelLabel;

		/// <summary>이 창은 화면 요소만으로 그린다.</summary>
		public PresentationKind Kind => PresentationKind.UIOnly;

		[MenuItem("WM/Idle/Playground")]
		public static void Open()
		{
			IdlePlaygroundWindow window = GetWindow<IdlePlaygroundWindow>();
			window.titleContent = new GUIContent("Idle Playground");
			window.minSize = new Vector2(360f, 320f);
			window.Show();
		}

		private void OnEnable()
		{
			RebuildSession();
			lastTickTime = EditorApplication.timeSinceStartup;
			EditorApplication.update += Tick;
		}

		private void OnDisable()
		{
			EditorApplication.update -= Tick;
		}

		private void RebuildSession()
		{
			IdleTuning tuning = tuningAsset != null ? tuningAsset.ToTuning() : new IdleTuning();
			session = new IdleSession(tuning);
		}

		public void CreateGUI()
		{
			VisualElement root = rootVisualElement;
			root.style.paddingLeft = 12f;
			root.style.paddingRight = 12f;
			root.style.paddingTop = 12f;
			root.style.paddingBottom = 12f;

			ObjectField tuningField = new ObjectField("수치 에셋");
			tuningField.objectType = typeof(IdleTuningSO);
			tuningField.value = tuningAsset;
			tuningField.RegisterValueChangedCallback(changed =>
			{
				tuningAsset = changed.newValue as IdleTuningSO;
				RebuildSession();
			});
			root.Add(tuningField);

			root.Add(MakeSpacer(8f));

			resourceLabel = MakeLine(root, 22, FontStyle.Bold);
			incomeLabel = MakeLine(root, 12, FontStyle.Normal);
			killsLabel = MakeLine(root, 12, FontStyle.Normal);

			targetBar = new ProgressBar();
			targetBar.lowValue = 0f;
			targetBar.highValue = 1f;
			root.Add(targetBar);

			root.Add(MakeSpacer(10f));

			damageLevelLabel = MakeLine(root, 12, FontStyle.Normal);
			damageButton = new Button(() => Send(IdleUpgradeKind.Damage));
			root.Add(damageButton);

			root.Add(MakeSpacer(6f));

			speedLevelLabel = MakeLine(root, 12, FontStyle.Normal);
			speedButton = new Button(() => Send(IdleUpgradeKind.AttackSpeed));
			root.Add(speedButton);

			root.Add(MakeSpacer(12f));

			SliderInt speed = new SliderInt("빨리감기", 1, 200);
			speed.value = 1;
			speed.RegisterValueChangedCallback(changed => speedMultiplier = changed.newValue);
			root.Add(speed);

			Button reset = new Button(RebuildSession);
			reset.text = "처음부터";
			root.Add(reset);

			Render(session.Capture());
		}

		private static Label MakeLine(VisualElement parent, int fontSize, FontStyle fontStyle)
		{
			Label label = new Label(string.Empty);
			label.style.fontSize = fontSize;
			label.style.unityFontStyleAndWeight = fontStyle;
			parent.Add(label);
			return label;
		}

		private static VisualElement MakeSpacer(float height)
		{
			VisualElement spacer = new VisualElement();
			spacer.style.height = height;
			return spacer;
		}

		/// <summary>버튼이 하는 일은 이것뿐 — 의도를 보낸다. 받아들일지는 코어가 정한다.</summary>
		private void Send(IdleUpgradeKind kind)
		{
			session.Send(new IdleRaiseUpgradeIntent(kind));
			Render(session.Capture());
		}

		private void Tick()
		{
			double now = EditorApplication.timeSinceStartup;
			double elapsed = now - lastTickTime;
			if (elapsed < TICK_SECONDS)
			{
				return;
			}

			lastTickTime = now;
			session.Advance(elapsed * speedMultiplier);
			Render(session.Capture());
		}

		/// <summary>사진대로 그린다 — 여기서 코어를 만지지 않는다.</summary>
		public void Render(IdleSnapshot snapshot)
		{
			if (resourceLabel == null)
			{
				return;
			}

			resourceLabel.text = string.Format("자원 {0:N0}", snapshot.Resource);
			incomeLabel.text = string.Format("초당 {0:N2}", snapshot.IncomePerSecond);
			killsLabel.text = string.Format("처치 {0:N0}", snapshot.Kills);

			targetBar.value = (float)snapshot.TargetHealthRatio;
			targetBar.title = string.Format("대상 체력 {0:P0}", snapshot.TargetHealthRatio);

			DrawUpgrade(snapshot.Damage, damageLevelLabel, damageButton, "공격력", "한 방 {0:N2}");
			DrawUpgrade(snapshot.AttackSpeed, speedLevelLabel, speedButton, "공격속도", "초당 {0:N2}회");

			Repaint();
		}

		private static void DrawUpgrade(IdleUpgradeView view, Label levelLabel, Button button, string title, string valueFormat)
		{
			levelLabel.text = string.Format("{0} Lv.{1}  ({2})", title, view.Level, string.Format(valueFormat, view.CurrentValue));
			button.text = view.IsMaxed
				? string.Format("{0} 올리기 — 최대", title)
				: string.Format("{0} 올리기 — {1:N0}", title, view.NextCost);
			button.SetEnabled(view.CanAfford);
		}
	}
}
