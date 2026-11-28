using System.Collections.Generic;
using NUnit.Framework;
using WitchMendokusai.DomainSDK.Homestead;

namespace WitchMendokusai.Tests
{
	// 별 보는 언덕. 밤 시각, 하룻밤 한 번, 오늘 밤 별자리, 별 잇기, 별 조각과 오늘의 점
	public class HomesteadStarsTest
	{
		private const int NIGHT = 22;
		private const int NOON = 12;
		private const long DAY = 5;

		private static HomesteadSim MakeSim() => HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse _);

		// 별자리의 선을 차례로 그음. 선 끝을 이어 누르고, 끊기면 새 선의 앞 별부터
		private static void DrawAll(StarLink link)
		{
			foreach (int[] line in link.Constellation.Lines)
			{
				if (link.Selected != line[0] && link.Selected != line[1])
				{
					link.Press(line[0]);
				}
				link.Press(link.Selected == line[0] ? line[1] : line[0]);
			}
		}

		[Test]
		public void Twelve_constellations_three_each_season()
		{
			HomesteadSim sim = MakeSim();
			Assert.That(sim.Catalog.Constellations.Count, Is.EqualTo(12));
			for (int season = 0; season < StarSkyCheck.SEASONS; season++)
			{
				Assert.That(sim.Catalog.Constellations.FindAll(constellation => constellation.Season == season).Count, Is.EqualTo(3));
			}
		}

		[Test]
		public void Star_data_passes_the_catalog_check()
		{
			List<string> problems = new();
			StarSkyCheck.Check(HomesteadTestKit.LoadCatalog(), problems);
			Assert.That(problems, Is.Empty);
		}

		[TestCase(20, true)]
		[TestCase(23, true)]
		[TestCase(0, true)]
		[TestCase(3, true)]
		[TestCase(4, false)]
		[TestCase(12, false)]
		[TestCase(19, false)]
		public void Night_wraps_past_midnight(int hour, bool open)
		{
			Assert.That(MakeSim().IsStarHour(hour), Is.EqualTo(open));
		}

		[Test]
		public void Daytime_is_not_ready()
		{
			Assert.That(MakeSim().CanStargaze(NOON, DAY).Failure, Is.EqualTo(HomesteadFailure.NotReady));
		}

		[Test]
		public void Tonight_is_a_constellation_of_the_season_and_changes_by_day()
		{
			HomesteadSim sim = MakeSim();
			HashSet<string> seen = new();
			for (long day = 0; day < 3; day++)
			{
				ConstellationDef tonight = sim.TonightConstellation(2, day);
				Assert.That(tonight.Season, Is.EqualTo(2));
				seen.Add(tonight.Id);
			}
			Assert.That(seen.Count, Is.EqualTo(3));
			Assert.That(sim.TonightConstellation(2, DAY).Id, Is.EqualTo(sim.TonightConstellation(2, DAY).Id));
		}

		[Test]
		public void Wrong_star_only_moves_the_selection()
		{
			StarLink link = MakeSim().StartStarLink(0, DAY);
			int[] first = link.Constellation.Lines[0];
			link.Press(first[0]);
			int stranger = FindStarNotLinkedTo(link.Constellation, first[0]);
			Assert.That(link.Press(stranger), Is.EqualTo(-1));
			Assert.That(link.DrawnCount, Is.EqualTo(0));
			Assert.That(link.Selected, Is.EqualTo(stranger));
		}

		[Test]
		public void Same_line_twice_counts_once()
		{
			StarLink link = MakeSim().StartStarLink(0, DAY);
			int[] first = link.Constellation.Lines[0];
			link.Press(first[0]);
			Assert.That(link.Press(first[1]), Is.EqualTo(0));
			Assert.That(link.Press(first[0]), Is.EqualTo(-1));
			Assert.That(link.DrawnCount, Is.EqualTo(1));
		}

		[Test]
		public void Every_constellation_can_be_finished()
		{
			HomesteadSim sim = MakeSim();
			foreach (ConstellationDef constellation in sim.Catalog.Constellations)
			{
				StarLink link = new(constellation);
				DrawAll(link);
				Assert.That(link.IsDone, constellation.Id);
			}
		}

		[Test]
		public void Unfinished_sky_gives_nothing()
		{
			HomesteadSim sim = MakeSim();
			StarLink link = sim.StartStarLink(0, DAY);
			Assert.That(sim.FinishStargaze(link, NIGHT, DAY).Failure, Is.EqualTo(HomesteadFailure.NotReady));
			Assert.That(sim.Count(sim.Catalog.StarTuning.Shard), Is.EqualTo(0));
			Assert.That(sim.HasOmen, Is.False);
		}

		[Test]
		public void First_find_gives_more_shards_and_an_omen()
		{
			HomesteadSim sim = MakeSim();
			StarTuning tuning = sim.Catalog.StarTuning;
			StarLink link = sim.StartStarLink(0, DAY);
			DrawAll(link);
			Assert.That(sim.FinishStargaze(link, NIGHT, DAY).Ok);
			Assert.That(sim.Count(tuning.Shard), Is.EqualTo(tuning.FirstShards));
			Assert.That(sim.IsConstellationFound(link.Constellation.Id));
			Assert.That(sim.State.Omen, Is.EqualTo(link.Constellation.Id));
		}

		[Test]
		public void Once_a_night_then_again_tomorrow_with_fewer_shards()
		{
			HomesteadSim sim = MakeSim();
			StarTuning tuning = sim.Catalog.StarTuning;
			ConstellationDef tonight = sim.TonightConstellation(0, DAY);
			StarLink first = new(tonight);
			DrawAll(first);
			sim.FinishStargaze(first, NIGHT, DAY);
			Assert.That(sim.CanStargaze(NIGHT, DAY).Failure, Is.EqualTo(HomesteadFailure.AlreadyDone));

			// 같은 별자리를 다른 날 다시 (계절의 별자리 수만큼 뒤)
			long again = DAY + sim.Catalog.Constellations.FindAll(constellation => constellation.Season == 0).Count;
			StarLink second = sim.StartStarLink(0, again);
			Assert.That(second.Constellation.Id, Is.EqualTo(tonight.Id));
			DrawAll(second);
			Assert.That(sim.FinishStargaze(second, NIGHT, again).Ok);
			Assert.That(sim.Count(tuning.Shard), Is.EqualTo(tuning.FirstShards + tuning.RepeatShards));
		}

		[Test]
		public void Morning_while_linking_keeps_it_for_tomorrow()
		{
			HomesteadSim sim = MakeSim();
			StarLink link = sim.StartStarLink(0, DAY);
			DrawAll(link);
			Assert.That(sim.FinishStargaze(link, NOON, DAY).Failure, Is.EqualTo(HomesteadFailure.NotReady));
			Assert.That(sim.HasStargazed(DAY), Is.False);
		}

		[Test]
		public void Omen_is_taken_once_at_dungeon_start()
		{
			HomesteadSim sim = MakeSim();
			StarLink link = sim.StartStarLink(1, DAY);
			DrawAll(link);
			sim.FinishStargaze(link, NIGHT, DAY);
			ConstellationDef omen = sim.TakeOmen();
			Assert.That(omen.Id, Is.EqualTo(link.Constellation.Id));
			Assert.That(omen.Value, Is.GreaterThan(0));
			Assert.That(sim.TakeOmen(), Is.Null);
		}

		[Test]
		public void Omen_and_meal_are_separate()
		{
			HomesteadSim sim = MakeSim();
			sim.Give("potato", 2);
			sim.Cook("k_potato", false);
			sim.SetMeal("potato_bake");
			StarLink link = sim.StartStarLink(3, DAY);
			DrawAll(link);
			sim.FinishStargaze(link, NIGHT, DAY);
			Assert.That(sim.EatMeal(), Is.Not.Null);
			Assert.That(sim.TakeOmen(), Is.Not.Null);
		}

		[Test]
		public void Repair_drops_unknown_constellations()
		{
			HomesteadState state = new();
			state.Constellations.Add("c_gone");
			state.Omen = "c_gone";
			List<string> dropped = HomesteadRepair.DropUnknown(state, HomesteadTestKit.LoadCatalog());
			Assert.That(dropped, Does.Contain("c_gone"));
			Assert.That(state.Constellations, Is.Empty);
			Assert.That(state.Omen, Is.Empty);
		}

		private static int FindStarNotLinkedTo(ConstellationDef constellation, int star)
		{
			for (int other = 0; other < constellation.Stars.Count; other++)
			{
				if (other == star)
					continue;
				bool linked = constellation.Lines.Exists(line => (line[0] == star && line[1] == other) || (line[1] == star && line[0] == other));
				if (linked == false)
					return other;
			}
			Assert.Fail($"{constellation.Id}: 별 {star} 이 모든 별과 이어져 있음");
			return -1;
		}
	}
}
