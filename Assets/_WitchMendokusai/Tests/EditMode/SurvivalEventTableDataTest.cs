using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;

namespace WitchMendokusai.Tests
{
	public class SurvivalEventTableDataTest
	{
		private const string TABLE_PATH = "Assets/_WitchMendokusai/Domain/Dungeon/Survival/SET_SlimeForest10.asset";
		private static readonly float[] CHEST_ELITE_TIMES = { 240f, 420f };
		private static readonly float[] REGULAR_ELITE_TIMES = { 120f, 360f, 480f };
		private static readonly float[] SWARM_TIMES = { 240f, 540f };

		private static SurvivalEventTable Table => AssetDatabase.LoadAssetAtPath<SurvivalEventTable>(TABLE_PATH);

		private static DungeonThreatEffect ThreatWithExtraElites(int extraPerGap) => new DungeonThreatEffect(1, 1f, 1f, extraPerGap, 1f, 1f, 1f);

		[Test]
		public void Evolution_chest_elites_stand_at_four_and_seven_minutes()
		{
			float[] chestTimes = Table.Events.Where(entry => entry.GuaranteedChest).Select(entry => entry.Time).OrderBy(time => time).ToArray();

			CollectionAssert.AreEqual(CHEST_ELITE_TIMES, chestTimes);
			Assert.IsTrue(Table.Events.Where(entry => entry.GuaranteedChest).All(entry => entry.Type == SurvivalEventType.Elite && entry.Count == 1));
		}

		[Test]
		public void Existing_events_keep_their_times()
		{
			float[] regularElites = Table.Events.Where(entry => entry.Type == SurvivalEventType.Elite && entry.GuaranteedChest == false).Select(entry => entry.Time).ToArray();
			float[] swarms = Table.Events.Where(entry => entry.Type == SurvivalEventType.Swarm).Select(entry => entry.Time).ToArray();

			CollectionAssert.AreEqual(REGULAR_ELITE_TIMES, regularElites);
			CollectionAssert.AreEqual(SWARM_TIMES, swarms);
		}

		[Test]
		public void Threat_extra_elites_ignore_chest_events()
		{
			List<SurvivalEventEntry> run = Table.BuildRunEvents(ThreatWithExtraElites(1));
			List<ExtraEliteSlot> expected = DungeonThreatRules.ExtraEliteSlots(REGULAR_ELITE_TIMES, 1);
			int eliteCount = run.Count(entry => entry.Type == SurvivalEventType.Elite);

			Assert.AreEqual(REGULAR_ELITE_TIMES.Length + 2 + expected.Count, eliteCount);
			Assert.AreEqual(2, run.Count(entry => entry.GuaranteedChest));
		}

		[Test]
		public void Identity_threat_keeps_every_event_sorted()
		{
			List<SurvivalEventEntry> run = Table.BuildRunEvents(DungeonThreatEffect.Identity);

			Assert.AreEqual(Table.Events.Count, run.Count);
			CollectionAssert.IsOrdered(run.Select(entry => entry.Time).ToList());
		}
	}
}
