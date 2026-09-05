using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using WitchMendokusai.DomainSDK.Discovery;

namespace WitchMendokusai
{
	/// <summary>
	/// entity 카테고리. 골격은 Monster 만 (DevWindowController 가 같은 선택). Doll/NPC 는 후속 — 별도 카테고리 또는 sub-grouping 결정.
	/// </summary>
	public class EntityDiscoveryCategory : IEntryProvider
	{
		public string Id => "entity";
		public string DisplayName => "주민";
		public Sprite Icon => null;
		public IReadOnlyList<string> SubGroups => SUB_GROUPS;

		private static readonly Dictionary<MonsterType, string> MONSTER_TYPE_LABELS = new()
		{
			{ MonsterType.Normal, "일반" },
			{ MonsterType.Boss, "보스" },
		};

		private static readonly List<string> SUB_GROUPS = new()
		{
			"일반", "보스",
		};

		private readonly List<EntryDescriptor> entries = new();

		public void OnActivate()
		{
			entries.Clear();

			SOHelper.ForEach<Monster>(monster =>
			{
				if (monster == null)
					return;

				string subGroup = MONSTER_TYPE_LABELS.TryGetValue(monster.Type, out string label) ? label : null;

				string entryId = $"M_{monster.ID}";

				entries.Add(new EntryDescriptor(
					id: entryId,
					displayName: monster.Name,
					icon: monster.Sprite,
					source: monster,
					subGroup: subGroup,
					isUnlocked: DiscoveryUnlocks.IsUnlocked(Id, entryId)));
			});

			entries.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
		}

		public void OnDeactivate() => entries.Clear();

		public IReadOnlyList<EntryDescriptor> GetEntries() => entries;

		public VisualElement BuildDetail(EntryDescriptor entry)
		{
			Monster monster = entry.Source as Monster;
			VisualElement detail = new();

			if (monster == null)
				return detail;

			if (string.IsNullOrEmpty(monster.Description) == false)
			{
				Label descLabel = new(monster.Description);
				descLabel.style.whiteSpace = WhiteSpace.Normal;
				detail.Add(descLabel);
			}

			if (monster.Loots != null && monster.Loots.Count > 0)
			{
				Label lootsLabel = new($"드롭 아이템: {monster.Loots.Count}종");
				detail.Add(lootsLabel);
			}

			return detail;
		}
	}
}
