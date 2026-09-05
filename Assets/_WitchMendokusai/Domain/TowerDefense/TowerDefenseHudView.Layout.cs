using UnityEngine;
using Vector2 = WitchMendokusai.Numerics.Vector2;
using Vector3 = WitchMendokusai.Numerics.Vector3;
using Vector2Int = WitchMendokusai.Numerics.Vector2Int;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	// TowerDefenseHudView 의 칸 짜기 부분. 같은 클래스의 partial 조각이다. 상태(필드)는 TowerDefenseHudView.cs 를 본다.
	public partial class TowerDefenseHudView
	{
		/// <summary>
		/// 안전영역만큼 루트를 안쪽으로 민다 — 노치·둥근 모서리에 글자가 잘리지 않게.
		/// 데스크톱에서는 안전영역 = 화면 전체라 아무 일도 안 일어난다(그래서 한 코드로 둘 다 된다).
		/// </summary>
		private void ApplySafeArea()
		{
			Rect safe = Screen.safeArea;
			float width = Mathf.Max(1f, Screen.width);
			float height = Mathf.Max(1f, Screen.height);

			container.style.paddingLeft = Length.Percent(safe.xMin / width * 100f);
			container.style.paddingRight = Length.Percent((width - safe.xMax) / width * 100f);
			// 화면 좌표는 아래가 0 이고 UI 는 위가 0 이라 위아래가 뒤집힌다 — 여기서 한 번만 뒤집는다.
			container.style.paddingTop = Length.Percent((height - safe.yMax) / height * 100f);
			container.style.paddingBottom = Length.Percent(safe.yMin / height * 100f);
		}

		/// <summary>
		/// HUD 덩어리에 이름을 붙인다 — 「겹치나」를 눈이 아니라 *좌표로* 물을 수 있게.
		/// ★ 이름이 없으면 화면 검사가 「사람이 봐야 안다」에 영원히 묶인다. 붙는 순간 자동 확인이 가능해진다.
		/// </summary>
		private static VisualElement Named(VisualElement element, string name)
		{
			if (element != null)
				element.name = name;
			return element;
		}

		// 좌상단 컴팩트 스탯 — 폭을 내용에 맞춰 좁게(전폭 바 금지).
		/// <summary>
		/// 자원 띠 — 상단 가운데 독립. 자원 종류가 늘어나면 이 띠에 칸만 가로로 붙인다
		/// (다른 정보와 섞어두면 종류가 늘 때마다 화면 전체를 다시 짜야 한다).
		/// </summary>
		private static VisualElement BuildResourceBar(out Label resource, out Label income, out Label essence, out Label livesCell)
		{
			VisualElement bar = new VisualElement { name = "ResourceBar" };
			bar.style.position = Position.Absolute;
			bar.style.top = EDGE_GAP; // 공유 여백 — 조각마다 다른 숫자를 박지 않는다.
			bar.style.left = 0;
			bar.style.right = 0;
			bar.style.flexDirection = FlexDirection.Row;
			bar.style.justifyContent = Justify.Center;
			bar.pickingMode = PickingMode.Ignore;

			VisualElement inner = new VisualElement();
			inner.style.flexDirection = FlexDirection.Row;
			inner.style.alignItems = Align.Center;
			inner.style.paddingLeft = 18;
			inner.style.paddingRight = 18;
			inner.style.paddingTop = 8;
			inner.style.paddingBottom = 8;
			inner.style.backgroundColor = new Color(0.04f, 0.05f, 0.08f, 0.82f);
			SetRadius(inner, 8);
			inner.pickingMode = PickingMode.Ignore;

			// ★ 판이 도는 동안 필요한 것만 — 자원 · 정수 · 목숨. 「다음 수입」 같은 예측치는 결과 화면으로 뺀다
			//   (사용자 지시). 계산은 계속 돌고 라벨도 살아 있지만 화면에는 안 붙는다.
			inner.Add(MakeResourceCell(TowerDefenseIcon.Kind.Diamond, new Color(1f, 0.86f, 0.35f, 1f), out resource, 26));
			income = MakeHiddenStat();
			// 정수 — 자원 띠에 칸이 하나 붙는다. 「종류가 늘면 가로로 칸만 추가」로 설계해 둔 것이 여기서 회수된다.
			inner.Add(MakeDivider());
			inner.Add(MakeResourceCell(TowerDefenseIcon.Kind.Core, new Color(0.7f, 0.6f, 1f, 1f), out essence, 24));

			inner.Add(MakeDivider());
			inner.Add(MakeResourceCell(TowerDefenseIcon.Kind.Burst, new Color(1f, 0.45f, 0.45f, 1f), out livesCell, 22));

			bar.Add(inner);
			return bar;
		}

		private static VisualElement MakeResourceCell(TowerDefenseIcon.Kind iconKind, Color color, out Label value, int fontSize)
		{
			VisualElement cell = new VisualElement();
			cell.style.flexDirection = FlexDirection.Row;
			cell.style.alignItems = Align.Center;
			cell.pickingMode = PickingMode.Ignore;

			cell.Add(TowerDefenseIcon.Make(iconKind, color, 18));

			value = new Label(string.Empty);
			value.style.fontSize = fontSize;
			value.style.color = color;
			value.style.marginLeft = 8;
			value.pickingMode = PickingMode.Ignore;

			cell.Add(value);
			return cell;
		}

		private static VisualElement MakeDivider()
		{
			VisualElement divider = new VisualElement();
			divider.style.width = 1;
			divider.style.height = 20;
			divider.style.marginLeft = 16;
			divider.style.marginRight = 16;
			divider.style.backgroundColor = new Color(1f, 1f, 1f, 0.16f);
			divider.pickingMode = PickingMode.Ignore;
			return divider;
		}

		/// <summary> 진행 정보 — 우상단. 「지금 무슨 일이 일어나는가」만 모은다. </summary>
		/// <summary> 화면에 안 붙는 값 그릇 — 계산은 계속 흐르되 판이 도는 동안 눈에 띄지 않는다. </summary>
		private static Label MakeHiddenStat() => new Label(string.Empty) { style = { display = DisplayStyle.None } };

		private VisualElement BuildProgressPanel(
			out Label lives, out Label wave, out Label phase, out Label nextWave, out Label enemies, out Label best,
			out Button modeButton, out Button callButton)
		{
			VisualElement panel = new VisualElement { name = "ProgressPanel" };
			panel.style.position = Position.Absolute;
			panel.style.top = 18;
			panel.style.right = 24;
			panel.style.paddingLeft = 14;
			panel.style.paddingRight = 14;
			panel.style.paddingTop = 10;
			panel.style.paddingBottom = 10;
			panel.style.backgroundColor = new Color(0.04f, 0.05f, 0.08f, 0.78f);
			SetRadius(panel, 8);
			panel.style.alignItems = Align.FlexEnd;
			panel.pickingMode = PickingMode.Ignore;

			// 목숨이 맨 위 — 유출제에서는 이게 곧 남은 판의 길이다.
			// ★ 판이 도는 동안 *숫자를 늘어놓지 않는다* (사용자 지시: "다음 웨이브 정보, 지난 시간
			//   이런거 내부적으로 계산하고 런타임에 표시 안했으면 좋겠음. … 게임 끝났을때 통계처럼
			//   공개하는게 맞음. Like RiskofRain 2"). 계산은 그대로 돌고, *보여주기만* 결과 화면으로 옮긴다.
			//   그래서 라벨은 만들되(코드가 계속 값을 넣는다) 화면에는 안 붙인다 — 결과 화면이 이 값을 읽는다.
			lives = MakeHiddenStat();
			wave = MakeHiddenStat();
			phase = MakeHiddenStat();
			nextWave = MakeHiddenStat();
			enemies = MakeHiddenStat();
			best = MakeHiddenStat();

			VisualElement buttons = new VisualElement();
			buttons.style.flexDirection = FlexDirection.Row;
			buttons.style.marginTop = 8;
			buttons.pickingMode = PickingMode.Ignore;

			modeButton = MakeActionButton(string.Empty, fontSize: 12, () => WaveModeToggleRequested());
			modeButton.style.marginRight = 6;
			callButton = MakeActionButton("다음 웨이브 ▶", fontSize: 12, () => NextWaveRequested());
			buttons.Add(modeButton);
			buttons.Add(callButton);
			panel.Add(buttons);

			// 시간 조작 — 화면이 말하는 걸 볼 시간이 없으면 정보가 있어도 못 쓴다.
			VisualElement timeButtons = new VisualElement();
			timeButtons.style.flexDirection = FlexDirection.Row;
			timeButtons.style.marginTop = 6;
			timeButtons.pickingMode = PickingMode.Ignore;

			pauseButton = MakeActionButton("⏸ 멈춤", fontSize: 12, () => PauseToggleRequested());
			pauseButton.style.marginRight = 6;
			speedButton = MakeActionButton("배속 ×1", fontSize: 12, () => SpeedCycleRequested());
			timeButtons.Add(pauseButton);
			timeButtons.Add(speedButton);
			panel.Add(timeButtons);

			return panel;
		}

		private static void SetRadius(VisualElement element, int radius)
		{
			element.style.borderTopLeftRadius = radius;
			element.style.borderTopRightRadius = radius;
			element.style.borderBottomLeftRadius = radius;
			element.style.borderBottomRightRadius = radius;
		}

		// 「라벨   값」 한 줄 — 라벨은 흐리게, 값은 크고 밝게(스캔이 값으로 먼저 가게).
		private static VisualElement MakeStatRow(string caption, out Label valueLabel, Color valueColor)
		{
			VisualElement row = new VisualElement();
			row.style.flexDirection = FlexDirection.Row;
			row.style.alignItems = Align.Center;
			row.style.marginBottom = 2;
			row.pickingMode = PickingMode.Ignore;

			Label captionLabel = new Label(caption);
			captionLabel.style.fontSize = 12;
			captionLabel.style.color = new Color(0.62f, 0.66f, 0.74f, 1f);
			captionLabel.style.width = 72;
			captionLabel.style.flexShrink = 0;
			captionLabel.pickingMode = PickingMode.Ignore;

			valueLabel = new Label(string.Empty);
			valueLabel.style.fontSize = 20;
			valueLabel.style.color = valueColor;
			valueLabel.pickingMode = PickingMode.Ignore;

			row.Add(captionLabel);
			row.Add(valueLabel);
			return row;
		}



		// ── UI 배율 ───────────────────────────────────────────────────────────────
		// ★ 왜 필요한가 (사용자 지시: "UI 배율 설정할 수 있으면 좋겠음"): 화면 해상도에 따라 글자가
		//   지나치게 작거나 커진다. 배율은 취향이 아니라 *읽을 수 있느냐*의 문제다.
		// ★ 왜 개척 것만인가 (사용자 지시: "당장은 다른 UI 모두 건들 수 없으니 그건 TODO"): 본편 UI 전체는
		//   패널 설정(PanelSettings) 층에서 한 번에 걸어야 하고 그건 화면 전부를 다시 봐야 하는 일이다.
		//   여기서는 개척 HUD 조각들만 각자 *자기가 붙은 모서리*를 기준으로 키운다 —
		//   기준점을 안 맞추면 키우는 순간 화면 밖으로 밀려난다.
		// TODO(WM 전역): 본편 UI 배율은 UIRoot 의 PanelSettings.scale 로 올려야 한다(별도 작업).
		private static readonly float[] UiScaleSteps = { 0.85f, 1f, 1.2f, 1.45f };
		private int uiScaleStep = 1;

		/// <summary> 지금 배율(화면 버튼이 보여준다). </summary>
		public float UiScale => UiScaleSteps[Mathf.Clamp(uiScaleStep, 0, UiScaleSteps.Length - 1)];

		public void CycleUiScale()
		{
			uiScaleStep = (uiScaleStep + 1) % UiScaleSteps.Length;
			ApplyUiScale();
		}

		private void ApplyUiScale()
		{
			float scale = UiScale;

			foreach (VisualElement child in container.Children())
			{
				// 커서를 따라다니는 것(툴팁·월드 이름표)은 좌표를 직접 계산하므로 배율에서 뺀다 —
				// 키우면 가리키는 자리가 어긋난다.
				if (child == unitTooltip || child == worldLabelLayer)
					continue;

				child.style.transformOrigin = new StyleTransformOrigin(OriginFor(child.name));
				child.style.scale = new StyleScale(new Scale(new Vector2(scale, scale).ToUnity()));
			}

			if (uiScaleButton != null)
				uiScaleButton.text = "UI ×" + scale.ToString("0.##");
		}

		// 붙은 모서리를 기준점으로 — 안 맞추면 키우는 순간 화면 밖으로 밀려난다.
		private static TransformOrigin OriginFor(string panelName)
		{
			return panelName switch
			{
				"ProgressPanel" => new TransformOrigin(Length.Percent(100f), Length.Percent(0f)),
				"LegendWrapper" => new TransformOrigin(Length.Percent(0f), Length.Percent(100f)),
				"SelectionPanel" => new TransformOrigin(Length.Percent(0f), Length.Percent(100f)),
				"TowerDefenseSelectionBar" => new TransformOrigin(Length.Percent(50f), Length.Percent(100f)),
				_ => new TransformOrigin(Length.Percent(50f), Length.Percent(0f)),
			};
		}
	}
}
