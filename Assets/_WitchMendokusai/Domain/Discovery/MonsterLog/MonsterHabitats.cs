using System.Collections.Generic;

namespace WitchMendokusai
{
	/// <summary>
	/// 던전의 웨이브와 사건표에서 어느 몬스터가 어디에 언제 나오는지 모음. 도감 목록과 사는 곳 칸이 같은 것을 읽음.
	/// 도감에 실을 종을 자산에 따로 적지 않음: 들어갈 수 있는 던전에 나오면 실림
	/// </summary>
	public static class MonsterHabitats
	{
		/// <summary>같은 번호면 같은 종. 자산이 따로 올라온 사본도 하나로 (MonsterGrudge 와 같은 이유)</summary>
		private sealed class ByIdComparer : IEqualityComparer<Monster>
		{
			public bool Equals(Monster left, Monster right) => left.ID == right.ID;

			public int GetHashCode(Monster monster) => monster.ID;
		}

		private static readonly ByIdComparer BY_ID = new();

		/// <param name="includeDevOnly">개발 빌드 전용 던전까지 셈 (개발 빌드와 에디터)</param>
		public static Dictionary<Monster, List<MonsterHabitat>> Collect(IEnumerable<Dungeon> dungeons, bool includeDevOnly)
		{
			Dictionary<Monster, List<MonsterHabitat>> result = new(BY_ID);
			foreach (Dungeon dungeon in dungeons)
			{
				if (dungeon.DevBuildOnly && includeDevOnly == false)
				{
					continue;
				}
				Dictionary<Monster, List<(float Start, float End)>> spans = CollectSpans(dungeon);
				foreach (KeyValuePair<Monster, List<(float Start, float End)>> pair in spans)
				{
					List<MonsterHabitat> habitats = GetOrAdd(result, pair.Key);
					foreach ((float start, float end) in Merge(pair.Value))
					{
						habitats.Add(new MonsterHabitat(dungeon, true, start, end));
					}
				}
				AddEventMonsters(dungeon, spans, result);
			}
			return result;
		}

		private static Dictionary<Monster, List<(float Start, float End)>> CollectSpans(Dungeon dungeon)
		{
			Dictionary<Monster, List<(float Start, float End)>> spans = new(BY_ID);
			foreach (MonsterWave wave in dungeon.MonsterWaves)
			{
				foreach (Monster monster in wave.Monsters)
				{
					if (spans.TryGetValue(monster, out List<(float Start, float End)> list) == false)
					{
						list = new List<(float Start, float End)>();
						spans[monster] = list;
					}
					list.Add((wave.StartTime, wave.EndTime));
				}
			}
			return spans;
		}

		// 사건 (정예, 무리) 으로만 나오는 종. 웨이브에도 나오면 시간대는 웨이브 것
		private static void AddEventMonsters(Dungeon dungeon, Dictionary<Monster, List<(float Start, float End)>> spans, Dictionary<Monster, List<MonsterHabitat>> result)
		{
			if (dungeon.EventTable == null)
			{
				return;
			}
			HashSet<Monster> added = new(BY_ID);
			foreach (SurvivalEventEntry entry in dungeon.EventTable.Events)
			{
				if (entry.Monster != null && spans.ContainsKey(entry.Monster) == false && added.Add(entry.Monster))
				{
					GetOrAdd(result, entry.Monster).Add(new MonsterHabitat(dungeon, false, 0f, 0f));
				}
			}
		}

		/// <summary>겹치거나 이어진 시간대를 하나로</summary>
		public static List<(float Start, float End)> Merge(List<(float Start, float End)> spans)
		{
			List<(float Start, float End)> sorted = new(spans);
			sorted.Sort((left, right) => left.Start.CompareTo(right.Start));
			List<(float Start, float End)> merged = new();
			foreach ((float start, float end) in sorted)
			{
				int last = merged.Count - 1;
				if (last >= 0 && start <= merged[last].End)
				{
					merged[last] = (merged[last].Start, end > merged[last].End ? end : merged[last].End);
				}
				else
				{
					merged.Add((start, end));
				}
			}
			return merged;
		}

		private static List<MonsterHabitat> GetOrAdd(Dictionary<Monster, List<MonsterHabitat>> result, Monster monster)
		{
			if (result.TryGetValue(monster, out List<MonsterHabitat> list) == false)
			{
				list = new List<MonsterHabitat>();
				result[monster] = list;
			}
			return list;
		}
	}
}
