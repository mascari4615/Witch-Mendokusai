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

		private readonly List<CodexEntry> entries = new();

		public void OnActivate()
		{
			entries.Clear();

			SOHelper.ForEach<ItemData>(item =>
			{
				if (item == null)
					return;

				entries.Add(new CodexEntry(
					id: $"I_{item.ID}",
					displayName: item.Name,
					icon: item.Sprite,
					source: item));
			});

			entries.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
		}

		public void OnDeactivate() => entries.Clear();

		public IReadOnlyList<CodexEntry> GetEntries() => entries;

		public VisualElement BuildDetail(CodexEntry entry)
		{
			ItemData item = entry.Source as ItemData;
			VisualElement detail = new();

			Label nameLabel = new(item != null ? item.Name : entry.DisplayName);
			detail.Add(nameLabel);

			if (item != null)
			{
				Label idLabel = new($"id: I_{item.ID}");
				detail.Add(idLabel);

				Label gradeLabel = new($"등급: {item.Grade}  /  타입: {item.Type}");
				detail.Add(gradeLabel);

				if (string.IsNullOrEmpty(item.Description) == false)
				{
					Label descLabel = new(item.Description);
					descLabel.style.whiteSpace = WhiteSpace.Normal;
					detail.Add(descLabel);
				}
			}

			return detail;
		}
	}
}
