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
		private readonly Label hintLabel;
		private readonly Label bannerLabel;
		private readonly VisualElement legendPanel;
		private readonly VisualElement hotbarPanel;
		private readonly Button waveModeButton;
		private readonly Button nextWaveButton;
		private readonly System.Collections.Generic.List<VisualElement> hotbarSlots = new();

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

			container.Add(BuildStatPanel(out resourceValue, out waveValue, out phaseValue, out enemyValue, out bestValue));
			container.Add(BuildWaveControlPanel(out waveModeButton, out nextWaveButton));
			legendPanel = BuildLegendPanel();
			container.Add(legendPanel);
			hotbarPanel = BuildHotbar();
			container.Add(hotbarPanel);
			container.Add(BuildHintBar(out hintLabel));
			container.Add(BuildBanner(out bannerLabel, out _));
			container.Add(BuildCornerRestartButton());

			// 본편 HUD(HudLayer)를 숨겨도 개척 HUD 는 살아있어야 하므로 한 단 위 레이어에 붙인다.
			uiRoot.OverlayLayer.Add(container);
		}

		// 좌상단 컴팩트 스탯 — 폭을 내용에 맞춰 좁게(전폭 바 금지).
		private static VisualElement BuildStatPanel(out Label resource, out Label wave, out Label phase, out Label enemies, out Label bestValue)
		{
			VisualElement panel = new VisualElement { name = "StatPanel" };
			panel.style.position = Position.Absolute;
			panel.style.left = 24;
			panel.style.top = 24;
			panel.style.paddingLeft = 14;
			panel.style.paddingRight = 18;
			panel.style.paddingTop = 10;
			panel.style.paddingBottom = 10;
			panel.style.backgroundColor = new Color(0.04f, 0.05f, 0.08f, 0.72f);
			panel.style.borderTopLeftRadius = 6;
			panel.style.borderTopRightRadius = 6;
			panel.style.borderBottomLeftRadius = 6;
			panel.style.borderBottomRightRadius = 6;
			panel.pickingMode = PickingMode.Ignore;

			panel.Add(MakeStatRow("자원", out resource, new Color(1f, 0.86f, 0.35f, 1f)));
			panel.Add(MakeStatRow("웨이브", out wave, new Color(1f, 0.6f, 0.55f, 1f)));
			panel.Add(MakeStatRow("상태", out phase, new Color(0.72f, 0.88f, 1f, 1f)));
			// ★ 「남은 마수」가 없으면 웨이브가 끝났는지를 눈으로만 판단해야 한다 — 마수 한 마리가 코어에
			//   겹쳐 서 있으면 화면에서 사라져 "다 잡았는데 안 넘어간다"가 된다(사용자 실증). 숫자가 진실을 말한다.
			panel.Add(MakeStatRow("남은 마수", out enemies, new Color(1f, 0.45f, 0.42f, 1f)));
			// ★ 무한 모드엔 「클리어」가 없다 — 넘어야 할 선(지난 최고 기록)이 화면에 있어야 이번 판에 목표가 생긴다.
			panel.Add(MakeStatRow("최고 기록", out bestValue, new Color(0.78f, 0.82f, 0.92f, 1f)));
			return panel;
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
			captionLabel.style.width = 68;
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
			wrapper.style.bottom = 26;
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
		/// 웨이브 진행 조작 — 스탯 패널 바로 아래. 「자동/수동」 전환과 「다음 웨이브」 호출.
		/// 자동만 있으면 준비할 시간을 시계가 뺏고, 수동만 있으면 리듬이 사라진다. 둘을 화면에서 바로 바꾼다.
		/// </summary>
		private VisualElement BuildWaveControlPanel(out Button modeButton, out Button callButton)
		{
			VisualElement panel = new VisualElement { name = "WaveControlPanel" };
			panel.style.position = Position.Absolute;
			panel.style.left = 24;
			panel.style.top = 152;
			panel.style.flexDirection = FlexDirection.Row;
			panel.pickingMode = PickingMode.Ignore;

			modeButton = MakeActionButton(string.Empty, fontSize: 12, () => WaveModeToggleRequested());
			modeButton.style.marginRight = 6;

			callButton = MakeActionButton("다음 웨이브 ▶", fontSize: 12, () => NextWaveRequested());

			panel.Add(modeButton);
			panel.Add(callButton);
			return panel;
		}

		/// <summary>
		/// 우상단 「처음부터」 — 항상 보이는 재시작. 패배 배너를 기다리지 않아도 언제든 판을 버릴 수 있어야
		/// 시행착오가 빨라진다(작은 사이클을 반복해 다듬는 개발 방향과 정합).
		/// </summary>
		private VisualElement BuildCornerRestartButton()
		{
			VisualElement wrapper = new VisualElement();
			wrapper.style.position = Position.Absolute;
			wrapper.style.right = 24;
			wrapper.style.top = 24;
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

			// 끝났는데 다음 행동이 화면에 없으면 게임이 아니라 정지 화면이 된다 — 배너 바로 아래 재시작.
			restartButton = MakeActionButton("다시 도전", fontSize: 18, () => RestartRequested());
			restartButton.style.marginTop = 14;

			wrapper.Add(banner);
			wrapper.Add(restartButton);
			return wrapper;
		}


		/// <summary>
		/// 범례 — **화면이 스스로 설명해야 한다**(사용자 지시: "구분이 어떻게 됐는지에 대한 안내가 화면에
		/// 표시되어야겠지"). 아트가 아직 없어 색이 곧 정체이므로, 색 견본 + 이름을 그대로 띄운다.
		/// 색은 스테이지 SO 를 읽어 채운다 — 화면의 유닛 색과 범례 색이 같은 소스여야 안내가 거짓말을 안 한다.
		/// </summary>
		private VisualElement BuildLegendPanel()
		{
			VisualElement panel = new VisualElement { name = "LegendPanel" };
			panel.style.position = Position.Absolute;
			panel.style.left = 24;
			panel.style.top = 202;
			panel.style.paddingLeft = 12;
			panel.style.paddingRight = 16;
			panel.style.paddingTop = 8;
			panel.style.paddingBottom = 8;
			panel.style.backgroundColor = new Color(0.04f, 0.05f, 0.08f, 0.66f);
			panel.style.borderTopLeftRadius = 6;
			panel.style.borderTopRightRadius = 6;
			panel.style.borderBottomLeftRadius = 6;
			panel.style.borderBottomRightRadius = 6;
			panel.pickingMode = PickingMode.Ignore;
			return panel;
		}

		// 스테이지가 정해지는 시점(진입)에 채운다 — 색 출처가 SO 라 하드코딩 색이 없다.
		private void FillLegend(TowerDefenseStageSO stage)
		{
			legendPanel.Clear();
			if (stage == null)
				return;

			legendPanel.Add(MakeLegendRow(stage.CoreTint, "코어", "부서지면 끝"));
			legendPanel.Add(MakeLegendRow(stage.TowerTint, "포탑 인형", "적을 쏜다"));
			legendPanel.Add(MakeLegendRow(stage.HarvesterTint, "채집 인형", "수입 +" + stage.Rules.IncomePerHarvester));
			legendPanel.Add(MakeLegendRow(stage.EnemyTint, "마수", "코어로 전진"));
			legendPanel.Add(MakeLegendRow(new Color(1f, 0.82f, 0.25f, 1f), "금빛 원반", "채집 인형 자리"));
			legendPanel.Add(MakeLegendRow(stage.EnemyTint, "붉은 판", "마수 출현"));
		}

		private static VisualElement MakeLegendRow(Color swatchColor, string name, string note)
		{
			VisualElement row = new VisualElement();
			row.style.flexDirection = FlexDirection.Row;
			row.style.alignItems = Align.Center;
			row.style.marginBottom = 3;
			row.pickingMode = PickingMode.Ignore;

			VisualElement swatch = new VisualElement();
			swatch.style.width = 12;
			swatch.style.height = 12;
			swatch.style.marginRight = 8;
			swatch.style.backgroundColor = swatchColor;
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
			noteLabel.style.color = new Color(0.62f, 0.68f, 0.76f, 1f);
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
		private VisualElement BuildHotbar()
		{
			VisualElement wrapper = new VisualElement { name = "PlaceHotbar" };
			wrapper.style.position = Position.Absolute;
			wrapper.style.left = 0;
			wrapper.style.right = 0;
			wrapper.style.bottom = 64;
			wrapper.style.flexDirection = FlexDirection.Row;
			wrapper.style.justifyContent = Justify.Center;
			wrapper.pickingMode = PickingMode.Ignore;
			return wrapper;
		}

		// 슬롯 = 「숫자키  이름  비용」. 선택된 것만 테두리가 밝아진다.
		private void FillHotbar(TowerDefenseStageSO stage)
		{
			hotbarPanel.Clear();
			hotbarSlots.Clear();
			if (stage == null)
				return;

			hotbarPanel.Add(MakeHotbarSlot("1", "포탑 인형", stage.TowerCost, stage.TowerTint));
			hotbarPanel.Add(MakeHotbarSlot("2", "채집 인형", stage.HarvesterCost, stage.HarvesterTint));
		}

		private VisualElement MakeHotbarSlot(string key, string name, int cost, Color tint)
		{
			VisualElement slot = new VisualElement();
			slot.style.flexDirection = FlexDirection.Row;
			slot.style.alignItems = Align.Center;
			slot.style.marginLeft = 5;
			slot.style.marginRight = 5;
			slot.style.paddingLeft = 10;
			slot.style.paddingRight = 12;
			slot.style.paddingTop = 7;
			slot.style.paddingBottom = 7;
			slot.style.backgroundColor = new Color(0.04f, 0.05f, 0.08f, 0.78f);
			slot.style.borderTopLeftRadius = 5;
			slot.style.borderTopRightRadius = 5;
			slot.style.borderBottomLeftRadius = 5;
			slot.style.borderBottomRightRadius = 5;
			slot.style.borderLeftWidth = 2;
			slot.style.borderRightWidth = 2;
			slot.style.borderTopWidth = 2;
			slot.style.borderBottomWidth = 2;
			slot.pickingMode = PickingMode.Ignore;

			VisualElement swatch = new VisualElement();
			swatch.style.width = 12;
			swatch.style.height = 12;
			swatch.style.marginRight = 8;
			swatch.style.backgroundColor = tint;
			swatch.pickingMode = PickingMode.Ignore;

			Label keyLabel = new Label(key);
			keyLabel.style.fontSize = 12;
			keyLabel.style.color = new Color(0.6f, 0.66f, 0.75f, 1f);
			keyLabel.style.marginRight = 6;
			keyLabel.pickingMode = PickingMode.Ignore;

			Label nameLabel = new Label(name);
			nameLabel.style.fontSize = 14;
			nameLabel.style.color = new Color(0.93f, 0.95f, 0.99f, 1f);
			nameLabel.style.marginRight = 8;
			nameLabel.pickingMode = PickingMode.Ignore;

			Label costLabel = new Label(cost.ToString());
			costLabel.style.fontSize = 14;
			costLabel.style.color = new Color(1f, 0.86f, 0.35f, 1f);
			costLabel.pickingMode = PickingMode.Ignore;

			slot.Add(swatch);
			slot.Add(keyLabel);
			slot.Add(nameLabel);
			slot.Add(costLabel);
			hotbarSlots.Add(slot);
			return slot;
		}

		/// <summary> 선택 표시 갱신 — 컨트롤러가 선택 변경 시 호출. </summary>
		public void SetSelectedKind(TowerDefensePlaceableKind kind)
		{
			int selectedIndex = kind == TowerDefensePlaceableKind.Harvester ? 1 : 0;
			for (int i = 0; i < hotbarSlots.Count; i++)
			{
				Color border = i == selectedIndex
					? new Color(1f, 0.9f, 0.5f, 1f)
					: new Color(1f, 1f, 1f, 0.12f);
				hotbarSlots[i].style.borderLeftColor = border;
				hotbarSlots[i].style.borderRightColor = border;
				hotbarSlots[i].style.borderTopColor = border;
				hotbarSlots[i].style.borderBottomColor = border;
			}
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
			FillLegend(stage);
			FillHotbar(stage);
			SetBannerVisible(false);

			hintLabel.text = stage == null
				? string.Empty
				: "숫자키 1·2 로 고르고, 좌클릭으로 설치   ·   WASD 시점 이동   ·   휠 확대·축소   ·   X 나가기";
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
				TowerDefensePhase.Prepare when match.AutoAdvanceWaves == false =>
					match.IsNextWaveRequested ? "호출됨" : "건설 중 (대기)",
				TowerDefensePhase.Prepare => "건설 " + Mathf.CeilToInt(match.PrepareRemaining) + "초",
				TowerDefensePhase.Assault => "방어 중",
				_ => "종료",
			};

			enemyValue.text = match.Phase == TowerDefensePhase.Assault
				? match.AliveEnemyCount.ToString()
				: "-";

			waveModeButton.text = match.AutoAdvanceWaves ? "진행: 자동" : "진행: 수동";
			// 건설 국면에서만 부를 수 있다 — 못 누르는 버튼을 멀쩡해 보이게 두면 눌러보고 아무 일도 안 난다.
			nextWaveButton.SetEnabled(preparing && match.Outcome == TowerDefenseOutcome.InProgress);
		}

		/// <summary>
		/// 매치 종료 배너. 무한 모드 패배 = 버틴 웨이브 수가 곧 점수 —
		/// 기록 갱신 여부까지 말해야 「다시 도전」이 이유를 갖는다.
		/// </summary>
		public void ShowOutcome(TowerDefenseOutcome outcome, int wavesCleared, int bestWave, bool isNewRecord)
		{
			SetBannerVisible(true);
			SetBestRecord(bestWave);

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

		/// <summary> 최고 기록 표시 — 기록 없으면 「-」(0 웨이브라고 거짓말하지 않는다). </summary>
		public void SetBestRecord(int bestWave)
		{
			bestValue.text = bestWave > 0 ? bestWave.ToString() : "-";
		}

		private void SetBannerVisible(bool visible)
		{
			VisualElement wrapper = bannerLabel.parent;
			if (wrapper != null)
				wrapper.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
		}
	}
}
