using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using WitchMendokusai.DomainSDK.Idle;
using WitchMendokusai.DomainSDK.Upgrade;

namespace WitchMendokusai
{
	/// <summary>
	/// 방치 코어를 <b>지금 당장 만져 보는</b> 창 (TASK-WM-406).
	///
	/// ★ 왜 에디터 창인가 — 씬도 Play 도 없이 코어만으로 게임이 도는지 확인하려는 것이다.
	///   코어(DomainSDK/Idle)는 Unity 를 모르고, 이 창은 그 코어를 <b>읽어서 그리기만</b> 한다.
	///   즉 이 창은 표현 넷(3D · 2D · UI · Text) 중 <b>UI 하나의 첫 형태</b>이고,
	///   같은 코어에 다른 창을 붙이면 다른 표현이 된다.
	///
	/// ★ UI Toolkit 으로 짠 이유 — 런타임 화면으로 옮길 때 거의 그대로 간다(같은 VisualElement).
	///
	/// 곡선을 <b>손으로</b> 만져 보는 자리이기도 하다 — 판정 기준이 「업그레이드 누르는 맛이 있나」라
	/// 표만 봐서는 안 되고 실제로 눌러 봐야 한다.
	/// </summary>
	public sealed class IdlePlaygroundWindow : EditorWindow
	{
		private const double TICK_SECONDS = 0.1d;

		private IdleTuning tuning;
		private IdleState state;

		private double lastTickTime;
		private double speedMultiplier = 1d;

		private Label resourceLabel;
		private Label rateLabel;
		private Label felledLabel;
		private Button powerButton;
		private Button rateButton;
		private Label powerLevelLabel;
		private Label rateLevelLabel;

		[MenuItem("WM/Idle/Playground")]
		public static void Open()
		{
			IdlePlaygroundWindow window = GetWindow<IdlePlaygroundWindow>();
			window.titleContent = new GUIContent("Idle Playground");
			window.minSize = new Vector2(340f, 260f);
			window.Show();
		}

		private void OnEnable()
		{
			tuning = new IdleTuning();
			state = new IdleState();
			lastTickTime = EditorApplication.timeSinceStartup;
			EditorApplication.update += Tick;
		}

		private void OnDisable()
		{
			EditorApplication.update -= Tick;
		}

		public void CreateGUI()
		{
			VisualElement root = rootVisualElement;
			root.style.paddingLeft = 12f;
			root.style.paddingRight = 12f;
			root.style.paddingTop = 12f;
			root.style.paddingBottom = 12f;

			resourceLabel = MakeLine(root, 22, FontStyle.Bold);
			rateLabel = MakeLine(root, 12, FontStyle.Normal);
			felledLabel = MakeLine(root, 12, FontStyle.Normal);

			root.Add(MakeSpacer(10f));

			powerLevelLabel = MakeLine(root, 12, FontStyle.Normal);
			powerButton = new Button(() => Raise(IdleUpgradeKind.Power));
			root.Add(powerButton);

			root.Add(MakeSpacer(6f));

			rateLevelLabel = MakeLine(root, 12, FontStyle.Normal);
			rateButton = new Button(() => Raise(IdleUpgradeKind.Rate));
			root.Add(rateButton);

			root.Add(MakeSpacer(12f));

			SliderInt speed = new SliderInt("빨리감기", 1, 200);
			speed.value = 1;
			speed.RegisterValueChangedCallback(changed => speedMultiplier = changed.newValue);
			root.Add(speed);

			Button reset = new Button(() => state = new IdleState());
			reset.text = "처음부터";
			root.Add(reset);

			Redraw();
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

		private void Raise(IdleUpgradeKind kind)
		{
			IdleModel.TryRaise(state, tuning, kind, out UpgradeRaiseFailure _);
			Redraw();
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
			IdleModel.Step(state, tuning, elapsed * speedMultiplier);
			Redraw();
		}

		private void Redraw()
		{
			if (resourceLabel == null)
			{
				return;
			}

			resourceLabel.text = string.Format("자원 {0:N0}", state.Resource);
			rateLabel.text = string.Format("초당 {0:N2}", IdleModel.ResourcePerSecond(state, tuning));
			felledLabel.text = string.Format("처치 {0:N0}", state.TargetsFelled);

			powerLevelLabel.text = string.Format("세기 Lv.{0}  (한 방 {1:N2})", state.Power.Level, IdleModel.PowerOf(state, tuning));
			rateLevelLabel.text = string.Format("빠르기 Lv.{0}  (초당 {1:N2}회)", state.Rate.Level, IdleModel.RateOf(state, tuning));

			powerButton.text = CostText("세기 올리기", IdleUpgradeKind.Power);
			rateButton.text = CostText("빠르기 올리기", IdleUpgradeKind.Rate);

			powerButton.SetEnabled(CanAfford(IdleUpgradeKind.Power));
			rateButton.SetEnabled(CanAfford(IdleUpgradeKind.Rate));

			Repaint();
		}

		private string CostText(string prefix, IdleUpgradeKind kind)
		{
			if (IdleModel.TryGetNextCost(state, tuning, kind, out double cost) == false)
			{
				return string.Format("{0} — 최대", prefix);
			}

			return string.Format("{0} — {1:N0}", prefix, cost);
		}

		private bool CanAfford(IdleUpgradeKind kind)
		{
			return IdleModel.TryGetNextCost(state, tuning, kind, out double cost) && state.Resource >= cost;
		}
	}
}
