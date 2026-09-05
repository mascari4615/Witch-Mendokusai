using UnityEngine;
using Vector2 = WitchMendokusai.Numerics.Vector2;
// ★ 좌표는 판정 쪽 (TASK-WM-214) — 엔진으로 나갈 땐 자동, 엔진에서 받을 땐 캐스트.
using Vector3 = WitchMendokusai.Numerics.Vector3;
using Vector2Int = WitchMendokusai.Numerics.Vector2Int;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	// TowerDefenseHudView 의 Hotbar 부분. 같은 클래스의 partial 조각이다. 상태(필드)는 TowerDefenseHudView.cs 를 본다.
	public partial class TowerDefenseHudView
	{
		private readonly Label hintLabel;

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

		// 값이 바뀌면(할인 카드) 칸을 다시 그린다 — 안 그러면 화면이 옛 값을 계속 말한다.
		private int lastHotbarCostSignature = -1;

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
					// ★ 우클릭은 이제 취소다(판매는 뺐다) — 안내가 없는 기능을 가르치면 안 된다.
					? "설치 대기 — " + what + " · 좌클릭 설치 · 우클릭·ESC 취소"
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
				? "아래 칸 톡 = 고르기 · 자리 톡 → 한 번 더 톡 = 짓기   ·   빈 땅 톡 = 영웅 보내기   ·   코어 톡 → 연구 성좌"
					+ "   ·   끌기 = 시점 · 오므리기 = 확대·축소 · 두 손가락 비틀기 = 회전   ·   「지도」·「창」 단추"
				// ★ 안내가 실제 키와 달랐다: 배속은 Tab 이 아니라 F6 이고, 정작 제일 많이 쓰는
				//   「숫자키로 칸 고르기」는 한 글자도 없었다. 조작 안내가 틀리면 그 화면 전체를 못 믿는다.
				// ★ 「연구를 어떻게 여는지」가 어디에도 없었다(사용자 실증: "연구 어케 여는데").
				//   찾아야만 알 수 있는 기능은 *없는 기능*이다 — 첫 판의 유일한 다음 수라서 더 그렇다.
				// ★ 화면이 없는 기능을 안내하고 있었다 — 「설치 중 우클릭 = 판매」. 판매는 뺐고 그 자리는
				//   이제 *취소*다. 안내가 틀리면 그 화면 전체를 못 믿는다(오늘만 세 번째로 잡은 병).
				: "숫자키 1~9 칸 고르기 · 좌클릭 설치 · 설치 중 우클릭 = 취소   ·   평소 우클릭 = 영웅 보내기   ·   코어 클릭 → 연구 성좌"
					+ "   ·   Space 멈춤 · F6 배속   ·   WASD 시점 · 휠 확대·축소   ·   M 지도   ·   ESC 취소·메뉴";
		}
	}
}
