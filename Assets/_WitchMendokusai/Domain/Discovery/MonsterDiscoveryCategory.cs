using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using WitchMendokusai.DomainSDK.Discovery;

namespace WitchMendokusai
{
	/// <summary>
	/// 몬스터 분류. 목록은 들어갈 수 있는 던전에 나오는 종 (MonsterLogManager.Catalog), 해금은 MonsterLogUnlockSource, 단계와 진행은 장부
	/// </summary>
	public class MonsterDiscoveryCategory : IEntryProvider, IDiscoveryRecordProvider
	{
		public const string ID = "monster";
		private const string ENTRY_PREFIX = "M_";
		private const string NORMAL_LABEL = "일반";
		private const string BOSS_LABEL = "보스";

		private static readonly List<string> SUB_GROUPS = new() { NORMAL_LABEL, BOSS_LABEL };

		private readonly MonsterLogManager manager;
		private readonly DiscoverySettings settings;
		private readonly List<EntryDescriptor> entries = new();

		public MonsterDiscoveryCategory(MonsterLogManager manager, DiscoverySettings settings)
		{
			this.manager = manager;
			this.settings = settings;
		}

		public string Id => ID;
		public string DisplayName => "몬스터";
		public Sprite Icon => settings.MonsterIcon;
		public IReadOnlyList<string> SubGroups => SUB_GROUPS;

		public static int MonsterIdOf(string entryId) => int.Parse(entryId.Substring(ENTRY_PREFIX.Length));

		public static string TypeLabelOf(Monster monster) => monster.Type == MonsterType.Boss ? BOSS_LABEL : NORMAL_LABEL;

		public void OnActivate()
		{
			entries.Clear();
			foreach (Monster monster in manager.Catalog)
			{
				string entryId = ENTRY_PREFIX + monster.ID;
				entries.Add(new EntryDescriptor(
					id: entryId,
					displayName: monster.Name,
					icon: monster.Sprite,
					source: monster,
					subGroup: TypeLabelOf(monster),
					isUnlocked: DiscoveryUnlocks.IsUnlocked(Id, entryId)));
			}
		}

		public void OnDeactivate() => entries.Clear();

		public IReadOnlyList<EntryDescriptor> GetEntries() => entries;

		public DiscoveryRecordInfo RecordOf(EntryDescriptor entry)
		{
			Monster monster = (Monster)entry.Source;
			IReadOnlyList<int> targets = manager.TargetsOf(monster);
			int stage = manager.StageOf(monster);
			string line;
			if (entry.IsUnlocked == false)
			{
				line = "아직 못 만남";
			}
			else if (stage >= targets.Count)
			{
				line = "기록 완성";
			}
			else
			{
				line = $"{manager.Book.KillsOf(monster.ID)} / {targets[stage]}";
			}
			return new DiscoveryRecordInfo(stage, targets.Count, line, manager.HasNews(monster));
		}

		// 몬스터는 자기 장 (MonsterLogPage) 이 따로 있음
		public VisualElement BuildDetail(EntryDescriptor entry) => new();
	}
}
