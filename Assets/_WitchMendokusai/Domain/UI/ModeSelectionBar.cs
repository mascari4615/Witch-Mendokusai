using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 「지금 모드에서 무엇을 고를까」 바 — 건설 모드의 건물 바, 개척의 포탑·채집 바가 *같은 물건*이다.
	///
	/// ★ 왜 공용인가: 개척이 자기 전용 툴바를 따로 만들어 두 벌이 됐다(사용자 지적: "굳이 다른 식의
	///   툴바를 따로 만들 필요가 있을까"). 두 벌이면 칸 하나 늘릴 때 두 곳을 고쳐야 하고 생김새도 따로 논다.
	///   고르는 규칙(무엇이 있고 무엇이 골렸나)은 모드마다 다르지만, *고르는 행위*는 하나다.
	///
	/// 칸은 게임 공용 <see cref="Slot"/> — 인벤토리·핫바와 같은 USS 를 타므로 나중에 진짜 아이콘이
	/// 나오면 이 자리만 갈아끼우면 되고, 스킨을 바꾸면 전부 같이 바뀐다.
	/// </summary>
	public sealed class ModeSelectionBar
	{
		/// <summary> 칸 하나에 들어갈 내용. Icon 이 있으면 그림, 없으면 Tint 색으로 채운다. </summary>
		public readonly struct Entry
		{
			public readonly Sprite Icon;
			public readonly Color Tint;
			public readonly string Name;
			public readonly int Cost;
			public readonly object Tooltip;

			public Entry(string name, int cost, Color tint, Sprite icon = null, object tooltip = null)
			{
				Name = name;
				Cost = cost;
				Tint = tint;
				Icon = icon;
				Tooltip = tooltip;
			}
		}

		private readonly VisualElement container;
		private readonly List<VisualElement> cells = new();
		private readonly List<Slot> slots = new();
		private int selectedIndex;

		/// <summary> 칸을 골랐다(숫자키든 클릭이든 결과는 하나). </summary>
		public event System.Action<int> Selected = delegate { };

		/// <summary> 바닥에서 띄운 높이 / 카드 한 장 높이 — 위에 무엇을 얹을 때 이 둘을 읽어야 안 겹친다. </summary>
		public const int BOTTOM_OFFSET = 24;
		// ★ 84 였을 때 칸 안이 딱 맞아떨어져서, 남는 자리가 없자 *아이콘 상자가 먼저 찌그러졌다*
		//   (사용자 실증: "핫바 슬롯 아이콘 뭐 보이지도 않음"). 글자·그림·값이 다 들어가고도 남게 둔다.
		public const int CARD_HEIGHT = 100;

		/// <summary> 이 바가 차지한 맨 윗선(바닥 기준) — 위쪽 요소가 「얼마나 띄워야 하나」의 답. </summary>
		public static int TopFromBottom => BOTTOM_OFFSET + CARD_HEIGHT;

		public VisualElement Root => container;

		public ModeSelectionBar(string name)
		{
			container = new VisualElement { name = name };
			container.style.position = Position.Absolute;
			container.style.bottom = BOTTOM_OFFSET;
			container.style.left = 0;
			container.style.right = 0;
			container.style.flexDirection = FlexDirection.Row;
			container.style.justifyContent = Justify.Center;
			container.pickingMode = PickingMode.Ignore;
		}

		public void SetVisible(bool visible)
		{
			container.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
		}

		/// <summary> 내용을 통째로 다시 채운다 — 모드 진입·재시작처럼 목록이 바뀌는 시점에만 부른다. </summary>
		public void SetEntries(IReadOnlyList<Entry> entries)
		{
			container.Clear();
			cells.Clear();
			slots.Clear();

			for (int index = 0; index < entries.Count; index++)
				container.Add(BuildCell(entries[index], index));

			SetSelected(selectedIndex);
		}

		public void SetSelected(int index)
		{
			selectedIndex = index;
			for (int slotIndex = 0; slotIndex < slots.Count; slotIndex++)
				slots[slotIndex].SetSelected(slotIndex == index);
		}

		/// <summary>
		/// 세로 카드 배치 — 이름 없이 *그림과 값*만 있는 명일방주식 배치 칸(사용자 지시).
		///
		/// ★ 왜 이름을 빼나: 이름은 길이가 제각각이라 칸이 들쭉날쭉해지고, 배치 중에는 어차피 그림으로 고른다.
		///   무엇인지 자세히 알고 싶을 때는 칸에 마우스를 얹으면 툴팁이 말해준다.
		/// ★ 왜 세로로 기나: 손이 가는 곳(화면 아래)에 칸이 여럿 늘어서야 하는데, 가로로 긴 칸은 여덟 개만
		///   넘어가도 화면 폭을 다 먹는다. 세로 카드는 폭을 아껴 칸 수가 늘어도 버틴다.
		///
		/// <code>
		/// ┌──────┐
		/// │①     │ ← 숫자키
		/// │ [그림] │ ← 건물 모습
		/// │  40  │ ← 값
		/// └──────┘
		/// </code>
		/// </summary>
		public bool CardLayout { get; set; }

		private VisualElement BuildCardCell(Entry entry, int index)
		{
			VisualElement card = new VisualElement();
			card.style.width = 68;
			card.style.height = CARD_HEIGHT;
			card.style.marginLeft = 4;
			card.style.marginRight = 4;
			card.style.alignItems = Align.Center;
			card.style.justifyContent = Justify.SpaceBetween;
			card.style.paddingTop = 4;
			card.style.paddingBottom = 5;
			card.style.backgroundColor = new Color(0.05f, 0.06f, 0.10f, 0.88f);
			card.style.borderTopLeftRadius = 7;
			card.style.borderTopRightRadius = 7;
			card.style.borderBottomLeftRadius = 7;
			card.style.borderBottomRightRadius = 7;
			card.pickingMode = PickingMode.Position;
			card.RegisterCallback<PointerDownEvent>(_ => Selected(index));

			// ★ 툴팁은 *칸*이 띄운다 — 안쪽 슬롯은 포인터를 안 받게 돼 있어(칸 어디를 눌러도 골리게 하려고)
			//   슬롯에 붙인 툴팁 콜백은 애초에 한 번도 불리지 않았다(사용자 실증: "유닛 툴팁 어딨는데").
			if (entry.Tooltip != null)
			{
				object tooltipData = entry.Tooltip;
				card.RegisterCallback<PointerEnterEvent>(_ => card.GetUIServices()?.Tooltip?.Show(tooltipData));
				card.RegisterCallback<PointerLeaveEvent>(_ => card.GetUIServices()?.Tooltip?.Hide());
			}

			Label keyLabel = new Label((index + 1).ToString());
			keyLabel.style.fontSize = 11;
			keyLabel.style.color = new Color(0.55f, 0.62f, 0.72f, 1f);
			keyLabel.style.alignSelf = Align.FlexStart;
			keyLabel.style.marginLeft = 6;
			keyLabel.pickingMode = PickingMode.Ignore;
			card.Add(keyLabel);

			Slot slot = new Slot();
			slot.SetIndex(index);
			slot.style.width = 52;
			slot.style.height = 52;
			// 자리가 모자라면 *다른 것*이 줄어야 한다 — 그림은 줄면 아무것도 아니게 된다.
			slot.style.flexShrink = 0;
			slot.pickingMode = PickingMode.Ignore;
			if (entry.Icon != null)
				slot.SetIcon(entry.Icon);
			else
				slot.SetTint(entry.Tint);
			if (entry.Tooltip != null)
				slot.SetTooltipData(entry.Tooltip);
			slots.Add(slot);
			card.Add(slot);

			Label costLabel = new Label(entry.Cost > 0 ? entry.Cost.ToString() : "-");
			costLabel.style.fontSize = 13;
			costLabel.style.color = entry.Cost > 0
				? new Color(1f, 0.86f, 0.35f, 1f)
				: new Color(0.55f, 0.62f, 0.72f, 1f);
			costLabel.pickingMode = PickingMode.Ignore;
			card.Add(costLabel);

			cells.Add(card);
			return card;
		}

		private VisualElement BuildCell(Entry entry, int index)
		{
			if (CardLayout)
				return BuildCardCell(entry, index);

			VisualElement cell = new VisualElement();
			cell.style.flexDirection = FlexDirection.Row;
			cell.style.alignItems = Align.Center;
			cell.style.marginLeft = 5;
			cell.style.marginRight = 5;
			cell.style.paddingLeft = 8;
			cell.style.paddingRight = 14;
			cell.style.paddingTop = 6;
			cell.style.paddingBottom = 6;
			cell.style.backgroundColor = new Color(0.04f, 0.05f, 0.08f, 0.78f);
			cell.style.borderTopLeftRadius = 6;
			cell.style.borderTopRightRadius = 6;
			cell.style.borderBottomLeftRadius = 6;
			cell.style.borderBottomRightRadius = 6;
			cell.pickingMode = PickingMode.Position;
			cell.RegisterCallback<PointerDownEvent>(_ => Selected(index));

			if (entry.Tooltip != null)
			{
				object rowTooltip = entry.Tooltip;
				cell.RegisterCallback<PointerEnterEvent>(_ => cell.GetUIServices()?.Tooltip?.Show(rowTooltip));
				cell.RegisterCallback<PointerLeaveEvent>(_ => cell.GetUIServices()?.Tooltip?.Hide());
			}

			Slot slot = new Slot();
			slot.SetIndex(index);
			slot.pickingMode = PickingMode.Ignore; // 누르는 판정은 칸 전체가 받는다(칸 어디를 눌러도 골린다).
			if (entry.Icon != null)
				slot.SetIcon(entry.Icon);
			else
				slot.SetTint(entry.Tint);
			if (entry.Tooltip != null)
				slot.SetTooltipData(entry.Tooltip);
			slots.Add(slot);

			Label keyLabel = new Label((index + 1).ToString());
			keyLabel.style.fontSize = 12;
			keyLabel.style.color = new Color(0.6f, 0.66f, 0.75f, 1f);
			keyLabel.style.marginLeft = 8;
			keyLabel.style.marginRight = 6;
			keyLabel.pickingMode = PickingMode.Ignore;

			Label nameLabel = new Label(entry.Name);
			nameLabel.style.fontSize = 14;
			nameLabel.style.color = new Color(0.92f, 0.94f, 0.98f, 1f);
			nameLabel.pickingMode = PickingMode.Ignore;

			cell.Add(slot);
			cell.Add(keyLabel);
			cell.Add(nameLabel);

			if (entry.Cost > 0)
			{
				Label costLabel = new Label(entry.Cost.ToString());
				costLabel.style.fontSize = 14;
				costLabel.style.color = new Color(1f, 0.86f, 0.35f, 1f);
				costLabel.style.marginLeft = 10;
				costLabel.pickingMode = PickingMode.Ignore;
				cell.Add(costLabel);
			}

			cells.Add(cell);
			return cell;
		}
	}
}
