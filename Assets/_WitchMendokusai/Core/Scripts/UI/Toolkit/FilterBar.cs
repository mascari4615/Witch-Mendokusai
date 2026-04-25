using System;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// ItemType별 필터 버튼 모음. 클릭 시 OnFilterChanged 이벤트.
	/// </summary>
	public class FilterBar : VisualElement
	{
		public const string USS_CLASS = "wm-filter-bar";
		public const string BUTTON_CLASS = "wm-filter-bar__button";
		public const string BUTTON_ACTIVE_CLASS = "wm-filter-bar__button--active";

		public event Action<ItemType> OnFilterChanged = delegate { };

		private ItemType current = ItemType.None;

		public FilterBar()
		{
			AddToClassList(USS_CLASS);

			AddButton("전체", ItemType.None);
			AddButton("재료", ItemType.Loot);
			AddButton("물약", ItemType.Potion);
			AddButton("장비", ItemType.Equipment);
			AddButton("측면", ItemType.Aspects);

			RefreshActive();
		}

		private void AddButton(string label, ItemType type)
		{
			Button button = new(() => Select(type))
			{
				text = label,
				userData = type
			};
			button.AddToClassList(BUTTON_CLASS);
			Add(button);
		}

		private void Select(ItemType type)
		{
			if (current == type)
				return;
			current = type;
			RefreshActive();
			OnFilterChanged.Invoke(type);
		}

		private void RefreshActive()
		{
			foreach (VisualElement child in Children())
			{
				if (child is Button button && button.userData is ItemType type)
				{
					if (type == current)
						button.AddToClassList(BUTTON_ACTIVE_CLASS);
					else
						button.RemoveFromClassList(BUTTON_ACTIVE_CLASS);
				}
			}
		}
	}
}
