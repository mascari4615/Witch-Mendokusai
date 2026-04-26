using System.Collections.Generic;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// Items 모드 — ItemData 일람. 카테고리 필터 + 검색 + 슬롯 그리드.
	/// 슬롯 클릭은 명령 시스템(give) 또는 HoldingManager.PickFromVoid 로 일관되게 위임.
	/// </summary>
	public class ItemsMode : IDevMode
	{
		public string Id => "items";
		public string DisplayName => "Items";
		public VisualElement Root { get; }

		private readonly FilterBar filterBar;
		private readonly TextField searchField;
		private readonly Label hintLabel;
		private readonly ScrollView gridScroll;
		private readonly VisualElement grid;
		private readonly List<DevItemSlot> slots = new();
		private readonly List<ItemData> allItems = new();

		private ItemType currentFilter = ItemType.None;
		private string currentSearch = string.Empty;

		public ItemsMode()
		{
			Root = new VisualElement();
			Root.AddToClassList("wm-dev-mode-items");

			filterBar = new FilterBar();
			filterBar.OnFilterChanged += OnFilterChanged;
			Root.Add(filterBar);

			searchField = new TextField();
			searchField.AddToClassList("wm-dev-mode-items__search");
			searchField.RegisterValueChangedCallback(evt =>
			{
				currentSearch = evt.newValue ?? string.Empty;
				Refresh();
			});
			Root.Add(searchField);

			hintLabel = new Label("좌클릭: 1개  ·  Shift+좌: MaxAmount  ·  우클릭: 16개  ·  Ctrl+좌: 잡기 (인벤 슬롯에 드롭)");
			hintLabel.AddToClassList("wm-dev-mode-items__hint");
			hintLabel.pickingMode = PickingMode.Ignore;
			Root.Add(hintLabel);

			gridScroll = new ScrollView(ScrollViewMode.Vertical);
			gridScroll.AddToClassList("wm-dev-mode-items__grid-scroll");
			Root.Add(gridScroll);

			grid = new VisualElement();
			grid.AddToClassList("wm-dev-mode-items__grid");
			gridScroll.Add(grid);
		}

		public void OnActivate()
		{
			LoadAllItems();
			Refresh();
		}

		public void OnDeactivate() { }

		private void LoadAllItems()
		{
			allItems.Clear();
			SOHelper.ForEach<ItemData>(item =>
			{
				if (item != null)
					allItems.Add(item);
			});
			allItems.Sort((a, b) => a.ID.CompareTo(b.ID));
		}

		private void OnFilterChanged(ItemType type)
		{
			currentFilter = type;
			Refresh();
		}

		private void Refresh()
		{
			List<ItemData> filtered = new();
			for (int i = 0; i < allItems.Count; i++)
			{
				ItemData item = allItems[i];
				if (Matches(item) == false)
					continue;
				filtered.Add(item);
			}

			BuildSlots(filtered.Count);
			for (int i = 0; i < filtered.Count; i++)
				slots[i].Bind(filtered[i]);
		}

		private bool Matches(ItemData item)
		{
			if (currentFilter != ItemType.None && item.Type != currentFilter)
				return false;

			if (string.IsNullOrEmpty(currentSearch))
				return true;

			if (item.Name != null && item.Name.IndexOf(currentSearch, System.StringComparison.OrdinalIgnoreCase) >= 0)
				return true;

			if (item.ID.ToString().Contains(currentSearch))
				return true;

			return false;
		}

		private void BuildSlots(int count)
		{
			while (slots.Count > count)
			{
				DevItemSlot last = slots[^1];
				grid.Remove(last);
				slots.RemoveAt(slots.Count - 1);
			}

			while (slots.Count < count)
			{
				DevItemSlot slot = new();
				grid.Add(slot);
				slots.Add(slot);
			}
		}
	}
}
