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

		public VisualElement Root => container;

		public ModeSelectionBar(string name)
		{
			container = new VisualElement { name = name };
			container.style.position = Position.Absolute;
			container.style.bottom = 24;
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

		private VisualElement BuildCell(Entry entry, int index)
		{
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
