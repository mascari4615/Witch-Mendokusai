using UnityEngine;
using Vector2 = WitchMendokusai.Numerics.Vector2;
// ★ 좌표는 판정 쪽 (TASK-WM-214) — 엔진으로 나갈 땐 자동, 엔진에서 받을 땐 캐스트.
using Vector3 = WitchMendokusai.Numerics.Vector3;
using Vector2Int = WitchMendokusai.Numerics.Vector2Int;
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
	public partial class TowerDefenseHudView
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
		private readonly Label livesTopValue; // 위 띠의 목숨 — 판이 도는 동안 보이는 셋 중 하나.

		// 예고 계산 버퍼 — 매 프레임 새 리스트를 만들지 않는다.
		private readonly System.Collections.Generic.List<int> compositionBuffer = new();
		private int[] archetypeCountBuffer = System.Array.Empty<int>();
		private Button pullButton;
		private Button pauseButton;
		private Button rangeDebugButton;
		private Button uiScaleButton;
		private Button difficultyButton;
		private Button speedButton;
		// 자원 노드 위에 붙는 벌이 배수 표. 월드 좌표를 매 프레임 화면으로 투영해 따라붙인다
		// (UI Toolkit 은 월드 공간 텍스트가 없고, 폰트 에셋을 새로 들이지 않기 위한 선택).
		private readonly VisualElement worldLabelLayer;
		private readonly System.Collections.Generic.List<Label> worldLabels = new();
		// 건물 머리 위 작은 바 — 이름표와 같은 좌표계라 같은 목록으로 관리한다(따로 두면 어긋난다).
		private readonly System.Collections.Generic.List<VisualElement> dollBarViews = new();
		private Button researchButton;
		private VisualElement perkRow;
		private VisualElement coreCardRow;
		private readonly Button waveModeButton;
		private readonly Button nextWaveButton;

		// 본편 UI 복원용 — 숨기기 전 값을 보관(무조건 되돌리면 원래 숨김 상태였던 경우를 깨뜨린다).
		private DisplayStyle baseHudPreviousDisplay = DisplayStyle.Flex;
		private DisplayStyle baseWindowsPreviousDisplay = DisplayStyle.Flex;
		private readonly System.Collections.Generic.List<Canvas> hiddenCanvases = new();

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

		private Button sellButton;

		/// <summary> 코어를 고른 채 「연구」를 눌렀다. </summary>

		/// <summary> UI 배율을 한 단계 돌린다. </summary>
		public event System.Action UiScaleCycleRequested = delegate { };

		/// <summary> 고른 건물의 레벨업 선택지를 골랐다. </summary>
		public event System.Action<TowerDefenseBuildingPerk> BuildingPerkChosen = delegate { };

		/// <summary> 코어 레벨업 카드를 골랐다(판 전체에 걸린다). </summary>
		public event System.Action<int> CoreCardChosen = delegate { };

		// ★ 글자 크기 사다리 — 세 단이 전부다. 조각마다 숫자를 박으면 기기가 바뀔 때 저마다 다르게
		//   어긋난다(실측: 개척 HUD 에 고정 픽셀이 53곳, 비율은 7곳뿐이었다). 늘리고 줄이는 손잡이를
		//   하나로 모아야 「해상도 바뀌면 다 같이」가 성립한다.
		public const int TEXT_SMALL = 12;
		public const int TEXT_BODY = 16;
		public const int TEXT_TITLE = 22;

		/// <summary> 화면 가장자리에서 띄우는 기본 여백 — 띠들이 공유한다(각자 박지 않는다). </summary>
		public const int EDGE_GAP = 16;

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

			// ★ 안전영역 — 노치·둥근 모서리·제스처 바가 먹는 자리를 루트에서 한 번에 비켜준다.
			//   각 조각이 저마다 여백을 박으면 기기마다 어긋난다(지금까지 그랬다). 비켜주는 곳은 여기 한 곳.
			ApplySafeArea();
			// ★ 한 번만 재면 창 크기·회전이 바뀐 뒤로는 계속 옛 값이다. 자리가 다시 잡힐 때마다 다시 잰다 —
			//   해상도가 바뀌어도 「띠가 늘어날 뿐」이 되려면 이 되풀이가 있어야 한다.
			container.RegisterCallback<GeometryChangedEvent>(_ => ApplySafeArea());

			// ★ 한 덩어리가 한 가지만 말한다 — 예전엔 전부 좌상단에 몰려 있어 무엇부터 봐야 할지 알 수 없었다
			//   (사용자 실증: "정보는 전부 모여 있어서 복잡복잡"). 자원은 상단 가운데 독립 띠(종류가 늘어도
			//   가로로 칸만 추가), 진행은 우상단, 범례는 좌하단 접기, 고르는 것은 하단 가운데.
			container.Add(Named(BuildResourceBar(out resourceValue, out incomeValue, out essenceValue, out livesTopValue), "ResourceBar"));
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

		private readonly System.Collections.Generic.List<TowerDefenseBuildingPerk> perkKeys = new();

		/// <summary> 「연구」 버튼 — 코어를 골라 연구 창을 연다(숨은 문을 눈에 보이게). </summary>
		public event System.Action ResearchPanelRequested = delegate { };

		/// <summary> 지도·미니맵을 눌렀다 — 그 자리로 시점을 옮겨 달라(컨트롤러가 카메라를 쥔다). </summary>
		public event System.Action<Vector3> LookAtRequested = delegate { };
		private bool lastTouchMode;

		private static bool IsTouch =>
			InputManager.TryGetExistingInstance(out InputManager inputManager) && inputManager.IsTouchMode;


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
			livesTopValue.text = livesValue.text; // 같은 값, 보이는 자리만 다르다.
			essenceValue.text = match.NextWaveEssence > 0
				? match.Essence + " (+" + match.NextWaveEssence + ")"
				: match.Essence.ToString();
			nextWaveValue.text = BuildWavePreview(match);
			// ★ 월드에 붙는 것(이름표·체력바·광맥 배수)은 여기서 그리지 않는다 — TickWorldAnchored 가
			//   *카메라가 움직인 뒤에* 그린다. 아래 주석 참고.
			worldTickMatch = match;
			worldTickStage = stage;
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
		/// 자원 노드마다 「×1.9」 벌이 배수를 띄운다 — 노드가 다 똑같아 보이면 「어디로 넓힐까」가 판단이 안 된다.
		/// 화면 밖으로 나간 노드는 감춘다(뒤쪽 노드가 화면 가장자리에 눌어붙는 것 방지).
		/// </summary>
		private TowerDefenseMatch worldTickMatch;
		private TowerDefenseStageSO worldTickStage;
	}
}
