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

		private readonly System.Collections.Generic.List<TowerDefenseBuildingPerk> perkKeys = new();

		/// <summary> 「연구」 버튼 — 코어를 골라 연구 창을 연다(숨은 문을 눈에 보이게). </summary>
		public event System.Action ResearchPanelRequested = delegate { };

		/// <summary> 지도·미니맵을 눌렀다 — 그 자리로 시점을 옮겨 달라(컨트롤러가 카메라를 쥔다). </summary>
		public event System.Action<Vector3> LookAtRequested = delegate { };
		private bool lastTouchMode;

		private static bool IsTouch =>
			InputManager.TryGetExistingInstance(out InputManager inputManager) && inputManager.IsTouchMode;
	}
}
