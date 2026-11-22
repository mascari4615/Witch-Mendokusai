using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	/// <summary>던전 세부 최고 기록 규칙. 항목마다 더 좋은 쪽만 남고, 최단 클리어는 클리어한 판만</summary>
	public sealed class DungeonRecordRulesTest
	{
		private static DungeonRecord Run(int seconds, int kills, int level, int nyang, bool cleared) => new()
		{
			PlayTime = TimeSpan.FromSeconds(seconds),
			KillCount = kills,
			Level = level,
			Nyang = nyang,
			IsCleared = cleared,
		};

		[Test]
		public void Each_stat_keeps_its_best()
		{
			DungeonBestRecords best = default;

			best = DungeonRecordRules.Merge(best, Run(300, 120, 12, 80, cleared: true));
			best = DungeonRecordRules.Merge(best, Run(420, 90, 15, 50, cleared: false));
			best = DungeonRecordRules.Merge(best, Run(250, 100, 10, 95, cleared: true));

			Assert.That(best.LongestSurvivalSeconds, Is.EqualTo(420));
			Assert.That(best.FastestClearSeconds, Is.EqualTo(250));
			Assert.That(best.MostKills, Is.EqualTo(120));
			Assert.That(best.HighestLevel, Is.EqualTo(15));
			Assert.That(best.MostNyang, Is.EqualTo(95));
		}

		[Test]
		public void Defeat_never_sets_fastest_clear()
		{
			DungeonBestRecords best = DungeonRecordRules.Merge(default, Run(60, 10, 3, 5, cleared: false));

			Assert.That(best.FastestClearSeconds, Is.EqualTo(0), "0 은 기록 없음. 패배한 짧은 판이 최단 클리어가 되면 안 됨");
		}

		[Test]
		public void Values_cover_every_stat_once()
		{
			List<DungeonRecordStat> stats = DungeonRecordRules.Values(default, 0).Select(entry => entry.Stat).ToList();

			Assert.That(stats, Is.EquivalentTo(Enum.GetValues(typeof(DungeonRecordStat)).Cast<DungeonRecordStat>()));
		}

		[Test]
		public void Record_key_names_dungeon_and_stat()
		{
			Assert.That(DungeonRecordRules.RecordKey(2, DungeonRecordStat.MostKills), Is.EqualTo("Dungeon.2.MostKills"));
		}
	}
}
