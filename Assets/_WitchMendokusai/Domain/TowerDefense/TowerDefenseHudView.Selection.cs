using UnityEngine;
using Vector2 = WitchMendokusai.Numerics.Vector2;
// ★ 좌표는 판정 쪽 (TASK-WM-214) — 엔진으로 나갈 땐 자동, 엔진에서 받을 땐 캐스트.
using Vector3 = WitchMendokusai.Numerics.Vector3;
using Vector2Int = WitchMendokusai.Numerics.Vector2Int;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	// TowerDefenseHudView 의 Selection 부분. 같은 클래스의 partial 조각이다. 상태(필드)는 TowerDefenseHudView.cs 를 본다.
	public partial class TowerDefenseHudView
	{
		// 커서가 얹힌 유닛 설명 — 「이게 뭐고 얼마나 버티나」를 물어볼 유일한 수단.
		private VisualElement unitTooltip;
		private Label unitTooltipLabel;
		// 고른 건물에 하는 일 — 지금은 코어의 「연구」 하나지만, 건물 레벨·선택지가 붙을 자리다.
		private VisualElement selectionPanel;
		private Label selectionTitleLabel;
		// 공용 선택 바 — 건설 모드의 건물 바와 같은 물건(개척 전용 툴바를 따로 두지 않는다).
		private readonly ModeSelectionBar selectionBar;
		public event System.Action SelectionCloseRequested = delegate { };

		/// <summary>
		/// 고른 건물 패널 — 좌하단. 「이미 서 있는 것에 하는 일」이 여기 모인다(지금은 코어의 연구).
		///
		/// ★ 왜 고른 뒤에 뜨나 (사용자 지시: "레벨 업 할때마다 화면에 바로 띄우면 안될 것 같고,
		///   건물 선택하면 그때 띄우거나"): 건물이 수십 개가 되면 각자의 알림이 화면을 덮는다.
		///   물어본 것에만 답하는 화면이 결국 더 많은 것을 보여준다.
		/// </summary>
		// 미니맵 위에 앉는 높이 — 미니맵이 커지면 이 값도 같이 커져야 한다(같은 모서리를 나눠 쓴다).
		private const int SELECTION_PANEL_BOTTOM = 330;

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
			// ★ 불투명하게 (사용자 지시: "배경도 불투명하게 해줘"). 반투명이면 뒤의 판이 비쳐
			//   글자와 겹쳐 읽히지 않고, 무엇보다 「지금 무엇을 하는 창인지」가 흐려진다.
			panel.style.backgroundColor = new Color(0.03f, 0.04f, 0.07f, 1f);
			panel.style.display = DisplayStyle.None;
			panel.pickingMode = PickingMode.Position;

			// 안쪽 카드 — 글과 버튼은 여기 담긴다(바깥은 화면을 덮는 어둠).
			// ★ 전체화면으로 만들어 봤다가 되돌렸다 — 화면으로 확인하니 「체력 72/72」 넉 줄에 화면
			//   전체를 쓰는 그림이 나왔다. 휑한 검은 화면은 큰 게 아니라 빈 것이다.
			//   *전체화면이 맞는 창은 성좌*(마디 43개가 실제로 화면을 채운다)이고, 이건 고른 것을
			//   설명하는 카드다. 배경만 불투명하게 두어 뒤가 비치지 않게 한다.
			VisualElement card = new VisualElement();
			card.style.minWidth = 560;
			card.style.maxWidth = 760;
			card.style.paddingLeft = 28;
			card.style.paddingRight = 28;
			card.style.paddingTop = 22;
			card.style.paddingBottom = 22;
			card.style.backgroundColor = new Color(0.05f, 0.06f, 0.10f, 1f);
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
			// ★ 팔기는 지금 화면에서 뺀다 (사용자 지시: "제거는 우선 기능 빼봐").
			//   오른쪽 단추로 파는 길을 뺐는데 이 단추만 남아 있으면 「없앴다」와 「있다」가 한 화면에서 갈린다.
			//   배선은 남겨 둔다 — 되살릴 땐 이 한 줄만 되돌리면 된다(다시 만들 이유가 없다).
			if (sellButton != null)
				sellButton.style.display = DisplayStyle.None;
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
	}
}
