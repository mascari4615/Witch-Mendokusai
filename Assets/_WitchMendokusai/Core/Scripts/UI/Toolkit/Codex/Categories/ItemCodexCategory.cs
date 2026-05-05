using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 아이템 카테고리. SOHelper.ForEach&lt;ItemData&gt; 자동 인덱스.
	/// </summary>
	public class ItemCodexCategory : ICodexCategory
	{
		public string Id => "item";
		public string DisplayName => "아이템";
		public Sprite Icon => null;
		public IReadOnlyList<string> SubGroups => SUB_GROUPS;

		private static readonly Dictionary<ItemType, string> ITEM_TYPE_LABELS = new()
		{
			{ ItemType.Loot, "전리품" },
			{ ItemType.Potion, "포션" },
			{ ItemType.Equipment, "장비" },
			{ ItemType.Aspects, "특성" },
		};

		private static readonly List<string> SUB_GROUPS = new()
		{
			"전리품", "포션", "장비", "특성",
		};

		private readonly List<CodexEntry> entries = new();

		public void OnActivate()
		{
			entries.Clear();

			SOHelper.ForEach<ItemData>(item =>
			{
				if (item == null)
					return;

				string subGroup = ITEM_TYPE_LABELS.TryGetValue(item.Type, out string label) ? label : null;

				entries.Add(new CodexEntry(
					id: $"I_{item.ID}",
					displayName: item.Name,
					icon: item.Sprite,
					source: item,
					gradeKey: item.Grade.ToString().ToLowerInvariant(),
					subGroup: subGroup));
			});

			entries.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
		}

		public void OnDeactivate() => entries.Clear();

		public IReadOnlyList<CodexEntry> GetEntries() => entries;

		public VisualElement BuildDetail(CodexEntry entry)
		{
			ItemData item = entry.Source as ItemData;
			VisualElement detail = new();

			if (item == null)
				return detail;

			if (string.IsNullOrEmpty(item.Description) == false)
			{
				Label descLabel = new(item.Description);
				descLabel.style.whiteSpace = WhiteSpace.Normal;
				detail.Add(descLabel);
			}

			Label priceLabel = new($"구매가: {item.PurchasePrice}  ·  판매가: {item.SalePrice}  ·  최대보유: {item.MaxAmount}");
			detail.Add(priceLabel);

			if (item.Recipes != null && item.Recipes.Count > 0)
			{
				Label recipesLabel = new($"제작 레시피: {item.Recipes.Count}개");
				detail.Add(recipesLabel);
			}

			return detail;
		}
	}
}
