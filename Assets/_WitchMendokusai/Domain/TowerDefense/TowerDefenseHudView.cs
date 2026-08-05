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
		private readonly Label essenceValue;

		// 예고 계산 버퍼 — 매 프레임 새 리스트를 만들지 않는다.
		private readonly System.Collections.Generic.List<int> compositionBuffer = new();
		private int[] archetypeCountBuffer = System.Array.Empty<int>();
		private readonly Label hintLabel;
		private readonly Label bannerLabel;
		private Label relicLabel;
		private Button pullButton;
		private Button pauseButton;
		private Button rangeDebugButton;
		private Button uiScaleButton;
		private Button difficultyButton;
		private Button speedButton;
		private readonly VisualElement legendPanel;
		// 범례 줄이 실제로 들어가는 곳(래퍼는 접기 버튼까지 감싼다).
		private VisualElement legendRows;
		// 자원 노드 위에 붙는 벌이 배수 표. 월드 좌표를 매 프레임 화면으로 투영해 따라붙인다
		// (UI Toolkit 은 월드 공간 텍스트가 없고, 폰트 에셋을 새로 들이지 않기 위한 선택).
		private readonly VisualElement worldLabelLayer;
		private readonly System.Collections.Generic.List<Label> worldLabels = new();
		// 인형 이름표 — 노드 배수표와 같은 방식(월드→화면 투영)이지만 대상이 다르므로 목록을 나눈다.
		private readonly System.Collections.Generic.List<Label> dollLabelViews = new();
		// 건물 머리 위 작은 바 — 이름표와 같은 좌표계라 같은 목록으로 관리한다(따로 두면 어긋난다).
		private readonly System.Collections.Generic.List<VisualElement> dollBarViews = new();
		// 웨이브 사이 드래프트 — 카드가 걸리면 화면 한가운데를 막는다(고르기 전엔 아무것도 못 한다).
		private Label boonSummaryLabel;
		// 커서가 얹힌 유닛 설명 — 「이게 뭐고 얼마나 버티나」를 물어볼 유일한 수단.
		private VisualElement unitTooltip;
		private Label unitTooltipLabel;
		// 고른 건물에 하는 일 — 지금은 코어의 「연구」 하나지만, 건물 레벨·선택지가 붙을 자리다.
		private VisualElement selectionPanel;
		private Label selectionTitleLabel;
		private Button researchButton;
		private VisualElement perkRow;
		private VisualElement coreCardRow;
		private TowerDefenseMinimapView minimap;
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

		/// <summary> 드래프트 카드 선택(인덱스) — 고르기 전엔 판이 멈춰 있다. </summary>

		/// <summary> 디버그 — 세워둔 것 전부의 사거리를 한 번에 보여준다/감춘다. </summary>
		public event System.Action ToggleAllRangesRequested = delegate { };

		/// <summary> 판을 나가겠다 — 모서리 버튼(의도가 분명한 손짓). </summary>
		public event System.Action ExitRequested = delegate { };

		/// <summary> 고른 건물을 팔겠다 / 창을 닫겠다. </summary>
		public event System.Action SellSelectedRequested = delegate { };
		public event System.Action SelectionCloseRequested = delegate { };

		private Button sellButton;

		/// <summary> 코어를 고른 채 「연구」를 눌렀다. </summary>

		/// <summary> UI 배율을 한 단계 돌린다. </summary>
		public event System.Action UiScaleCycleRequested = delegate { };

		/// <summary> 고른 건물의 레벨업 선택지를 골랐다. </summary>
		public event System.Action<TowerDefenseBuildingPerk> BuildingPerkChosen = delegate { };

		/// <summary> 코어 레벨업 카드를 골랐다(판 전체에 걸린다). </summary>
		public event System.Action<int> CoreCardChosen = delegate { };

		/// <summary> 난이도를 한 단계 돌린다 — 다음 판부터 걸린다. </summary>
		public event System.Action DifficultyCycleRequested = delegate { };

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
			container.Add(Named(BuildResourceBar(out resourceValue, out incomeValue, out essenceValue), "ResourceBar"));
			container.Add(Named(BuildProgressPanel(out livesValue, out waveValue, out phaseValue, out nextWaveValue, out enemyValue, out bestValue,
				out waveModeButton, out nextWaveButton), "ProgressPanel"));
			// ★ 범례는 이제 *지도 안*에 산다 — 판 옆에 상시로 펼쳐 두면 화면 4분의 1 을 먹으면서도
			//   정작 「저게 뭐였지」 할 때는 안 읽힌다(사용자 실증). 지도를 여는 행위가 곧 알아보는 행위다.
			legendPanel = Named(BuildLegendPanel(), "LegendPanel");
			mapPanel = new TowerDefenseMapPanel(legendPanel);
			mapPanel.LookAtRequested += focus => LookAtRequested(focus);
			container.Add(mapPanel.Root);
			selectionBar = new ModeSelectionBar("TowerDefenseSelectionBar") { CardLayout = true };
			selectionBar.Selected += index => SlotClicked(index);
			container.Add(selectionBar.Root);
			container.Add(Named(BuildHintBar(out hintLabel), "HintBar"));
			container.Add(Named(BuildArmedBar(out armedLabel), "ArmedBar"));
			worldLabelLayer = new VisualElement { name = "WorldLabels" };
			worldLabelLayer.style.position = Position.Absolute;
			worldLabelLayer.style.left = 0;
			worldLabelLayer.style.right = 0;
			worldLabelLayer.style.top = 0;
			worldLabelLayer.style.bottom = 0;
			worldLabelLayer.pickingMode = PickingMode.Ignore;
			container.Add(worldLabelLayer);

			container.Add(BuildBanner(out bannerLabel, out _));
			container.Add(Named(BuildCornerRestartButton(), "RestartButton"));
			container.Add(Named(BuildBoonSummary(out boonSummaryLabel), "BoonSummary"));
			unitTooltip = Named(BuildUnitTooltip(out unitTooltipLabel), "UnitTooltip");
			// ★ 툴팁은 HUD 안이 아니라 *최상단 층*에 산다 — 같은 층에 두면 핫바·패널 뒤로 숨는다
			//   (사용자 실증: "툴팁이 핫바 뒤에 나오는데 이걸 왜 인지를 못하는지"). 층이 답이다.
			uiRoot.TooltipLayer.Add(unitTooltip);
			container.Add(BuildSelectionPanel(out selectionPanel, out selectionTitleLabel, out researchButton));
			selectionPanel.name = "SelectionPanel";

			// 미니맵 — 판이 무한으로 자라므로 「전체를 보는 눈」이 없으면 넓은 판이 넓지 않은 것과 같다.
			minimap = new TowerDefenseMinimapView();
			minimap.Clicked += focus => LookAtRequested(focus);
			minimap.Root.name = "Minimap";
			container.Add(minimap.Root);

			// 본편 HUD(HudLayer)를 숨겨도 개척 HUD 는 살아있어야 하므로 한 단 위 레이어에 붙인다.
			// ★ 개척 HUD 는 *모드 HUD 층*이다 — 본편 HUD 를 통째 숨겨도 살아남되, 사람이 여는 창
			//   (티메토 등)보다는 아래여야 한다. 예전엔 최상단 Overlay 에 있어서 핫바가 티메토 창을
			//   덮었다(사용자 실증: "티메토 UI보다 핫바가 더 위쪽에 보이는 문제").
			uiRoot.ModeHudLayer.Add(container);
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
		private static VisualElement BuildResourceBar(out Label resource, out Label income, out Label essence)
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
			// 정수 — 자원 띠에 칸이 하나 붙는다. 「종류가 늘면 가로로 칸만 추가」로 설계해 둔 것이 여기서 회수된다.
			inner.Add(MakeDivider());
			inner.Add(MakeResourceCell(TowerDefenseIcon.Kind.Core, new Color(0.7f, 0.6f, 1f, 1f), out essence, 24));

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
			panel.Add(MakeStatRow("웨이브", out wave, new Color(1f, 0.6f, 0.55f, 1f)));
			panel.Add(MakeStatRow("상태", out phase, new Color(0.72f, 0.88f, 1f, 1f)));
			panel.Add(MakeStatRow("다음 웨이브", out nextWave, new Color(1f, 0.72f, 0.45f, 1f)));
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
			// 선택 바와 겹치면 글자가 칸 위에 얹혀 둘 다 안 읽힌다(라이브 실증).
			// ★ 숫자를 다시 적지 않고 *선택 바가 차지한 높이*를 읽는다 — 예전엔 104 라고 손으로 적어뒀다가
			//   칸 높이가 자라자 2px 파묻혔다(좌표 검사가 잡아냄). 여백만 더한다.
			const int HINT_GAP = 8;
			wrapper.style.bottom = ModeSelectionBar.TopFromBottom + HINT_GAP;
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
			// 지도 안에서 읽는 글이라 뒤가 비치면 안 된다 — 진행 상황판 위에 겹쳐 둘 다 안 읽혔다(실측).
			panel.style.backgroundColor = new Color(0.04f, 0.05f, 0.08f, 0.94f);
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
			// 우상단은 진행 패널 자리 — 겹치면 웨이브 표시를 덮는다(라이브 스크린샷 실증). 우하단으로 뺀다.
			wrapper.style.bottom = 24;
			wrapper.pickingMode = PickingMode.Ignore;

			wrapper.style.flexDirection = FlexDirection.Row;

			// 디버그 — 세워둔 것 전부의 사거리를 한 번에. 상시 표시는 껐지만 「전체를 보고 싶은 순간」은 있다.
			// 난이도 — 「다음 판부터」라고 말해주는 것까지가 이 버튼의 일이다(지금 판이 안 바뀌는데
			// 바뀐 줄 알면 그게 거짓말이다).
			// 지도 — 「미니맵만 두지 말고 맵을 UI 로 열 수 있게」(사용자 지시). 범례도 그 안에 있다.
			Button mapButton = MakeActionButton("지도", fontSize: 13, () => ToggleMap());
			mapButton.style.marginRight = 8;
			wrapper.Add(mapButton);

			difficultyButton = MakeActionButton("난이도: 보통", fontSize: 13, () => DifficultyCycleRequested());
			difficultyButton.style.marginRight = 8;
			wrapper.Add(difficultyButton);

			uiScaleButton = MakeActionButton("UI ×1", fontSize: 13, () => UiScaleCycleRequested());
			uiScaleButton.style.marginRight = 8;
			wrapper.Add(uiScaleButton);

			rangeDebugButton = MakeActionButton("사거리 전체", fontSize: 13, () => ToggleAllRangesRequested());
			rangeDebugButton.style.marginRight = 8;
			wrapper.Add(rangeDebugButton);

			wrapper.Add(MakeActionButton("처음부터", fontSize: 13, () => RestartRequested()));
			// ★ 나가는 문은 하나다 (TASK-WM-200) — 예전엔 이 버튼이 곧바로 판을 끝냈다. 그건 취소 키가
			//   판을 끝내던 것과 같은 병이다(되돌릴 수 없는 일이 한 번의 손짓 거리에 있다). 이제 이
			//   버튼도 메뉴를 열고, 나가기는 그 안에서 한 번 더 말해야 한다. 폰에는 키가 없으니
			//   메뉴로 가는 *화면 위의 길*이기도 하다.
			Button menuButton = MakeActionButton("메뉴", fontSize: 13, () => MenuToggleRequested());
			menuButton.style.marginLeft = 8;
			wrapper.Add(menuButton);
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

		/// <summary>
		/// 카드 한 장 — 이름·설명만. 숫자를 더 얹으면 세 장을 비교하는 데 시간이 걸린다.
		///
		/// ★ 이 모양을 코어 레벨업 선택이 그대로 쓴다. 예전엔 「화면 한가운데 예쁜 카드」와
		///   「선택창의 수수한 버튼」이 따로 있었고, 정작 *뜨는 쪽은 수수한 버튼*이었다
		///   (예쁜 쪽은 부르는 데가 없어 한 번도 안 떴다). 한 벌로 합쳐 뜨는 쪽이 예쁜 것을 쓴다.
		/// ★ compact = 선택창처럼 좁은 자리에 들어갈 때. 같은 카드가 크기만 줄어든다
		///   (모양을 두 벌로 만들면 또 갈라진다).
		/// </summary>
		private VisualElement MakeBoonCard(TowerDefenseBoon boon, System.Action onChosen, bool compact)
		{
			Button card = new Button(() => onChosen());
			card.style.width = compact ? 118 : 200;
			card.style.height = compact ? 96 : 132;
			card.style.marginLeft = compact ? 3 : 10;
			card.style.marginRight = compact ? 3 : 10;
			card.style.backgroundColor = new Color(0.10f, 0.12f, 0.18f, 0.96f);
			card.style.alignItems = Align.Center;
			card.style.justifyContent = Justify.Center;
			SetRadius(card, 10);
			card.style.borderLeftWidth = 2;
			card.style.borderRightWidth = 2;
			card.style.borderTopWidth = 2;
			card.style.borderBottomWidth = 2;
			Color accent = BoonColor(boon.Kind);
			card.style.borderLeftColor = accent;
			card.style.borderRightColor = accent;
			card.style.borderTopColor = accent;
			card.style.borderBottomColor = accent;
			card.pickingMode = PickingMode.Position;

			card.Add(TowerDefenseIcon.Make(BoonIcon(boon.Kind), accent, compact ? 22 : 34));

			Label name = new Label(boon.DisplayName);
			name.style.fontSize = compact ? 13 : 18;
			name.style.color = new Color(0.96f, 0.97f, 1f, 1f);
			name.style.marginTop = compact ? 5 : 10;
			name.pickingMode = PickingMode.Ignore;
			card.Add(name);

			Label note = new Label(boon.Note);
			note.style.fontSize = compact ? 10 : 13;
			note.style.color = accent;
			note.style.marginTop = compact ? 3 : 6;
			note.pickingMode = PickingMode.Ignore;
			card.Add(note);

			return card;
		}

		// 종류마다 색·아이콘이 갈려야 세 장이 한눈에 구분된다(글자를 읽어야 알면 그건 목록이지 카드가 아니다).
		private static Color BoonColor(TowerDefenseBoonKind kind)
		{
			return kind switch
			{
				TowerDefenseBoonKind.Firepower => new Color(1f, 0.55f, 0.45f, 1f),
				TowerDefenseBoonKind.Income => new Color(0.42f, 0.92f, 0.68f, 1f),
				TowerDefenseBoonKind.Bounty => new Color(1f, 0.86f, 0.35f, 1f),
				TowerDefenseBoonKind.Life => new Color(1f, 0.62f, 0.9f, 1f),
				TowerDefenseBoonKind.Essence => new Color(0.7f, 0.6f, 1f, 1f),
				_ => new Color(0.62f, 0.82f, 1f, 1f),
			};
		}

		private static TowerDefenseIcon.Kind BoonIcon(TowerDefenseBoonKind kind)
		{
			return kind switch
			{
				TowerDefenseBoonKind.Firepower => TowerDefenseIcon.Kind.Burst,
				TowerDefenseBoonKind.Income => TowerDefenseIcon.Kind.Ring,
				TowerDefenseBoonKind.Bounty => TowerDefenseIcon.Kind.Diamond,
				TowerDefenseBoonKind.Life => TowerDefenseIcon.Kind.Core,
				TowerDefenseBoonKind.Essence => TowerDefenseIcon.Kind.Snow,
				_ => TowerDefenseIcon.Kind.Leaf,
			};
		}

		/// <summary> 지금까지 고른 것 — 자원 띠 바로 아래. 안 보이면 「내가 뭘 골랐더라」가 판 내내 미궁이 된다. </summary>
		private static VisualElement BuildBoonSummary(out Label summary)
		{
			VisualElement wrapper = new VisualElement { name = "BoonSummary" };
			wrapper.style.position = Position.Absolute;
			wrapper.style.top = 62;
			wrapper.style.left = 0;
			wrapper.style.right = 0;
			wrapper.style.alignItems = Align.Center;
			wrapper.pickingMode = PickingMode.Ignore;

			summary = new Label(string.Empty);
			summary.style.fontSize = 13;
			summary.style.color = new Color(1f, 0.88f, 0.5f, 0.92f);
			summary.style.display = DisplayStyle.None;
			summary.pickingMode = PickingMode.Ignore;

			wrapper.Add(summary);
			return wrapper;
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
				child.style.scale = new StyleScale(new Scale(new Vector2(scale, scale)));
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

		/// <summary>
		/// 고른 건물 패널 — 좌하단. 「이미 서 있는 것에 하는 일」이 여기 모인다(지금은 코어의 연구).
		///
		/// ★ 왜 고른 뒤에 뜨나 (사용자 지시: "레벨 업 할때마다 화면에 바로 띄우면 안될 것 같고,
		///   건물 선택하면 그때 띄우거나"): 건물이 수십 개가 되면 각자의 알림이 화면을 덮는다.
		///   물어본 것에만 답하는 화면이 결국 더 많은 것을 보여준다.
		/// </summary>
		// 미니맵 위에 앉는 높이 — 미니맵이 커지면 이 값도 같이 커져야 한다(같은 모서리를 나눠 쓴다).
		private const int SELECTION_PANEL_BOTTOM = 330;

		private VisualElement unlockPathBox;

		/// <summary>
		/// 연구 길을 편다 — 「이미 연 것 · 다음에 열릴 것 · 그 뒤」 (WM-200).
		///
		/// ★ 표는 규칙층이 준 것 그대로다. 여기서 다시 계산하면 창이 약속한 것과 실제로 열리는 것이
		///   어긋나고, 그건 자원을 잘못 쓰게 만드는 거짓말이 된다.
		/// </summary>
		public void ShowUnlockPath(System.Collections.Generic.IReadOnlyList<TowerDefenseUnlockEntry> path, int researchLevel)
		{
			if (unlockPathBox == null)
				return;

			if (path == null || path.Count == 0)
			{
				unlockPathBox.style.display = DisplayStyle.None;
				return;
			}

			unlockPathBox.style.display = DisplayStyle.Flex;
			unlockPathBox.Clear();

			Label heading = new Label("연구로 열리는 것");
			heading.style.fontSize = 15;
			heading.style.color = new Color(0.62f, 0.68f, 0.78f, 1f);
			heading.style.marginBottom = 8;
			heading.pickingMode = PickingMode.Ignore;
			unlockPathBox.Add(heading);

			int nextLevel = researchLevel + 1;
			for (int i = 0; i < path.Count; i++)
			{
				TowerDefenseUnlockEntry entry = path[i];
				bool opened = entry.Level <= researchLevel;
				bool isNext = entry.Level == nextLevel;

				VisualElement row = new VisualElement();
				row.style.flexDirection = FlexDirection.Row;
				row.style.alignItems = Align.Center;
				row.style.paddingTop = 3;
				row.style.paddingBottom = 3;
				row.style.paddingLeft = 8;
				row.style.paddingRight = 8;
				row.pickingMode = PickingMode.Ignore;
				// 다음에 열릴 줄만 배경으로 띄운다 — 지금 값을 치르면 얻는 것이 그것뿐이라서.
				if (isNext)
				{
					row.style.backgroundColor = new Color(0.16f, 0.28f, 0.48f, 0.75f);
					SetRadius(row, 6);
				}

				Label mark = new Label(opened ? "✔" : (isNext ? "▶" : "·"));
				mark.style.width = 22;
				mark.style.fontSize = 14;
				mark.style.color = opened
					? new Color(0.45f, 0.82f, 0.5f, 1f)
					: (isNext ? new Color(1f, 0.86f, 0.42f, 1f) : new Color(0.45f, 0.5f, 0.6f, 1f));
				mark.pickingMode = PickingMode.Ignore;
				row.Add(mark);

				Label stepLabel = new Label(entry.Level == 0 ? "처음부터" : entry.Level + "단계");
				stepLabel.style.width = 76;
				stepLabel.style.fontSize = 14;
				stepLabel.style.color = opened
					? new Color(0.6f, 0.66f, 0.74f, 1f)
					: new Color(0.72f, 0.78f, 0.88f, 1f);
				stepLabel.pickingMode = PickingMode.Ignore;
				row.Add(stepLabel);

				Label nameLabel = new Label(UnlockName(entry));
				nameLabel.style.fontSize = 15;
				nameLabel.style.color = opened
					? new Color(0.58f, 0.64f, 0.72f, 1f)
					: new Color(0.94f, 0.96f, 1f, 1f);
				nameLabel.pickingMode = PickingMode.Ignore;
				row.Add(nameLabel);

				unlockPathBox.Add(row);
			}
		}

		private static string UnlockName(TowerDefenseUnlockEntry entry)
		{
			return entry.Kind switch
			{
				TowerDefensePlaceableKind.Harvester => "채집 인형",
				TowerDefensePlaceableKind.Wall => "벽",
				TowerDefensePlaceableKind.Trap => "함정",
				TowerDefensePlaceableKind.Outpost => "전초기지",
				TowerDefensePlaceableKind.Generator => "발전 인형",
				TowerDefensePlaceableKind.Tower => "포탑 인형 " + (entry.TowerIndex + 1) + "종",
				_ => "?",
			};
		}

		private VisualElement BuildSelectionPanel(out VisualElement panel, out Label title, out Button research)
		{
			panel = new VisualElement { name = "SelectionPanel" };
			panel.style.position = Position.Absolute;
			// ★ 왼쪽 아래는 범례 자리다 — 거기 두면 둘이 포개져 둘 다 안 읽힌다(좌표 검사가 잡아냄).
			//   오른쪽 아래로 보내되 미니맵 *위*에 앉힌다: 손이 가는 곳(핫바) 근처면서 겹치는 것이 없다.
			// ★ 구석의 작은 상자였다 — 연구처럼 *판을 결정하는 창*이 거기 있으면 안 보인다
			//   (사용자 실증: "연구 전체화면 UI로. 작아서 안보여"). 화면 가운데 큰 창으로 올린다.
			panel.style.left = 0;
			panel.style.right = 0;
			panel.style.top = 0;
			panel.style.bottom = 0;
			panel.style.alignItems = Align.Center;
			panel.style.justifyContent = Justify.Center;
			panel.style.paddingLeft = 0;
			panel.style.paddingRight = 0;
			panel.style.paddingTop = 0;
			panel.style.paddingBottom = 0;
			panel.style.backgroundColor = new Color(0.02f, 0.03f, 0.05f, 0.78f);
			panel.style.display = DisplayStyle.None;
			panel.pickingMode = PickingMode.Position;

			// 안쪽 카드 — 글과 버튼은 여기 담긴다(바깥은 화면을 덮는 어둠).
			VisualElement card = new VisualElement();
			card.style.minWidth = 560;
			card.style.maxWidth = 760;
			card.style.paddingLeft = 28;
			card.style.paddingRight = 28;
			card.style.paddingTop = 22;
			card.style.paddingBottom = 22;
			card.style.backgroundColor = new Color(0.05f, 0.06f, 0.10f, 0.98f);
			card.style.alignItems = Align.Center;
			SetRadius(card, 12);
			panel.Add(card);

			title = new Label(string.Empty);
			title.style.fontSize = 20;
			title.style.whiteSpace = WhiteSpace.Normal;
			title.style.unityTextAlign = TextAnchor.MiddleCenter;
			title.style.color = new Color(0.94f, 0.96f, 1f, 1f);
			title.style.whiteSpace = WhiteSpace.Normal;
			title.style.marginBottom = 8;
			title.pickingMode = PickingMode.Ignore;
			card.Add(title);

			// ★ 「연구」 = **성좌를 연다**. 예전엔 이 단추가 곧바로 한 단계를 사들였다(값 치르고 끝).
			//   성좌를 만들어 놓고도 **여는 문이 없어서**, 화면 가득한 연구도가 게임에서 도달 불가였다
			//   (이벤트를 받는 쪽은 있는데 쏘는 곳이 한 군데도 없었다 — 조용히 죽은 기능의 전형).
			//   단계 올리기는 이제 성좌의 큰 마디가 한다.
			research = MakeActionButton("연구", fontSize: 18, () => ResearchPanelRequested());
			research.style.display = DisplayStyle.None;
			research.style.height = 48;
			research.style.marginTop = 14;
			research.style.paddingLeft = 24;
			research.style.paddingRight = 24;
			card.Add(research);

			// ★ 연구 창이 *무엇을 얻는지* 말하지 않았다 (실측: 자원 60 을 내라면서 대가가 화면에 없음).
			//   값을 치르는 결정인데 대가를 모르면 그건 선택이 아니라 도박이다. 연구 길을 그대로 편다 —
			//   이미 연 것 / 다음에 열릴 것 / 그 뒤에 올 것이 한눈에.
			unlockPathBox = new VisualElement();
			unlockPathBox.style.display = DisplayStyle.None;
			unlockPathBox.style.marginTop = 16;
			unlockPathBox.style.alignSelf = Align.Stretch;
			unlockPathBox.pickingMode = PickingMode.Ignore;
			card.Add(unlockPathBox);

			// ★ 판매를 여기서도 — 「빌딩 모드에서만 우클릭」은 손이 기억해야 하는 규칙이라,
			//   고른 건물을 보고 있는 그 자리에 버튼을 둔다(사용자 지시: "건물 정보에 강화나 판매를").
			sellButton = MakeActionButton("팔기", fontSize: 16, () => SellSelectedRequested());
			sellButton.style.display = DisplayStyle.None;
			sellButton.style.height = 40;
			sellButton.style.marginTop = 10;
			sellButton.style.paddingLeft = 20;
			sellButton.style.paddingRight = 20;
			card.Add(sellButton);

			// 닫기 — 전체화면 창은 나가는 문이 보여야 한다.
			Button close = MakeActionButton("닫기", fontSize: 14, () => SelectionCloseRequested());
			close.style.marginTop = 16;
			card.Add(close);

			// 레벨업으로 고를 것이 쌓여 있으면 여기에 세 장이 뜬다 — 화면 한가운데를 막지 않는다.
			// 코어 레벨업 카드 — 판 전체에 걸리는 것이라 건물 선택지와 줄을 나눈다(성격이 다르다).
			coreCardRow = new VisualElement();
			coreCardRow.style.flexDirection = FlexDirection.Row;
			coreCardRow.style.flexWrap = Wrap.Wrap; // 카드가 셋이면 좁은 선택창에서 줄이 넘어간다.
			coreCardRow.style.marginTop = 8;
			coreCardRow.pickingMode = PickingMode.Ignore;
			card.Add(coreCardRow);

			perkRow = new VisualElement();
			perkRow.style.flexDirection = FlexDirection.Row;
			perkRow.style.marginTop = 8;
			perkRow.pickingMode = PickingMode.Ignore;
			card.Add(perkRow);

			return panel;
		}

		/// <summary> 고른 건물을 보여준다 — 아무것도 안 골랐으면 패널 자체를 감춘다. </summary>
		public void ShowSelection(string description, bool canResearch, int researchLevel, int researchCost,
			bool researchUsesEssence = true,
			System.Collections.Generic.IReadOnlyList<TowerDefenseBuildingPerk> perkOffers = null,
			System.Collections.Generic.IReadOnlyList<TowerDefenseBoon> coreCards = null)
		{
			if (selectionPanel == null)
				return;

			if (string.IsNullOrEmpty(description))
			{
				selectionPanel.style.display = DisplayStyle.None;
				return;
			}

			// 연구 길은 연구할 수 있는 것(코어)에만 뜻이 있다 — 벽을 보면서 연구 길이 펼쳐지면 헷갈린다.
			if (canResearch == false && unlockPathBox != null)
				unlockPathBox.style.display = DisplayStyle.None;

			selectionPanel.style.display = DisplayStyle.Flex;
			selectionTitleLabel.text = description;
			researchButton.style.display = canResearch ? DisplayStyle.Flex : DisplayStyle.None;
			// 코어는 못 판다 — 팔 수 있는 것에만 버튼을 보인다(눌리지 않는 버튼을 만들지 않는다).
			if (sellButton != null)
				sellButton.style.display = canResearch ? DisplayStyle.None : DisplayStyle.Flex;
			if (canResearch)
				// ★ 이 단추는 이제 *성좌를 연다* — 곧바로 한 단계를 사지 않는다. 그래서 값을 안 적는다.
				//   값은 성좌 안에서 마디마다 다르고, 여기 적힌 값은 아무도 안 걷는 거짓말이 된다.
				//   (화면이 잘못 알려주면 플레이어는 엉뚱한 것을 모으러 간다 — 예전에 실제로 그랬다.)
				researchButton.text = "연구 성좌  ·  지금 " + researchLevel + "단계";

			// 코어 카드 — 개수가 바뀔 때만 다시 그린다(매 프레임 새로 만들면 클릭이 안 먹는다).
			int cardCount = coreCards != null ? coreCards.Count : 0;
			if (cardCount == 0)
			{
				if (coreCardRow.childCount > 0)
					coreCardRow.Clear();
			}
			else if (coreCardRow.childCount != cardCount)
			{
				coreCardRow.Clear();
				for (int index = 0; index < cardCount; index++)
				{
					int cardIndex = index;
					coreCardRow.Add(MakeBoonCard(coreCards[index], () => CoreCardChosen(cardIndex), compact: true));
				}
			}

			// 고를 것이 없으면 줄 자체를 비운다 — 빈 상자가 떠 있으면 그게 더 방해된다.
			int offerCount = perkOffers != null ? perkOffers.Count : 0;
			if (offerCount == 0)
			{
				if (perkRow.childCount > 0)
					perkRow.Clear();
				return;
			}

			if (perkRow.childCount != offerCount || perkKeys.Count != offerCount || PerksChanged(perkOffers))
			{
				perkRow.Clear();
				perkKeys.Clear();
				for (int index = 0; index < offerCount; index++)
				{
					TowerDefenseBuildingPerk perk = perkOffers[index];
					perkKeys.Add(perk);
					Button perkButton = MakeActionButton(TowerDefenseBuildingProgress.NameOf(perk), fontSize: 12,
						() => BuildingPerkChosen(perk));
					perkButton.style.marginRight = 6;
					perkRow.Add(perkButton);
				}
			}
		}

		private readonly System.Collections.Generic.List<TowerDefenseBuildingPerk> perkKeys = new();

		private bool PerksChanged(System.Collections.Generic.IReadOnlyList<TowerDefenseBuildingPerk> offers)
		{
			for (int index = 0; index < offers.Count && index < perkKeys.Count; index++)
			{
				if (offers[index] != perkKeys[index])
					return true;
			}
			return false;
		}

		/// <summary>
		/// 커서가 얹힌 유닛 설명 — 커서를 따라다닌다. 화면 밖으로 새지 않게 가장자리에서 뒤집는다.
		/// 빈 문자열이면 감춘다(가리킬 게 없는데 상자만 떠 있으면 그게 더 방해된다).
		/// </summary>
		private const float TOOLTIP_MIN_WIDTH = 200f;
		private const float TOOLTIP_MIN_HEIGHT = 60f;

		public void ShowUnitTooltip(string text, Vector2 screenPosition)
		{
			if (unitTooltip == null)
				return;

			if (string.IsNullOrEmpty(text))
			{
				unitTooltip.style.display = DisplayStyle.None;
				return;
			}

			unitTooltip.style.display = DisplayStyle.Flex;
			unitTooltipLabel.text = text;

			// 어디에 놓을지는 순수 계산이 정한다 — 커서 없이도 증명할 수 있게 밖으로 꺼내 뒀다.
			// 상자 크기는 *실제로 잡힌 크기*를 쓴다(아직 배치 전이면 최소 크기로 가정).
			const float OFFSET = 18f;
			Vector2 tooltipSize = new Vector2(
				Mathf.Max(unitTooltip.resolvedStyle.width, TOOLTIP_MIN_WIDTH),
				Mathf.Max(unitTooltip.resolvedStyle.height, TOOLTIP_MIN_HEIGHT));
			Vector2 placed = TowerDefenseTooltipPlacement.Resolve(
				screenPosition, new Vector2(Screen.width, Screen.height), tooltipSize, OFFSET);

			unitTooltip.style.left = placed.x;
			unitTooltip.style.top = placed.y;
		}

		private VisualElement BuildUnitTooltip(out Label label)
		{
			VisualElement box = new VisualElement { name = "UnitTooltip" };
			box.style.position = Position.Absolute;
			box.style.paddingLeft = 10;
			box.style.paddingRight = 10;
			box.style.paddingTop = 7;
			box.style.paddingBottom = 7;
			box.style.backgroundColor = new Color(0.04f, 0.05f, 0.08f, 0.92f);
			SetRadius(box, 6);
			box.style.borderLeftWidth = 1;
			box.style.borderRightWidth = 1;
			box.style.borderTopWidth = 1;
			box.style.borderBottomWidth = 1;
			Color border = new Color(1f, 1f, 1f, 0.22f);
			box.style.borderLeftColor = border;
			box.style.borderRightColor = border;
			box.style.borderTopColor = border;
			box.style.borderBottomColor = border;
			box.style.display = DisplayStyle.None;
			box.pickingMode = PickingMode.Ignore; // 툴팁이 클릭을 먹으면 그 자리에 못 짓는다.

			label = new Label(string.Empty);
			label.style.fontSize = 13;
			label.style.color = new Color(0.94f, 0.96f, 1f, 1f);
			label.style.whiteSpace = WhiteSpace.Normal;
			label.pickingMode = PickingMode.Ignore;

			box.Add(label);
			return box;
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
			bannerWrapper = wrapper;

			// ★ 결말은 *한 덩어리*로 읽혀야 한다. 예전엔 제목에만 어두운 판이 깔리고 요약·유물은
			//   맨바닥에 떠서 지도·인형과 겹쳤다 — 판이 끝난 이유를 되짚으라고 만든 글이 정작
			//   가장 안 읽히는 글이 됐다. 제목·요약·유물을 한 장의 카드에 담아 배경과 갈라놓는다.
			//   (버튼은 카드 밖 — 누르는 것과 읽는 것은 다른 일이다.)
			outcomeCard = new VisualElement();
			outcomeCard.style.backgroundColor = new Color(0.04f, 0.05f, 0.08f, 0.88f);
			outcomeCard.style.paddingLeft = 28;
			outcomeCard.style.paddingRight = 28;
			outcomeCard.style.paddingTop = 16;
			outcomeCard.style.paddingBottom = 18;
			outcomeCard.style.alignItems = Align.Center;
			outcomeCard.style.maxWidth = 720;
			SetRadius(outcomeCard, 10);
			outcomeCard.pickingMode = PickingMode.Ignore;

			banner = new Label(string.Empty);
			banner.style.fontSize = 34;
			banner.style.color = new Color(1f, 0.85f, 0.4f, 1f);
			banner.style.unityTextAlign = TextAnchor.MiddleCenter;
			banner.style.whiteSpace = WhiteSpace.Normal;
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

			outcomeCard.Add(banner);
			wrapper.Add(outcomeCard);

			summaryLabel = new Label(string.Empty);
			summaryLabel.style.fontSize = 14;
			summaryLabel.style.color = new Color(0.8f, 0.85f, 0.95f, 1f);
			summaryLabel.style.marginTop = 10;
			summaryLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
			summaryLabel.style.whiteSpace = WhiteSpace.Normal;
			summaryLabel.style.display = DisplayStyle.None;
			summaryLabel.pickingMode = PickingMode.Ignore;
			outcomeCard.Add(summaryLabel);

			outcomeCard.Add(relicLabel);
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
			legendRows.Add(MakeLegendRow(stage.OutpostTint, "전초기지",
				"보급 원점 + 시야 · 대신 마수가 여기로도 온다", TowerDefenseIcon.Kind.Core));
			legendRows.Add(MakeLegendRow(stage.EssenceTint, "정수",
				"바깥 금빛 자리에서만 난다 · 연구·승급 전용", TowerDefenseIcon.Kind.Core));
			legendRows.Add(MakeLegendRow(stage.TrapTint, "함정",
				"밟으면 터진다 · " + stage.TrapCharges + "번 쓰면 사라짐", TowerDefenseIcon.Kind.Burst));
			legendRows.Add(MakeLegendRow(stage.WallTint, "벽",
				"못 지나간다 · 길을 휘게 만든다(완전히 막을 순 없다)", TowerDefenseIcon.Kind.Beam));
			// ★ 연구는 이제 *코어를 골라서* 한다 — 연구 인형은 핫바에서 사라졌는데 범례만 남아
			//   「못 짓는 건물」을 설명하고 있었다. 설명이 규칙보다 오래 남으면 그건 오해를 만든다.
			legendRows.Add(MakeLegendRow(stage.LabTint, "코어 연구",
				"코어를 골라 정수로 올린다 · 단계마다 모든 포탑 피해 +"
					+ Mathf.RoundToInt(stage.LabDamageBonus * 100f) + "%", TowerDefenseIcon.Kind.Ring));
			// ★ 전기는 건물이 멈추고 서는 핵심 규칙인데 범례에 아예 없었다.
			legendRows.Add(MakeLegendRow(stage.GeneratorTint, "발전 인형",
				"범위 안 건물에 전기를 댄다 · 전기가 없으면 그 건물은 서 있기만 한다", TowerDefenseIcon.Kind.Ring));
			// ★ 벌이는 정액이 아니다 — 무는 자리 수와 거리가 정한다(먼 곳일수록 크게 번다).
			//   「정산마다 +N」이라고만 적으면 어디에 세우든 같다는 뜻이 되어, 개척할 이유가 지워진다.
			legendRows.Add(MakeLegendRow(stage.HarvesterTint, "채집 인형",
				"금빛 자리 위에서만 캔다 · 무는 자리가 많고 멀수록 많이 번다(기본 +"
					+ stage.Rules.IncomePerHarvester + ")", TowerDefenseIcon.Kind.Leaf));
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
		/// <summary>
		/// 칸 툴팁 데이터 — 본편 툴팁이 *이미 그릴 줄 아는* 형식(SlotData)으로 넘긴다.
		///
		/// ★ 실패 사례(사용자 실증 "유닛 툴팁 어딨는데"): 포탑 데이터 객체를 그대로 넘겼더니 툴팁 쪽에
		///   그 타입을 그릴 방법이 없어 *조용히 아무것도 안 떴다*. 툴팁은 아는 형식만 그린다.
		/// </summary>
		/// <summary>
		/// 그 유닛의 *실제 모습* — 프리팹의 스프라이트를 그대로 칸에 넣는다(사용자 지시: "아이콘 말고 건물 모습").
		///
		/// ★ 왜 그림을 따로 안 만드나: 아이콘을 새로 그리면 화면의 인형과 칸의 그림이 갈라지고, 인형이
		///   바뀔 때마다 아이콘도 따로 고쳐야 한다. 같은 스프라이트를 쓰면 영원히 어긋나지 않는다.
		/// </summary>
		/// <summary>
		/// 칸에 그릴 그림 — 데이터가 들고 있는 그림이 정본이다.
		///
		/// ★ 예전엔 *프리팹의 SpriteRenderer* 를 뒤져 꺼냈다. 그 그림은 애니메이터가 실행 중에 갈아끼우는
		///   자리라 프리팹 상태에선 비어 있기 일쑤고, 그래서 칸이 빈 상자로 떴다(사용자 실증: "핫바 슬롯
		///   아이콘 뭐 보이지도 않음"). 데이터가 「이게 내 그림」이라고 말하는 자리를 쓴다.
		/// 데이터에 그림이 없으면 프리팹을 뒤지는 옛 길로 물러선다 — 없는 것보다는 낫다.
		/// </summary>
		private static Sprite UnitSprite(Unit unit)
		{
			if (unit == null)
				return null;
			if (unit.Sprite != null)
				return unit.Sprite;
			if (unit.Prefab == null)
				return null;

			SpriteRenderer renderer = unit.Prefab.GetComponentInChildren<SpriteRenderer>(true);
			return renderer != null ? renderer.sprite : null;
		}

		private static SlotData SlotTip(string name, string description)
		{
			SlotData data = new();
			data.SetData(null, name, description);
			return data;
		}

		private static string DescribeTower(TowerDefenseTowerArchetype tower)
		{
			string text = tower.Note + "\n사거리 " + tower.Range.ToString("0.#") + "  ·  피해 " + tower.Damage
				+ "  ·  " + tower.Cooldown.ToString("0.##") + "초마다";
			if (tower.Pierce > 1)
				text += "\n한 발이 " + tower.Pierce + "기를 꿰뚫는다";
			if (tower.SplashRadius > 0f)
				text += "\n착탄 주변 " + tower.SplashRadius.ToString("0.#") + "까지 함께 맞는다";
			if (tower.SlowFactor > 0f)
				text += "\n맞은 마수가 " + (int)(tower.SlowFactor * 100f) + "% 느려진다";
			if (tower.SlowedTargetBonus > 0f)
				text += "\n느려진 마수에겐 +" + (int)(tower.SlowedTargetBonus * 100f) + "% 더 아프게";
			return text;
		}

		/// <summary>
		/// 칸에 적을 값 — *규칙이 실제로 떼는 값*을 매치에 묻는다.
		/// ★ 예전엔 화면이 스테이지 원값을 그대로 적었다. 건설 할인 카드를 고르면 화면은 40 이라
		///   말하는데 지갑에선 34 가 빠졌다 — 화면이 거짓말하면 그 화면을 보고 한 판단이 전부 어긋난다.
		/// 매치가 아직 없으면(첫 그리기) 원값으로 둔다 — 곧 다음 갱신에서 맞춰진다.
		/// </summary>
		private static int CostOf(TowerDefenseMatch match, TowerDefensePlaceableKind kind, int towerIndex, int fallback)
		{
			return match != null ? match.CostOf(kind, towerIndex) : fallback;
		}

		// 값이 바뀌면(할인 카드) 칸을 다시 그린다 — 안 그러면 화면이 옛 값을 계속 말한다.
		private int lastHotbarCostSignature = -1;

		private bool minimapClickBound;

		/// <summary> 펼치는 지도 — 지형·점·범례·설명이 한자리에. </summary>
		private TowerDefenseMapPanel mapPanel;

		/// <summary> 「연구」 버튼 — 코어를 골라 연구 창을 연다(숨은 문을 눈에 보이게). </summary>
		public event System.Action ResearchPanelRequested = delegate { };

		/// <summary> 지도·미니맵을 눌렀다 — 그 자리로 시점을 옮겨 달라(컨트롤러가 카메라를 쥔다). </summary>
		public event System.Action<Vector3> LookAtRequested = delegate { };

		/// <summary> 지도 열고 닫기 — 조작 쪽(M 키·버튼)이 부른다. </summary>
		public void ToggleMap() => mapPanel?.Toggle();

		/// <summary> 지도가 열려 있나 — 열려 있으면 클릭이 배치로 새면 안 된다. </summary>
		public bool IsMapOpen => mapPanel != null && mapPanel.IsOpen;

		private VisualElement menuPanel;

		/// <summary> 메뉴창이 열려 있나. </summary>
		public bool IsMenuOpen => menuPanel != null && menuPanel.style.display == DisplayStyle.Flex;

		/// <summary>
		/// 메뉴창 여닫기 (TASK-WM-200 · 사용자 지시 "ESC로 메뉴창 열리게").
		///
		/// ★ 왜 판을 나가는 문을 여기 두나: 예전엔 취소 키 한 번이 곧 판을 끝냈다 — 되돌릴 수 없는 일이
		///   가장 누르기 쉬운 자리에 있었다. 나가기는 「나가겠다」고 두 번 말해야 하는 자리로 옮긴다.
		/// ★ 손가락에도 이 창이 필요하다: 폰엔 키가 없어서 「멈춤·나가기」로 가는 길이 화면 위밖에 없다.
		/// </summary>
		/// <summary>
		/// 메뉴를 열어/닫아 달라 — *멈춤까지 함께* 다뤄야 하므로 판단은 컨트롤러 한 곳에서 한다
		/// (화면이 직접 열면 「메뉴는 떠 있는데 판은 계속 돈다」가 생긴다).
		/// </summary>
		public event System.Action MenuToggleRequested = delegate { };

		public void SetMenuOpen(bool open)
		{
			EnsureMenuPanel();
			if (menuPanel == null)
				return;
			menuPanel.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
		}

		/// <summary> 메뉴에서 「이어하기」를 눌렀다. </summary>
		public event System.Action MenuResumeRequested = delegate { };

		private void EnsureMenuPanel()
		{
			if (menuPanel != null || container == null)
				return;

			menuPanel = new VisualElement { name = "TowerDefenseMenu" };
			menuPanel.style.position = Position.Absolute;
			menuPanel.style.left = 0;
			menuPanel.style.right = 0;
			menuPanel.style.top = 0;
			menuPanel.style.bottom = 0;
			menuPanel.style.alignItems = Align.Center;
			menuPanel.style.justifyContent = Justify.Center;
			menuPanel.style.backgroundColor = new Color(0.02f, 0.03f, 0.05f, 0.78f);
			menuPanel.style.display = DisplayStyle.None;
			// 메뉴가 떠 있는 동안의 손짓은 메뉴 것이다 — 뒤쪽 땅에 건물이 서면 안 된다.
			menuPanel.pickingMode = PickingMode.Position;

			VisualElement card = new VisualElement();
			card.style.backgroundColor = new Color(0.06f, 0.07f, 0.1f, 0.98f);
			card.style.paddingLeft = 32;
			card.style.paddingRight = 32;
			card.style.paddingTop = 26;
			card.style.paddingBottom = 26;
			card.style.minWidth = 360;
			card.style.alignItems = Align.Stretch;
			SetCardCorners(card, 14);
			menuPanel.Add(card);

			Label title = new Label("잠깐 멈춤");
			title.style.fontSize = 24;
			title.style.color = new Color(0.94f, 0.96f, 0.99f, 1f);
			title.style.unityTextAlign = TextAnchor.MiddleCenter;
			title.style.marginBottom = 20;
			card.Add(title);

			card.Add(MakeMenuButton("이어하기", new Color(0.24f, 0.45f, 0.78f, 1f), () =>
			{
				SetMenuOpen(false);
				MenuResumeRequested();
			}));

			card.Add(MakeMenuButton("판 나가기", new Color(0.5f, 0.22f, 0.24f, 1f), () =>
			{
				SetMenuOpen(false);
				ExitRequested();
			}));

			container.Add(menuPanel);
		}

		private Button MakeMenuButton(string text, Color color, System.Action onClick)
		{
			Button button = new Button(onClick) { text = text };
			button.style.height = 54;
			button.style.fontSize = 18;
			button.style.marginTop = 8;
			button.style.marginBottom = 0;
			button.style.backgroundColor = color;
			button.style.color = new Color(0.97f, 0.98f, 1f, 1f);
			SetCardCorners(button, 10);
			return button;
		}

		private static void SetCardCorners(VisualElement element, float radius)
		{
			element.style.borderTopLeftRadius = radius;
			element.style.borderTopRightRadius = radius;
			element.style.borderBottomLeftRadius = radius;
			element.style.borderBottomRightRadius = radius;
		}

		/// <summary> 지금 무엇을 하려는 중인지 한 줄 — 핫바 바로 위. </summary>
		private static VisualElement BuildArmedBar(out Label label)
		{
			VisualElement wrapper = new VisualElement();
			wrapper.style.position = Position.Absolute;
			wrapper.style.left = 0;
			wrapper.style.right = 0;
			wrapper.style.bottom = ModeSelectionBar.TopFromBottom + 44;
			wrapper.style.alignItems = Align.Center;
			wrapper.pickingMode = PickingMode.Ignore;

			// 첫 프레임에만 보이는 기본 문구 — 어느 장치로 잡든 맞는 말로 둔다(「클릭」은 손가락엔 거짓).
			label = new Label("고르기 — 건물을 고르면 그 건물을 본다");
			label.style.fontSize = 15;
			label.style.color = new Color(0.62f, 0.68f, 0.78f, 1f);
			label.style.backgroundColor = new Color(0.04f, 0.05f, 0.08f, 0.72f);
			label.style.paddingLeft = 14;
			label.style.paddingRight = 14;
			label.style.paddingTop = 4;
			label.style.paddingBottom = 4;
			label.pickingMode = PickingMode.Ignore;
			wrapper.Add(label);
			return wrapper;
		}

		private void FillHotbar(TowerDefenseStageSO stage, TowerDefenseMatch match)
		{
			if (stage == null || match == null)
				return;

			// ★ 칸의 주인은 규칙층이다 — 화면이 자기 손으로 목록을 다시 조립하면 해금이 바뀌는 순간
			//   「보이는 칸」과 「눌리는 칸」이 갈라진다(예전에 정확히 그렇게 돼 있었다).
			System.Collections.Generic.List<ModeSelectionBar.Entry> entries = new();
			foreach (TowerDefenseSlot slot in match.AvailableSlots)
				entries.Add(EntryFor(slot, stage, match));

			selectionBar.SetEntries(entries);
		}

		/// <summary> 칸 하나를 어떻게 그릴까 — 이름·값·색·아이콘·설명. </summary>
		private ModeSelectionBar.Entry EntryFor(TowerDefenseSlot slot, TowerDefenseStageSO stage, TowerDefenseMatch match)
		{
			int cost = match.CostOf(slot.Kind, slot.TowerIndex);
			switch (slot.Kind)
			{
				case TowerDefensePlaceableKind.Harvester:
					return new ModeSelectionBar.Entry("채집 인형", cost, stage.HarvesterTint,
						icon: UnitSprite(stage.HarvesterUnit),
						tooltip: SlotTip("채집 인형", "자원 노드 위에만 선다. 코어까지 보급이 이어져야 수입이 들어온다.\n바깥 노드는 정수를 낸다."));
				case TowerDefensePlaceableKind.Wall:
					return new ModeSelectionBar.Entry("벽", cost, stage.WallTint,
						tooltip: SlotTip("벽", "마수의 길을 휘게 한다. 길을 완전히 막는 자리에는 못 세운다."));
				case TowerDefensePlaceableKind.Trap:
					return new ModeSelectionBar.Entry("함정", cost, stage.TrapTint,
						tooltip: SlotTip("함정", "바닥에 깐다. 밟으면 터지고 횟수를 다 쓰면 사라진다."));
				case TowerDefensePlaceableKind.Outpost:
					return new ModeSelectionBar.Entry("전초기지", cost, stage.OutpostTint,
						tooltip: SlotTip("전초기지", "정수로 짓는다. 새 보급 원점이자 *새로 지켜야 할 곳*이 된다 — 마수가 그리로도 몰린다."));
				case TowerDefensePlaceableKind.Generator:
					return new ModeSelectionBar.Entry("발전 인형", cost, stage.GeneratorTint,
						icon: UnitSprite(stage.HarvesterUnit),
						tooltip: SlotTip("발전 인형", "범위 안 건물에 전기를 댄다. 전기를 못 받는 건물은 서 있기만 한다.\n코어도 처음부터 얼마간 대준다."));
				case TowerDefensePlaceableKind.Hero:
					// ★ 지금 이 칸은 목록에 안 나온다 — 영웅은 칸에서 뺐고(사용자 지시) 빈 땅을 눌러 보낸다.
					//   그런데 설명은 「고르고 땅을 찍으면」이라는 *없어진 조작*을 그대로 말하고 있었다.
					//   가지를 지우지는 않는다(칸이 돌아오면 기본 가지로 떨어져 포탑처럼 그려진다) —
					//   대신 지금 규칙과 같은 말을 하게 둔다.
					return new ModeSelectionBar.Entry("영웅 부르기", 0, stage.HeroTint,
						icon: UnitSprite(stage.HeroUnit),
						tooltip: SlotTip("영웅 부르기", "짓는 게 아니라 보내는 칸이다. 지금은 이 칸 없이도 빈 땅을 누르면 영웅이 그리로 걸어간다."));
				default:
					TowerDefenseTowerArchetype tower = stage.TowerArchetypes != null && slot.TowerIndex < stage.TowerArchetypes.Length
						? stage.TowerArchetypes[slot.TowerIndex]
						: null;
					return tower != null
						? new ModeSelectionBar.Entry(tower.DisplayName, cost, tower.Tint,
							icon: UnitSprite(stage.TowerUnit), tooltip: SlotTip(tower.DisplayName, DescribeTower(tower)))
						: new ModeSelectionBar.Entry("포탑 인형", cost, stage.TowerTint,
							icon: UnitSprite(stage.TowerUnit), tooltip: SlotTip("포탑 인형", "사거리 안의 마수를 쏜다."));
			}
		}

		/// <summary> 고른 칸 표시 — 공용 바가 그린다. </summary>
		public void SetSelectedSlot(int selectedIndex)
		{
			selectionBar.SetSelected(selectedIndex);
		}

		/// <summary>
		/// 지금 설치 대기인가를 화면에 박는다 — 클릭 한 번의 뜻이 여기서 갈린다.
		/// </summary>
		public void SetArmed(bool armed, string what)
		{
			if (armedLabel == null)
				return;

			lastArmed = armed;
			lastArmedWhat = what;

			armedLabel.text = IsTouch
				? (armed
					? "설치 대기 — " + what + " · 자리를 톡 → 한 번 더 톡 하면 지어진다 · 「창」에서 취소"
					: "고르기 — 건물 톡 = 살펴보기 · 빈 땅 톡 = 영웅 보내기")
				: (armed
					// ★ 무르는 법을 여기 적는다 (사용자 실측: "짓기 취소는 어케함? 할 구가 없네").
					//   취소가 되는데 *어디에도 안 적혀 있으면* 없는 기능이다 — 설치 대기 줄이 그 말을 할 자리다.
					? "설치 대기 — " + what + " · 좌클릭 설치 · 우클릭 판매 · ESC 취소"
					: "고르기 — 건물 클릭 = 살펴보기 · 빈 땅 우클릭 = 영웅 보내기");
			armedLabel.style.color = armed
				? new Color(1f, 0.82f, 0.35f, 1f)
				: new Color(0.62f, 0.68f, 0.78f, 1f);
		}

		private Label armedLabel;

		// TASK-WM-200 — 조작 안내는 *지금 쥔 장치*를 말해야 한다. 손가락으로 하는데 「우클릭」이라고
		// 적혀 있으면 그 줄은 안내가 아니라 거짓말이고, 화면 전체의 신뢰가 같이 떨어진다.
		private bool lastArmed;
		private string lastArmedWhat = string.Empty;
		private TowerDefenseStageSO lastHintStage;
		private bool lastTouchMode;

		private static bool IsTouch =>
			InputManager.TryGetExistingInstance(out InputManager inputManager) && inputManager.IsTouchMode;

		/// <summary> 장치가 바뀌면 안내를 다시 쓴다 — 마우스를 놓고 손가락을 대는 순간 문구가 따라와야 한다. </summary>
		private void RefreshDeviceHints()
		{
			if (lastTouchMode == IsTouch)
				return;

			lastTouchMode = IsTouch;
			SetArmed(lastArmed, lastArmedWhat);
			ApplyHintText(lastHintStage);
		}

		private void ApplyHintText(TowerDefenseStageSO stage)
		{
			lastHintStage = stage;
			if (hintLabel == null)
				return;

			if (stage == null)
			{
				hintLabel.text = string.Empty;
				return;
			}

			hintLabel.text = IsTouch
				? "아래 칸 톡 = 고르기 · 자리 톡 → 한 번 더 톡 = 짓기   ·   빈 땅 톡 = 영웅 보내기   ·   코어 톡 = 연구"
					+ "   ·   끌기 = 시점 · 오므리기 = 확대·축소 · 두 손가락 비틀기 = 회전   ·   「지도」·「창」 단추"
				// ★ 안내가 실제 키와 달랐다: 배속은 Tab 이 아니라 F6 이고, 정작 제일 많이 쓰는
				//   「숫자키로 칸 고르기」는 한 글자도 없었다. 조작 안내가 틀리면 그 화면 전체를 못 믿는다.
				// ★ 「연구를 어떻게 여는지」가 어디에도 없었다(사용자 실증: "연구 어케 여는데").
				//   찾아야만 알 수 있는 기능은 *없는 기능*이다 — 첫 판의 유일한 다음 수라서 더 그렇다.
				: "숫자키 1~9 칸 고르기 · 좌클릭 설치 · 설치 중 우클릭 = 판매   ·   평소 우클릭 = 영웅 보내기   ·   코어 클릭 = 연구"
					+ "   ·   Space 멈춤 · F6 배속   ·   WASD 시점 · 휠 확대·축소   ·   M 지도   ·   ESC 취소·메뉴";
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
			FillHotbar(stage, null);
			SetBannerVisible(false);

			// ★ 「지금 설치 모드인가」가 화면 어디에도 없었다(사용자 실증: "확실히 지금이 설치 모드라는
			//   걸 알려줘야함"). 칸을 골랐는지 아닌지로 클릭의 뜻이 통째로 달라지는데, 그걸 손이
			//   기억해야 했다. 안내 줄 맨 앞에 상태를 박는다 — 색까지 바꿔 곁눈으로도 읽히게.
			ApplyHintText(stage);
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
			Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude);
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
			RefreshDeviceHints();

			if (match == null)
				return;

			// 값이 바뀌었으면(건설 할인 카드) 칸을 다시 그린다 — 화면이 옛 값을 계속 말하면 안 된다.
			// ★ 값뿐 아니라 *칸 수*도 서명에 넣는다 — 연구로 새 칸이 열렸는데 값이 그대로면
			//   화면이 옛 목록을 계속 그린다(해금이 눈에 안 보이면 연구를 왜 했는지 알 수 없다).
			int costSignature = match.CostOf(TowerDefensePlaceableKind.Tower)
				+ match.CostOf(TowerDefensePlaceableKind.Harvester) * 1000
				+ match.AvailableSlots.Count * 1000000;
			if (costSignature != lastHotbarCostSignature)
			{
				lastHotbarCostSignature = costSignature;
				FillHotbar(stage, match);
			}

			resourceValue.text = match.Resource.ToString();

			bool endless = stage == null || stage.Rules.IsEndless;
			waveValue.text = endless
				? (match.WaveIndex + 1).ToString()
				: (match.WaveIndex + 1) + " / " + stage.Rules.WaveCount;

			bool preparing = match.Phase == TowerDefensePhase.Prepare;

			// ★ 실시간에는 「국면」이 없다 — 이 자리가 말해야 할 것은 *지금 판이 어떤 상태인가*다.
			//   압력(시간이 올린 마수 강도)과 적응(내가 한 수단에 기댄 결과)은 판을 바꾸는데도 화면 어디에도
			//   안 나와 있었다 — 안 보이는 규칙은 없는 규칙이다(개선 목록 23번).
			string pressureText = "마수 강도 x" + match.Pressure.ToString("0.0");
			string adaptationText = TowerDefenseAdaptation.Describe(match.Adaptation);
			string heroText = match.HeroRespawnIn > 0f
				? "  ·  영웅 " + Mathf.CeilToInt(match.HeroRespawnIn) + "초 뒤 복귀"
				: string.Empty;

			phaseValue.text = pressureText
				+ (string.IsNullOrEmpty(adaptationText) ? string.Empty : "  ·  " + adaptationText)
				+ heroText;

			// 실시간에는 국면이 없다 — 늘 교전 중이라 이 조건은 항상 참이었다(페이즈제 잔재).
			enemyValue.text = match.AliveEnemyCount.ToString();

			// 「기본 + 채집 N기」로 쪼개 보여준다 — 총액만 보이면 그 숫자가 어디서 왔는지 알 수 없다.
			// 끊긴 채집이 있으면 그 사실이 수입 옆에 붙어야 한다 — 안 그러면 「왜 수입이 줄었지」가 미스터리가 된다.
			string supplyNote = match.DisconnectedHarvesters > 0
				? "  ⚠ 보급 끊김 " + match.DisconnectedHarvesters
				: string.Empty;

			// ★ 「채집 N기」는 *실제로 버는* 수여야 한다. 지은 수를 말하면 다섯 채 중 둘만 일해도
			//   다섯이라 하고, 그러면 「왜 수입이 이것밖에 안 되지」가 영영 안 풀린다.
			//   지은 수와 다르면 둘 다 보여준다 — 「몇 채가 놀고 있나」가 곧 다음에 할 일이다.
			string harvesterNote = match.HarvesterCount > 0
				? (match.WorkingHarvesters == match.HarvesterCount
					? " (기본 " + stage.Rules.BaseWaveIncome + " + 채집 " + match.WorkingHarvesters + "기)"
					: " (기본 " + stage.Rules.BaseWaveIncome + " + 채집 " + match.WorkingHarvesters + "/" + match.HarvesterCount + "기)")
				: string.Empty;
			incomeValue.text = match.NextWaveIncome + harvesterNote;
			incomeValue.text += supplyNote;

			livesValue.text = match.UsesLives ? match.Lives.ToString() : "-";
			essenceValue.text = match.NextWaveEssence > 0
				? match.Essence + " (+" + match.NextWaveEssence + ")"
				: match.Essence.ToString();
			nextWaveValue.text = BuildWavePreview(match);
			UpdateNodeLabels(match, stage);
			UpdateDollLabels(match);
			minimap?.Tick(match, stage);
			minimap?.RefreshTerrain(match.MapLayout, stage); // 작은 지도에도 땅이 보여야 한다.
			if (minimapClickBound == false && match != null)
			{
				minimap?.EnableClickToLook(match);
				minimapClickBound = true;
			}
			mapPanel?.Tick(match, stage);

			string boonSummary = match.BoonSummary;
			boonSummaryLabel.text = boonSummary;
			boonSummaryLabel.style.display = string.IsNullOrEmpty(boonSummary) ? DisplayStyle.None : DisplayStyle.Flex;

			// ★ 판이 끝나면 조작 손잡이도 같이 죽어야 한다. 라이브에서 봤다 — 결말 화면이 떠 있는데
			//   멈춤·배속·진행·핫바가 전부 눌릴 것처럼 살아 있었다. 눌러도 아무 일이 없는 스위치는
			//   「안 되는구나」를 알려주는 대신 *바꿨다고 믿게* 만든다(이 판에서 이미 한 번 당한 병이다).
			bool over = match.Outcome != TowerDefenseOutcome.InProgress;
			selectionBar.Root.SetEnabled(over == false);
			waveModeButton.SetEnabled(over == false);

			bool paused = match.SpeedScale <= 0f;
			pauseButton.text = paused ? "▶ 재개" : "⏸ 멈춤";
			pauseButton.SetEnabled(over == false);
			speedButton.text = "배속 ×" + Mathf.Max(1f, match.SpeedScale).ToString("0");
			speedButton.SetEnabled(over == false && paused == false);

			waveModeButton.text = match.AutoAdvanceWaves ? "진행: 자동" : "진행: 수동";
			if (difficultyButton != null)
				difficultyButton.text = "난이도: " + TowerDefenseDifficulty.NameOf(match.Difficulty) + " (다음 판)";
			// ★ 실시간에는 「건설 국면」이 없다 — 그 조건에 걸어두면 이 버튼이 *영원히* 안 눌린다.
			//   규칙은 언제든 부를 수 있게 돼 있는데(RequestNextWave) 화면만 막고 있었다.
			//   기다리는 것이 벌칙이 되지 않게 하려고 넣은 손잡이가, 정작 한 번도 못 쓰였다.
			//   이미 예약했으면 다시 못 누르게 한다 — 예약은 1회성이다.
			nextWaveButton.SetEnabled(match.Outcome == TowerDefenseOutcome.InProgress
				&& match.IsNextWaveRequested == false);
			nextWaveButton.text = match.IsNextWaveRequested ? "곧 온다" : "다음 웨이브 ▶";
		}

		/// <summary>
		/// 매치 종료 배너. 무한 모드 패배 = 버틴 웨이브 수가 곧 점수 —
		/// 기록 갱신 여부까지 말해야 「다시 도전」이 이유를 갖는다.
		/// </summary>
		public void ShowOutcome(TowerDefenseOutcome outcome, int survivedSeconds, int nestsDestroyed, int score, int best,
			bool isNewRecord, int relicsGained, int relicBalance, bool canPull, string summary = null)
		{
			SetBannerVisible(true);
			SetBestRecord(best);
			ShowRelicResult(relicsGained, relicBalance, canPull);

			// 문구 규칙은 화면 밖(TowerDefenseOutcomeText)에 있다 — 화면 없이도 시험할 수 있어야
			// 「무슨 일이 일어났는지 제대로 말하나」를 자동으로 물을 수 있다.
			bannerLabel.text = TowerDefenseOutcomeText.Build(
				outcome, FormatDuration(survivedSeconds), nestsDestroyed, score, best, isNewRecord);
			// ★ 이겼을 때도 요약은 붙어야 한다 — 예전엔 여기서 빠져나가 이긴 판을 되짚을 수단이 없었다.
			ShowSummary(summary);
		}

		/// <summary>
		/// 판 요약 — 「왜 졌는지」를 되짚을 유일한 자리. 없으면 매 판이 같은 실수의 반복이 된다.
		/// 배너 아래에 조용히 붙인다(결말 문구를 밀어내지 않게).
		/// </summary>
		private void ShowSummary(string summary)
		{
			if (summaryLabel == null)
				return;

			summaryLabel.text = summary ?? string.Empty;
			summaryLabel.style.display = string.IsNullOrEmpty(summary) ? DisplayStyle.None : DisplayStyle.Flex;
		}

		private Label summaryLabel;

		/// <summary> 결말 한 덩어리 — 제목·요약·유물이 같은 판 위에 앉는다(버튼은 밖). </summary>
		private VisualElement outcomeCard;

		/// <summary> 결말 화면 껍데기 — 보임/숨김의 주인(안쪽 구조와 무관하게 이것만 토글한다). </summary>
		private VisualElement bannerWrapper;

		/// <summary> 초 → 「3분 20초」. 숫자만 던지면 몇 분인지 사람이 암산해야 한다. </summary>
		private static string FormatDuration(int seconds)
		{
			if (seconds < 60)
				return seconds + "초";
			return seconds / 60 + "분 " + seconds % 60 + "초";
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

		/// <summary> 최고 기록 — 점수(초 환산). 기록 없으면 「-」. </summary>
		public void SetBestRecord(int bestScore)
		{
			bestValue.text = bestScore > 0 ? FormatDuration(bestScore) : "-";
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

		/// <summary> 「다음 웨이브: 돌진 3 · 방패 1」 — 매치가 실제 스폰에 쓰는 그 계산을 그대로 부른다. </summary>
		private string BuildWavePreview(TowerDefenseMatch match)
		{
			int previewWave = match.Phase == TowerDefensePhase.Assault ? match.WaveIndex + 1 : match.WaveIndex;
			match.ComposeWave(previewWave, compositionBuffer);

			// 웨이브 성격은 색까지 바꿔 예고한다 — 「무엇이 오는가」를 한눈에 알아야 대비가 성립한다.
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
			System.Collections.Generic.IReadOnlyList<Vector3> nodes = match.ActiveResourceNodeLocalPositions;
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

		/// <summary>
		/// 인형 머리 위 이름표 — 「광역 포탑」이 아니라 「비올라」가 서 있어야 판다·잃는다에 무게가 생긴다.
		/// 안 밝힌 자리는 띄우지 않는다(시야 밖의 것을 화면이 알려주면 시야가 무의미해진다).
		/// </summary>
		private void UpdateDollLabels(TowerDefenseMatch match)
		{
			System.Collections.Generic.IReadOnlyList<TowerDefenseDollLabel> dolls = match.DollLabels;
			Camera camera = ViewCameraResolver.Current;

			while (dollLabelViews.Count < dolls.Count)
			{
				Label label = new Label(string.Empty);
				label.style.position = Position.Absolute;
				label.style.fontSize = 12;
				label.style.unityTextAlign = TextAnchor.MiddleCenter;
				label.pickingMode = PickingMode.Ignore;
				worldLabelLayer.Add(label);
				dollLabelViews.Add(label);

				VisualElement bar = TowerDefenseProgressBar.Create();
				worldLabelLayer.Add(bar);
				dollBarViews.Add(bar);
			}

			for (int index = 0; index < dollLabelViews.Count; index++)
			{
				Label label = dollLabelViews[index];
				VisualElement bar = dollBarViews[index];
				if (index >= dolls.Count || camera == null)
				{
					label.style.display = DisplayStyle.None;
					bar.style.display = DisplayStyle.None;
					continue;
				}

				TowerDefenseDollLabel doll = dolls[index];
				Vector3 screenPosition = camera.WorldToScreenPoint(doll.Anchor.position);
				if (screenPosition.z <= 0f || match.IsExploredAt(doll.Anchor.position) == false)
				{
					label.style.display = DisplayStyle.None;
					bar.style.display = DisplayStyle.None;
					continue;
				}

				label.style.display = DisplayStyle.Flex;
				label.text = doll.Text;
				label.style.color = doll.Tint;
				label.style.left = screenPosition.x - 40f;
				label.style.top = Screen.height - screenPosition.y + 12f;
				label.style.width = 80;

				bar.style.display = DisplayStyle.Flex;
				bar.style.left = screenPosition.x - TowerDefenseProgressBar.WIDTH * 0.5f;
				bar.style.top = Screen.height - screenPosition.y + 4f;
				TowerDefenseProgressBar.SetRatio(bar, doll.ReadyRatio, doll.Working);
			}
		}

		private void SetBannerVisible(bool visible)
		{
			// ★ 「제목의 부모」로 잡으면 안 된다 — 실제로 그렇게 짜여 있다가, 제목·요약을 카드 한 장에
			//   묶는 순간 부모가 카드로 바뀌어 *바깥 껍데기가 영영 숨은 채*로 남았다(결말 화면이 통째로
			//   안 떴다). 껍데기는 이름으로 잡는다 — 안쪽 구조를 바꿔도 안 흔들린다.
			if (bannerWrapper != null)
				bannerWrapper.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
		}
	}
}
