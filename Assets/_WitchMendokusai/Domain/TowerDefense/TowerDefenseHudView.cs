using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 특수시공 개척(TD) HUD — 자원/웨이브/국면 + 조작 힌트 + 결과 배너.
	///
	/// ★ 두 가지가 이 클래스의 존재 이유이고 둘 다 사용자 실증에서 나왔다:
	/// 1. **본편 HUD 를 끈다.** 개척은 *다른 게임*인데 핫바·건물바·시계가 그대로 떠 있으면 화면이
	///    두 게임의 UI 가 겹친 상태가 된다("기존 HUD는 꺼야지 다른 게임이니까").
	///    본편 HUD 는 전부 `UIRoot.HudLayer` 에 붙으므로(HotbarView/BuildingBarView/WorldClockView)
	///    그 레이어를 통째 숨기고, 개척 HUD 는 한 단 위 `OverlayLayer` 에 붙어 살아남는다.
	/// 2. **한 줄을 꽉 채우지 않는다.** 전폭 바 + 긴 한 줄 힌트는 읽히지 않는다("텍스트 한줄도 다
	///    채우지 말고"). 좌상단 컴팩트 스탯 + 하단 중앙 짧은 조작 힌트로 분리.
	///
	/// MonoBehaviour 가 아닌 평범한 클래스 — VContainer [Inject] 메서드는 타입당 1개 제약이 있고
	/// 컨트롤러가 이미 Construct 를 쓰므로 소유자가 UIRoot 를 넘겨 생성·구동한다.
	///
	/// ⚠ 비주얼 톤(색·폰트·여백)은 기능 가독성 우선의 v0 임시안 — 정식 톤은 사용자 확정 후.
	/// </summary>
	public class TowerDefenseHudView
	{
		private readonly UIRoot uiRoot;
		private readonly VisualElement container;
		private readonly Label resourceValue;
		private readonly Label waveValue;
		private readonly Label phaseValue;
		private readonly Label enemyValue;
		private readonly Label bestValue;
		private readonly Label incomeValue;
		private readonly Label nextWaveValue;
		private readonly Label livesValue;

		// 예고 계산 버퍼 — 매 프레임 새 리스트를 만들지 않는다.
		private readonly System.Collections.Generic.List<int> compositionBuffer = new();
		private int[] archetypeCountBuffer = System.Array.Empty<int>();
		private readonly Label hintLabel;
		private readonly Label bannerLabel;
		private Label relicLabel;
		private Button pullButton;
		private Button pauseButton;
		private Button speedButton;
		private readonly VisualElement legendPanel;
		// 범례 줄이 실제로 들어가는 곳(래퍼는 접기 버튼까지 감싼다).
		private VisualElement legendRows;
		// 자원 노드 위에 붙는 벌이 배수 표. 월드 좌표를 매 프레임 화면으로 투영해 따라붙인다
		// (UI Toolkit 은 월드 공간 텍스트가 없고, 폰트 에셋을 새로 들이지 않기 위한 선택).
		private readonly VisualElement worldLabelLayer;
		private readonly System.Collections.Generic.List<Label> worldLabels = new();
		// 공용 선택 바 — 건설 모드의 건물 바와 같은 물건(개척 전용 툴바를 따로 두지 않는다).
		private readonly ModeSelectionBar selectionBar;
		private readonly Button waveModeButton;
		private readonly Button nextWaveButton;

		// 본편 UI 복원용 — 숨기기 전 값을 보관(무조건 되돌리면 원래 숨김 상태였던 경우를 깨뜨린다).
		private DisplayStyle baseHudPreviousDisplay = DisplayStyle.Flex;
		private DisplayStyle baseWindowsPreviousDisplay = DisplayStyle.Flex;
		private readonly System.Collections.Generic.List<Canvas> hiddenCanvases = new();

		/// <summary>
		/// 「처음부터 다시」 요청 — 소유 컨트롤러가 구독해 매치를 새로 시작한다.
		/// 키가 아니라 화면 버튼인 이유: 새 조작키는 입력 정의 3곳을 동시에 늘려야 하는데,
		/// 재시작은 *자주 안 쓰지만 반드시 보여야 하는* 기능이라 숨은 키보다 보이는 버튼이 맞다.
		/// </summary>
		public event System.Action RestartRequested = delegate { };

		/// <summary> 웨이브 진행 방식(자동↔수동) 전환 요청. </summary>
		public event System.Action WaveModeToggleRequested = delegate { };

		/// <summary> 다음 웨이브 호출 요청 — 수동 진행의 진행 버튼이자, 자동에서도 "지금 와라". </summary>
		public event System.Action NextWaveRequested = delegate { };

		/// <summary> 핫바 칸을 눌러 고름 — 숫자키와 같은 경로로 들어간다(고르는 방법이 둘이어도 결과는 하나). </summary>
		public event System.Action<int> SlotClicked = delegate { };

		/// <summary> 결말 화면에서 유물로 인형 뽑기. </summary>
		public event System.Action PullRequested = delegate { };

		/// <summary> 멈춤 토글 / 배속 순환 — 보고 판단할 시간을 플레이어가 쥔다. </summary>
		public event System.Action PauseToggleRequested = delegate { };
		public event System.Action SpeedCycleRequested = delegate { };

		public TowerDefenseHudView(UIRoot uiRoot)
		{
			this.uiRoot = uiRoot;

			container = new VisualElement { name = nameof(TowerDefenseHudView) };
			container.style.position = Position.Absolute;
			container.style.left = 0;
			container.style.right = 0;
			container.style.top = 0;
			container.style.bottom = 0;
			container.style.display = DisplayStyle.None;
			container.pickingMode = PickingMode.Ignore;

			// ★ 한 덩어리가 한 가지만 말한다 — 예전엔 전부 좌상단에 몰려 있어 무엇부터 봐야 할지 알 수 없었다
			//   (사용자 실증: "정보는 전부 모여 있어서 복잡복잡"). 자원은 상단 가운데 독립 띠(종류가 늘어도
			//   가로로 칸만 추가), 진행은 우상단, 범례는 좌하단 접기, 고르는 것은 하단 가운데.
			container.Add(BuildResourceBar(out resourceValue, out incomeValue));
			container.Add(BuildProgressPanel(out livesValue, out waveValue, out phaseValue, out nextWaveValue, out enemyValue, out bestValue,
				out waveModeButton, out nextWaveButton));
			legendPanel = BuildLegendPanel();
			container.Add(legendPanel);
			selectionBar = new ModeSelectionBar("TowerDefenseSelectionBar");
			selectionBar.Selected += index => SlotClicked(index);
			container.Add(selectionBar.Root);
			container.Add(BuildHintBar(out hintLabel));
			worldLabelLayer = new VisualElement { name = "WorldLabels" };
			worldLabelLayer.style.position = Position.Absolute;
			worldLabelLayer.style.left = 0;
			worldLabelLayer.style.right = 0;
			worldLabelLayer.style.top = 0;
			worldLabelLayer.style.bottom = 0;
			worldLabelLayer.pickingMode = PickingMode.Ignore;
			container.Add(worldLabelLayer);

			container.Add(BuildBanner(out bannerLabel, out _));
			container.Add(BuildCornerRestartButton());

			// 본편 HUD(HudLayer)를 숨겨도 개척 HUD 는 살아있어야 하므로 한 단 위 레이어에 붙인다.
			uiRoot.OverlayLayer.Add(container);
		}

		// 좌상단 컴팩트 스탯 — 폭을 내용에 맞춰 좁게(전폭 바 금지).
		/// <summary>
		/// 자원 띠 — 상단 가운데 독립. 자원 종류가 늘어나면 이 띠에 칸만 가로로 붙인다
		/// (다른 정보와 섞어두면 종류가 늘 때마다 화면 전체를 다시 짜야 한다).
		/// </summary>
		private static VisualElement BuildResourceBar(out Label resource, out Label income)
		{
			VisualElement bar = new VisualElement { name = "ResourceBar" };
			bar.style.position = Position.Absolute;
			bar.style.top = 18;
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

			inner.Add(MakeResourceCell(TowerDefenseIcon.Kind.Diamond, new Color(1f, 0.86f, 0.35f, 1f), out resource, 26));
			inner.Add(MakeDivider());
			inner.Add(MakeResourceCell(TowerDefenseIcon.Kind.Ring, new Color(0.42f, 0.92f, 0.68f, 1f), out income, 20));

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
			panel.Add(MakeStatRow("목숨", out lives, new Color(1f, 0.45f, 0.45f, 1f)));
			panel.Add(MakeStatRow("파도", out wave, new Color(1f, 0.6f, 0.55f, 1f)));
			panel.Add(MakeStatRow("상태", out phase, new Color(0.72f, 0.88f, 1f, 1f)));
			panel.Add(MakeStatRow("다음 파도", out nextWave, new Color(1f, 0.72f, 0.45f, 1f)));
			panel.Add(MakeStatRow("남은 마수", out enemies, new Color(1f, 0.45f, 0.42f, 1f)));
			panel.Add(MakeStatRow("최고 기록", out best, new Color(0.78f, 0.82f, 0.92f, 1f)));

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

		// 하단 중앙 조작 힌트 — 짧게. 전폭으로 늘이지 않는다.
		private static VisualElement BuildHintBar(out Label hint)
		{
			VisualElement wrapper = new VisualElement();
			wrapper.style.position = Position.Absolute;
			wrapper.style.left = 0;
			wrapper.style.right = 0;
			// 선택 바(bottom 24)와 겹치면 글자가 칸 위에 얹혀 둘 다 안 읽힌다(라이브 스크린샷 실증).
			wrapper.style.bottom = 104;
			wrapper.style.alignItems = Align.Center;
			wrapper.pickingMode = PickingMode.Ignore;

			hint = new Label(string.Empty);
			hint.style.fontSize = 13;
			hint.style.color = new Color(0.80f, 0.86f, 0.94f, 1f);
			hint.style.backgroundColor = new Color(0.04f, 0.05f, 0.08f, 0.62f);
			hint.style.paddingLeft = 14;
			hint.style.paddingRight = 14;
			hint.style.paddingTop = 6;
			hint.style.paddingBottom = 6;
			hint.style.unityTextAlign = TextAnchor.MiddleCenter;
			hint.pickingMode = PickingMode.Ignore;

			wrapper.Add(hint);
			return wrapper;
		}

		/// <summary>
		/// 범례 — 좌하단, 접을 수 있다. 항상 펼쳐두면 판을 가리고, 아예 없으면 색이 무슨 뜻인지 알 방법이 없다.
		/// </summary>
		private VisualElement BuildLegendPanel()
		{
			VisualElement wrapper = new VisualElement { name = "LegendWrapper" };
			wrapper.style.position = Position.Absolute;
			wrapper.style.left = 24;
			wrapper.style.bottom = 108;
			wrapper.style.alignItems = Align.FlexStart;
			wrapper.pickingMode = PickingMode.Ignore;

			Button toggle = MakeActionButton("▼ 범례", fontSize: 12, () => { });
			toggle.style.marginBottom = 4;

			VisualElement panel = new VisualElement { name = "LegendPanel" };
			panel.style.paddingLeft = 12;
			panel.style.paddingRight = 16;
			panel.style.paddingTop = 8;
			panel.style.paddingBottom = 8;
			panel.style.backgroundColor = new Color(0.04f, 0.05f, 0.08f, 0.66f);
			SetRadius(panel, 6);
			panel.pickingMode = PickingMode.Ignore;

			toggle.clicked += () =>
			{
				bool visible = panel.style.display != DisplayStyle.None;
				panel.style.display = visible ? DisplayStyle.None : DisplayStyle.Flex;
				toggle.text = visible ? "▶ 범례" : "▼ 범례";
			};

			wrapper.Add(toggle);
			wrapper.Add(panel);
			legendRows = panel;
			return wrapper;
		}

		// 스테이지가 정해지는 시점(진입)에 채운다 — 색 출처가 SO 라 하드코딩 색이 없다.
		private VisualElement BuildCornerRestartButton()
		{
			VisualElement wrapper = new VisualElement();
			wrapper.style.position = Position.Absolute;
			wrapper.style.right = 24;
			// 우상단은 진행 패널 자리 — 겹치면 파도 표시를 덮는다(라이브 스크린샷 실증). 우하단으로 뺀다.
			wrapper.style.bottom = 24;
			wrapper.pickingMode = PickingMode.Ignore;

			wrapper.Add(MakeActionButton("처음부터", fontSize: 13, () => RestartRequested()));
			return wrapper;
		}

		// 버튼은 반드시 pickingMode = Position — 부모들이 Ignore 라 눌리는 건 이 요소뿐이다.
		private static Button MakeActionButton(string text, int fontSize, System.Action onClick)
		{
			Button button = new Button(() => onClick()) { text = text };
			button.style.fontSize = fontSize;
			button.style.color = new Color(0.94f, 0.96f, 1f, 1f);
			button.style.backgroundColor = new Color(0.10f, 0.12f, 0.18f, 0.88f);
			button.style.paddingLeft = 14;
			button.style.paddingRight = 14;
			button.style.paddingTop = 7;
			button.style.paddingBottom = 7;
			button.style.marginLeft = 0;
			button.style.marginRight = 0;
			button.style.borderTopLeftRadius = 5;
			button.style.borderTopRightRadius = 5;
			button.style.borderBottomLeftRadius = 5;
			button.style.borderBottomRightRadius = 5;
			button.style.borderLeftWidth = 1;
			button.style.borderRightWidth = 1;
			button.style.borderTopWidth = 1;
			button.style.borderBottomWidth = 1;
			Color border = new Color(1f, 1f, 1f, 0.22f);
			button.style.borderLeftColor = border;
			button.style.borderRightColor = border;
			button.style.borderTopColor = border;
			button.style.borderBottomColor = border;
			button.pickingMode = PickingMode.Position;
			return button;
		}

		private VisualElement BuildBanner(out Label banner, out Button restartButton)
		{
			VisualElement wrapper = new VisualElement();
			wrapper.style.position = Position.Absolute;
			wrapper.style.left = 0;
			wrapper.style.right = 0;
			wrapper.style.top = 200;
			wrapper.style.alignItems = Align.Center;
			wrapper.style.display = DisplayStyle.None;
			wrapper.name = "BannerWrapper";
			wrapper.pickingMode = PickingMode.Ignore;

			banner = new Label(string.Empty);
			banner.style.fontSize = 34;
			banner.style.color = new Color(1f, 0.85f, 0.4f, 1f);
			banner.style.backgroundColor = new Color(0.04f, 0.05f, 0.08f, 0.8f);
			banner.style.paddingLeft = 24;
			banner.style.paddingRight = 24;
			banner.style.paddingTop = 12;
			banner.style.paddingBottom = 12;
			banner.pickingMode = PickingMode.Ignore;

			// 판 밖에 남는 것 — 이번에 번 유물과 보유량. 끝나는 화면에서 바로 보여야 다음 판 이유가 된다.
			relicLabel = new Label(string.Empty);
			relicLabel.style.fontSize = 17;
			relicLabel.style.color = new Color(0.85f, 0.78f, 1f, 1f);
			relicLabel.style.marginTop = 10;
			relicLabel.style.display = DisplayStyle.None;
			relicLabel.pickingMode = PickingMode.Ignore;

			VisualElement buttons = new VisualElement();
			buttons.style.flexDirection = FlexDirection.Row;
			buttons.style.marginTop = 14;
			buttons.pickingMode = PickingMode.Ignore;

			pullButton = MakeActionButton("인형 뽑기", fontSize: 16, () => PullRequested());
			pullButton.style.marginRight = 8;
			pullButton.style.display = DisplayStyle.None;

			// 끝났는데 다음 행동이 화면에 없으면 게임이 아니라 정지 화면이 된다 — 배너 바로 아래 재시작.
			restartButton = MakeActionButton("다시 도전", fontSize: 18, () => RestartRequested());

			buttons.Add(pullButton);
			buttons.Add(restartButton);

			wrapper.Add(banner);
			wrapper.Add(relicLabel);
			wrapper.Add(buttons);
			return wrapper;
		}


		/// <summary>
		/// 범례 — **화면이 스스로 설명해야 한다**(사용자 지시: "구분이 어떻게 됐는지에 대한 안내가 화면에
		/// 표시되어야겠지"). 아트가 아직 없어 색이 곧 정체이므로, 색 견본 + 이름을 그대로 띄운다.
		/// 색은 스테이지 SO 를 읽어 채운다 — 화면의 유닛 색과 범례 색이 같은 소스여야 안내가 거짓말을 안 한다.
		/// </summary>
		private void FillLegend(TowerDefenseStageSO stage)
		{
			legendRows.Clear();
			if (stage == null)
				return;

			legendRows.Add(MakeLegendRow(stage.CoreTint, "코어", "부서지면 끝", TowerDefenseIcon.Kind.Core));
			if (stage.TowerArchetypes == null || stage.TowerArchetypes.Length == 0)
			{
				legendRows.Add(MakeLegendRow(stage.TowerTint, "포탑 인형", "적을 쏜다"));
			}
			else
			{
				foreach (TowerDefenseTowerArchetype tower in stage.TowerArchetypes)
				{
					if (tower == null)
						continue;
					legendRows.Add(MakeLegendRow(tower.Tint, tower.DisplayName, tower.Note, TowerDefenseIcon.ForTower(tower)));
				}
			}
			legendRows.Add(MakeLegendRow(stage.TrapTint, "함정",
				"밟으면 터진다 · " + stage.TrapCharges + "번 쓰면 사라짐", TowerDefenseIcon.Kind.Burst));
			legendRows.Add(MakeLegendRow(stage.WallTint, "벽",
				"못 지나간다 · 길을 휘게 만든다(완전히 막을 순 없다)", TowerDefenseIcon.Kind.Beam));
			legendRows.Add(MakeLegendRow(stage.LabTint, "연구 인형",
				"세울수록 모든 포탑 피해 +" + Mathf.RoundToInt(stage.LabDamageBonus * 100f) + "%", TowerDefenseIcon.Kind.Ring));
			legendRows.Add(MakeLegendRow(stage.HarvesterTint, "채집 인형",
				"금빛 자리 위에서만 캔다 · 정산마다 +" + stage.Rules.IncomePerHarvester, TowerDefenseIcon.Kind.Leaf));
			if (stage.EnemyArchetypes == null || stage.EnemyArchetypes.Length == 0)
			{
				legendRows.Add(MakeLegendRow(stage.EnemyTint, "마수", "코어로 전진 · 잡으면 +" + stage.Rules.BountyPerKill));
			}
			else
			{
				// 종류별로 한 줄씩 — 섞여 나오는데 범례가 한 줄이면 플레이어는 여전히 한 종류로 본다.
				foreach (TowerDefenseEnemyArchetype archetype in stage.EnemyArchetypes)
				{
					if (archetype == null)
						continue;
					legendRows.Add(MakeLegendRow(archetype.Tint, archetype.DisplayName, DescribeArchetype(archetype)));
				}
			}
			legendRows.Add(MakeLegendRow(new Color(1f, 0.82f, 0.25f, 1f), "금빛 원반", "채집 인형 자리"));
			legendRows.Add(MakeLegendRow(stage.EnemyTint, "붉은 판", "마수 출현"));
		}

		private static VisualElement MakeLegendRow(Color swatchColor, string name, string note)
		{
			return MakeLegendRow(swatchColor, name, note, TowerDefenseIcon.Kind.Dot);
		}

		private static VisualElement MakeLegendRow(Color swatchColor, string name, string note, TowerDefenseIcon.Kind iconKind)
		{
			VisualElement row = new VisualElement();
			row.style.flexDirection = FlexDirection.Row;
			row.style.alignItems = Align.Center;
			row.style.marginBottom = 3;
			row.pickingMode = PickingMode.Ignore;

			VisualElement swatch = TowerDefenseIcon.Make(iconKind, swatchColor, 16);
			swatch.style.marginRight = 8;
			swatch.style.borderTopLeftRadius = 3;
			swatch.style.borderTopRightRadius = 3;
			swatch.style.borderBottomLeftRadius = 3;
			swatch.style.borderBottomRightRadius = 3;
			swatch.pickingMode = PickingMode.Ignore;

			Label nameLabel = new Label(name);
			nameLabel.style.fontSize = 13;
			nameLabel.style.color = new Color(0.92f, 0.94f, 0.98f, 1f);
			nameLabel.style.width = 76;
			nameLabel.pickingMode = PickingMode.Ignore;

			Label noteLabel = new Label(note);
			noteLabel.style.fontSize = 11;
			noteLabel.style.color = new Color(0.80f, 0.84f, 0.90f, 1f);
			noteLabel.pickingMode = PickingMode.Ignore;

			row.Add(swatch);
			row.Add(nameLabel);
			row.Add(noteLabel);
			return row;
		}


		/// <summary>
		/// 설치 핫바 — 기존 건설 모드의 BuildingBarView 와 같은 자리(하단 중앙)·같은 문법(슬롯 선택 → 클릭 설치).
		/// 조작이 게임 전체에서 하나로 통일되고, 설치 종류가 늘어도 슬롯만 늘리면 된다
		/// (가챠로 방어 인형이 늘어나는 방향과 정합).
		/// </summary>

		/// <summary> 선택 표시 갱신 — 컨트롤러가 선택 변경 시 호출. </summary>
		/// <summary> 고른 슬롯 표시 — 포탑 종류가 늘어도 이 함수는 그대로다(슬롯 = 선택의 단위). </summary>
		/// <summary> 고를 수 있는 것들을 공용 바에 넘긴다 — 목록이 바뀌는 시점(진입·재시작)에만. </summary>
		private void FillHotbar(TowerDefenseStageSO stage)
		{
			if (stage == null)
				return;

			System.Collections.Generic.List<ModeSelectionBar.Entry> entries = new();

			if (stage.TowerArchetypes != null && stage.TowerArchetypes.Length > 0)
			{
				// 아직 안 뽑은 인형은 칸 자체가 없다 — 못 쓰는 칸을 보여주면 「눌리지 않는 칸」이 또 생긴다.
				System.Collections.Generic.List<int> unlocked =
					DataManager.TryGetExistingInstance(out DataManager dataManager)
						? dataManager.TowerDefenseUnlockedTowers
						: null;

				for (int index = 0; index < stage.TowerArchetypes.Length; index++)
				{
					TowerDefenseTowerArchetype tower = stage.TowerArchetypes[index];
					if (tower == null)
						continue;
					if (TowerDefenseMeta.IsUnlocked(index, stage.DefaultUnlockedTowerCount, unlocked) == false)
						continue;
					entries.Add(new ModeSelectionBar.Entry(tower.DisplayName, tower.Cost, tower.Tint, tooltip: tower));
				}
			}
			else
			{
				entries.Add(new ModeSelectionBar.Entry("포탑 인형", stage.TowerCost, stage.TowerTint));
			}

			entries.Add(new ModeSelectionBar.Entry("채집 인형", stage.HarvesterCost, stage.HarvesterTint));
			entries.Add(new ModeSelectionBar.Entry("연구 인형", stage.LabCost, stage.LabTint));
			entries.Add(new ModeSelectionBar.Entry("벽", stage.WallCost, stage.WallTint));
			entries.Add(new ModeSelectionBar.Entry("함정", stage.TrapCost, stage.TrapTint));
			selectionBar.SetEntries(entries);
		}

		/// <summary> 고른 칸 표시 — 공용 바가 그린다. </summary>
		public void SetSelectedSlot(int selectedIndex)
		{
			selectionBar.SetSelected(selectedIndex);
		}


		public void Show(TowerDefenseStageSO stage)
		{
			HideBaseGameUI();
			container.style.display = DisplayStyle.Flex;
			ResetForNewMatch(stage);
		}

		/// <summary>
		/// 새 판 상태로 되돌린다 — 배너 숨김 + 범례/핫바 재구성 + 힌트 복원.
		/// ⚠ 재시작 때 <see cref="Show"/> 를 부르면 안 된다: HideBaseGameUI 가 *이미 숨겨진* 본편 UI 를
		/// 다시 훑어 복원 목록이 빈 채로 덮여, 개척을 나갈 때 본편 UI 가 영영 안 돌아온다.
		/// </summary>
		public void ResetForNewMatch(TowerDefenseStageSO stage)
		{
			if (relicLabel != null)
			{
				relicLabel.style.display = DisplayStyle.None;
				pullButton.style.display = DisplayStyle.None;
			}

			FillLegend(stage);
			FillHotbar(stage);
			SetBannerVisible(false);

			hintLabel.text = stage == null
				? string.Empty
				: "좌클릭 설치 · 우클릭 판매 · Space 멈춤 · Tab 배속   ·   WASD 시점 이동   ·   휠 확대·축소   ·   X 나가기";
		}

		public void Hide()
		{
			container.style.display = DisplayStyle.None;
			SetBannerVisible(false);
			RestoreBaseGameUI();
		}

		/// <summary>
		/// 본편 UI 전체 숨김 — 개척은 *다른 게임*이라 본편 UI 가 겹치면 안 된다.
		/// 두 갈래를 모두 덮어야 한다(사용자 실증: 레이어만 껐더니 "체력바·인벤토리가 안 꺼진다"):
		/// ① UI Toolkit 레이어 — HudLayer(핫바/건물바/시계) + WindowsLayer(인벤토리 등 창)
		/// ② 씬의 uGUI 캔버스 — 플레이어 체력바 등은 Toolkit 이 아니라 씬 Canvas 에 있다.
		/// 개척 HUD 는 OverlayLayer(UIDocument) 라 캔버스를 꺼도 살아남는다.
		/// </summary>
		private void HideBaseGameUI()
		{
			if (uiRoot != null)
			{
				if (uiRoot.HudLayer != null)
				{
					baseHudPreviousDisplay = uiRoot.HudLayer.style.display.value;
					uiRoot.HudLayer.style.display = DisplayStyle.None;
				}
				if (uiRoot.WindowsLayer != null)
				{
					baseWindowsPreviousDisplay = uiRoot.WindowsLayer.style.display.value;
					uiRoot.WindowsLayer.style.display = DisplayStyle.None;
				}
			}

			hiddenCanvases.Clear();
			Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
			foreach (Canvas canvas in canvases)
			{
				// 루트 캔버스만 — 중첩 캔버스는 부모가 꺼지면 같이 사라진다(이중 처리 시 복원이 꼬임).
				if (canvas.isRootCanvas == false || canvas.enabled == false)
					continue;

				canvas.enabled = false;
				hiddenCanvases.Add(canvas);
			}
		}

		private void RestoreBaseGameUI()
		{
			if (uiRoot != null)
			{
				if (uiRoot.HudLayer != null)
					uiRoot.HudLayer.style.display = baseHudPreviousDisplay;
				if (uiRoot.WindowsLayer != null)
					uiRoot.WindowsLayer.style.display = baseWindowsPreviousDisplay;
			}

			foreach (Canvas canvas in hiddenCanvases)
			{
				if (canvas != null)
					canvas.enabled = true;
			}
			hiddenCanvases.Clear();
		}

		/// <summary> 매 프레임 갱신 — 소유 컨트롤러가 TD 모드 동안 호출. </summary>
		public void Tick(TowerDefenseMatch match, TowerDefenseStageSO stage)
		{
			if (match == null)
				return;

			resourceValue.text = match.Resource.ToString();

			bool endless = stage == null || stage.Rules.IsEndless;
			waveValue.text = endless
				? (match.WaveIndex + 1).ToString()
				: (match.WaveIndex + 1) + " / " + stage.Rules.WaveCount;

			bool preparing = match.Phase == TowerDefensePhase.Prepare;

			phaseValue.text = match.Phase switch
			{
				// 수동 진행은 남은 시간이 없다 — 시계를 보여주면 곧 시작될 것처럼 읽혀 거짓말이 된다.
				// 첫 파도는 사람이 부를 때까지 안 온다 — 시계를 보여주면 곧 시작될 것처럼 읽혀 거짓말이 된다.
				TowerDefensePhase.Prepare when match.IsWaitingForFirstCall =>
					"주위를 둘러보고, 준비되면 「다음 웨이브」",
				TowerDefensePhase.Prepare when match.AutoAdvanceWaves == false =>
					match.IsNextWaveRequested ? "호출됨" : "건설 중 (대기)",
				TowerDefensePhase.Prepare => "건설 " + Mathf.CeilToInt(match.PrepareRemaining) + "초",
				TowerDefensePhase.Assault => "방어 중",
				_ => "종료",
			};

			enemyValue.text = match.Phase == TowerDefensePhase.Assault
				? match.AliveEnemyCount.ToString()
				: "-";

			// 「기본 + 채집 N기」로 쪼개 보여준다 — 총액만 보이면 그 숫자가 어디서 왔는지 알 수 없다.
			incomeValue.text = match.HarvesterCount > 0
				? match.NextWaveIncome + " (기본 " + stage.Rules.BaseWaveIncome + " + 채집 " + match.HarvesterCount + "기)"
				: match.NextWaveIncome.ToString();

			livesValue.text = match.UsesLives ? match.Lives.ToString() : "-";
			nextWaveValue.text = BuildWavePreview(match);
			UpdateNodeLabels(match, stage);

			bool paused = match.SpeedScale <= 0f;
			pauseButton.text = paused ? "▶ 재개" : "⏸ 멈춤";
			speedButton.text = "배속 ×" + Mathf.Max(1f, match.SpeedScale).ToString("0");
			speedButton.SetEnabled(paused == false);

			waveModeButton.text = match.AutoAdvanceWaves ? "진행: 자동" : "진행: 수동";
			// 건설 국면에서만 부를 수 있다 — 못 누르는 버튼을 멀쩡해 보이게 두면 눌러보고 아무 일도 안 난다.
			nextWaveButton.SetEnabled(preparing && match.Outcome == TowerDefenseOutcome.InProgress);
		}

		/// <summary>
		/// 매치 종료 배너. 무한 모드 패배 = 버틴 웨이브 수가 곧 점수 —
		/// 기록 갱신 여부까지 말해야 「다시 도전」이 이유를 갖는다.
		/// </summary>
		public void ShowOutcome(TowerDefenseOutcome outcome, int wavesCleared, int bestWave, bool isNewRecord,
			int relicsGained, int relicBalance, bool canPull)
		{
			SetBannerVisible(true);
			SetBestRecord(bestWave);
			ShowRelicResult(relicsGained, relicBalance, canPull);

			if (outcome == TowerDefenseOutcome.Victory)
			{
				bannerLabel.text = "개척 성공";
				return;
			}

			// 문구가 숫자를 그대로 뱉으면 「0 웨이브까지 버팀 (최고 0)」 같은 말이 나온다(라이브 실측) —
			// 0 은 「버텼다」가 아니라 「못 넘겼다」이고, 기록 없는 첫 판에 「최고 0」은 알려줄 게 없는 잡음이다.
			string survived = wavesCleared > 0
				? wavesCleared + " 웨이브까지 버팀"
				: "첫 파도도 넘기지 못함";

			if (isNewRecord && wavesCleared > 0)
			{
				bannerLabel.text = survived + " — 최고 기록 갱신";
				return;
			}

			bannerLabel.text = bestWave > 0
				? survived + " (최고 " + bestWave + ")"
				: survived;
		}

		/// <summary>
		/// 결말 화면의 유물·뽑기 — 「이번 판에서 무엇을 얻었고, 그걸로 무엇을 할 수 있나」가
		/// 끝나는 화면 안에서 닫혀야 다시 도전할 이유가 생긴다.
		/// </summary>
		private void ShowRelicResult(int relicsGained, int relicBalance, bool canPull)
		{
			if (relicLabel == null)
				return;

			relicLabel.text = "유물 +" + relicsGained + "  (보유 " + relicBalance + ")";
			relicLabel.style.display = DisplayStyle.Flex;
			pullButton.SetEnabled(canPull);
			pullButton.style.display = DisplayStyle.Flex;
			pullButton.text = "인형 뽑기";
		}

		/// <summary> 뽑기 결과 — 무엇이 나왔는지 그 자리에서 말한다. </summary>
		public void ShowPullResult(TowerDefenseTowerArchetype pulled, int relicBalance, bool canPull)
		{
			if (relicLabel == null)
				return;

			relicLabel.text = (pulled != null ? "「" + pulled.DisplayName + "」 획득" : "인형 획득")
				+ "  (유물 " + relicBalance + ")";
			pullButton.SetEnabled(canPull);
		}

		/// <summary> 최고 기록 표시 — 기록 없으면 「-」(0 웨이브라고 거짓말하지 않는다). </summary>
		public void SetBestRecord(int bestWave)
		{
			bestValue.text = bestWave > 0 ? bestWave.ToString() : "-";
		}

		/// <summary> 종류 설명 — 체력·속도가 「어떻게 다른지」를 말로 준다(숫자만 보면 감이 안 온다). </summary>
		private static string DescribeArchetype(TowerDefenseEnemyArchetype archetype)
		{
			string toughness = archetype.HealthMultiplier >= 1.5f ? "단단함"
				: archetype.HealthMultiplier <= 0.7f ? "물렁함"
				: "보통";
			string pace = archetype.SpeedMultiplier >= 1.3f ? "빠름"
				: archetype.SpeedMultiplier <= 0.8f ? "느림"
				: "보통";
			return toughness + " · " + pace + " · 잡으면 +" + archetype.Bounty;
		}

		/// <summary> 「다음 파도: 돌진 3 · 방패 1」 — 매치가 실제 스폰에 쓰는 그 계산을 그대로 부른다. </summary>
		private string BuildWavePreview(TowerDefenseMatch match)
		{
			int previewWave = match.Phase == TowerDefensePhase.Assault ? match.WaveIndex + 1 : match.WaveIndex;
			match.ComposeWave(previewWave, compositionBuffer);

			// 파도 성격은 색까지 바꿔 예고한다 — 「무엇이 오는가」를 한눈에 알아야 대비가 성립한다.
			TowerDefenseWaveEventKind previewEvent = match.WaveEventAt(previewWave);
			nextWaveValue.style.color = TowerDefenseWaveEvent.DisplayColor(previewEvent);
			string eventPrefix = previewEvent == TowerDefenseWaveEventKind.None
				? string.Empty
				: "《" + TowerDefenseWaveEvent.DisplayName(previewEvent) + "》 ";

			// 적응은 반드시 보여야 한다 — 안 보이면 플레이어는 자기 포탑이 고장 났다고 여긴다.
			string adaptationNote = TowerDefenseAdaptation.Describe(match.Adaptation);
			if (adaptationNote.Length > 0)
				eventPrefix += "[" + adaptationNote + "] ";

			int archetypeCount = match.EnemyArchetypeCount;
			if (archetypeCount <= 0)
				return eventPrefix + compositionBuffer.Count + "기";

			if (archetypeCountBuffer.Length < archetypeCount)
				archetypeCountBuffer = new int[archetypeCount];

			TowerDefenseWaveComposer.CountByArchetype(compositionBuffer, archetypeCount, archetypeCountBuffer);

			string preview = string.Empty;
			for (int index = 0; index < archetypeCount; index++)
			{
				if (archetypeCountBuffer[index] <= 0)
					continue;
				TowerDefenseEnemyArchetype archetype = match.EnemyArchetypeAt(index);
				if (archetype == null)
					continue;
				if (preview.Length > 0)
					preview += " · ";
				preview += archetype.DisplayName + " " + archetypeCountBuffer[index];
			}

			return eventPrefix + (preview.Length > 0 ? preview : compositionBuffer.Count + "기");
		}

		/// <summary>
		/// 자원 노드마다 「×1.9」 벌이 배수를 띄운다 — 노드가 다 똑같아 보이면 「어디로 넓힐까」가 판단이 안 된다.
		/// 화면 밖으로 나간 노드는 감춘다(뒤쪽 노드가 화면 가장자리에 눌어붙는 것 방지).
		/// </summary>
		private void UpdateNodeLabels(TowerDefenseMatch match, TowerDefenseStageSO stage)
		{
			System.Collections.Generic.IReadOnlyList<Vector3> nodes = match.ActiveResourceNodePositions;
			Camera camera = ViewCameraResolver.Current;
			Transform stageRoot = match.StageRoot;

			while (worldLabels.Count < nodes.Count)
			{
				Label label = new Label(string.Empty);
				label.style.position = Position.Absolute;
				label.style.fontSize = 15;
				label.style.color = new Color(1f, 0.86f, 0.35f, 1f);
				label.style.unityTextAlign = TextAnchor.MiddleCenter;
				label.pickingMode = PickingMode.Ignore;
				worldLabelLayer.Add(label);
				worldLabels.Add(label);
			}

			for (int index = 0; index < worldLabels.Count; index++)
			{
				Label label = worldLabels[index];
				if (index >= nodes.Count || camera == null || stageRoot == null)
				{
					label.style.display = DisplayStyle.None;
					continue;
				}

				Vector3 worldPosition = stageRoot.TransformPoint(nodes[index]);

				// 아직 못 가본 자리의 벌이를 알려주면 시야가 무의미해진다 — 밝혔던 곳만 숫자를 보여준다.
				if (match.IsExploredAt(worldPosition) == false)
				{
					label.style.display = DisplayStyle.None;
					continue;
				}

				Vector3 screenPosition = camera.WorldToScreenPoint(worldPosition);
				if (screenPosition.z <= 0f)
				{
					label.style.display = DisplayStyle.None;
					continue;
				}

				label.style.display = DisplayStyle.Flex;
				label.text = "×" + match.NodeIncomeMultiplierAt(index).ToString("0.0");
				label.style.left = screenPosition.x - 22f;
				label.style.top = Screen.height - screenPosition.y - 34f;
			}
		}

		private void SetBannerVisible(bool visible)
		{
			VisualElement wrapper = bannerLabel.parent;
			if (wrapper != null)
				wrapper.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
		}
	}
}
