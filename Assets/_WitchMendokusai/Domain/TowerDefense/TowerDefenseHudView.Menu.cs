using UnityEngine;
using Vector2 = WitchMendokusai.Numerics.Vector2;
// ★ 좌표는 판정 쪽 (TASK-WM-214) — 엔진으로 나갈 땐 자동, 엔진에서 받을 땐 캐스트.
using Vector3 = WitchMendokusai.Numerics.Vector3;
using Vector2Int = WitchMendokusai.Numerics.Vector2Int;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	// TowerDefenseHudView 의 Menu 부분. 같은 클래스의 partial 조각이다. 상태(필드)는 TowerDefenseHudView.cs 를 본다.
	public partial class TowerDefenseHudView
	{
		private readonly VisualElement legendPanel;
		// 범례 줄이 실제로 들어가는 곳(래퍼는 접기 버튼까지 감싼다).
		private VisualElement legendRows;
		private TowerDefenseMinimapView minimap;

		/// <summary>
		/// 「처음부터 다시」 요청 — 소유 컨트롤러가 구독해 매치를 새로 시작한다.
		/// 키가 아니라 화면 버튼인 이유: 새 조작키는 입력 정의 3곳을 동시에 늘려야 하는데,
		/// 재시작은 *자주 안 쓰지만 반드시 보여야 하는* 기능이라 숨은 키보다 보이는 버튼이 맞다.
		/// </summary>
		public event System.Action RestartRequested = delegate { };

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

		private bool minimapClickBound;

		/// <summary> 펼치는 지도 — 지형·점·범례·설명이 한자리에. </summary>
		private TowerDefenseMapPanel mapPanel;

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
	}
}
