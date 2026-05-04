using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// entity 카테고리. 골격은 Monster 만 (DevWindowController 가 같은 선택). Doll/NPC 는 후속 — 별도 카테고리 또는 sub-grouping 결정.
	/// </summary>
	public class EntityCodexCategory : ICodexCategory
	{
		public string Id => "entity";
		public string DisplayName => "주민";
		public Sprite Icon => null;

		private readonly List<CodexEntry> entries = new();

		public void OnActivate()
		{
			entries.Clear();

			SOHelper.ForEach<Monster>(monster =>
			{
				if (monster == null)
					return;

				entries.Add(new CodexEntry(
					id: $"M_{monster.ID}",
					displayName: monster.Name,
					icon: monster.Sprite,
					source: monster));
			});

			entries.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
		}

		public void OnDeactivate() => entries.Clear();

		public IReadOnlyList<CodexEntry> GetEntries() => entries;

		public VisualElement BuildDetail(CodexEntry entry)
		{
			Monster monster = entry.Source as Monster;
			VisualElement detail = new();

			Label nameLabel = new(monster != null ? monster.Name : entry.DisplayName);
			detail.Add(nameLabel);

			if (monster != null)
			{
				Label idLabel = new($"id: M_{monster.ID}");
				detail.Add(idLabel);

				Label typeLabel = new($"타입: {monster.Type}");
				detail.Add(typeLabel);

				if (string.IsNullOrEmpty(monster.Description) == false)
				{
					Label descLabel = new(monster.Description);
					descLabel.style.whiteSpace = WhiteSpace.Normal;
					detail.Add(descLabel);
				}
			}

			return detail;
		}
	}
}
