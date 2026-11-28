using System.Collections.Generic;
using NUnit.Framework;
using WitchMendokusai.DomainSDK.Homestead;

namespace WitchMendokusai.Tests
{
	// 양봉. 벌통 놓기, 한 판에 한 번 차기, 둘레 꽃에 따라 꿀 종류, 밀랍에서 밀랍초까지
	public class HomesteadBeeTest
	{
		// 마을 배치 안에서 아무것도 없는 칸 묶음 (왼쪽 아래 끝)
		private const int FREE_X = -22;
		private const int FREE_Z = -11;

		private static HomesteadSim MakeSim(out HomesteadTestKit.FakePurse purse)
		{
			HomesteadSim sim = HomesteadTestKit.MakeSim(out purse);
			sim.SetVillageBlocked(new List<(int X, int Z)>());
			return sim;
		}

		private static PlacedPiece PutHive(HomesteadSim sim, HomesteadTestKit.FakePurse purse, int x, int z)
		{
			HomesteadTestKit.Afford(sim, purse, sim.Catalog.Get<PlaceableDef>(HomesteadSim.HIVE).Cost);
			HomesteadResult result = sim.Put(HomesteadSim.HIVE, x, z, 0, out PlacedPiece hive);
			Assert.That(result.Ok, result.Failure.ToString());
			return hive;
		}

		private static void PutFlowerbed(HomesteadSim sim, HomesteadTestKit.FakePurse purse, int x, int z)
		{
			HomesteadTestKit.Afford(sim, purse, sim.Catalog.Get<PlaceableDef>("flowerbed").Cost);
			Assert.That(sim.Put("flowerbed", x, z, 0, out PlacedPiece _).Ok);
		}

		[Test]
		public void Bee_data_passes_the_catalog_check()
		{
			List<string> problems = HomesteadCatalogCheck.Run(HomesteadTestKit.LoadCatalog());
			Assert.That(problems.FindAll(problem => problem.Contains("꿀") || problem.Contains("beeswax") || problem.Contains("honey")), Is.Empty, string.Join("\n", problems));
		}

		[Test]
		public void A_hive_fills_once_per_dungeon_run_and_waits_when_full()
		{
			HomesteadSim sim = MakeSim(out HomesteadTestKit.FakePurse purse);
			PlacedPiece hive = PutHive(sim, purse, FREE_X, FREE_Z);
			Assert.That(sim.TakeHoney(hive.Uid, out HoneyDef _).Failure, Is.EqualTo(HomesteadFailure.NotReady));

			sim.AdvanceMinutes(sim.Catalog.Tuning.HiveFillMinutes - 1);
			Assert.That(sim.IsHiveFull(hive), Is.False);
			Assert.That(sim.HiveMinutesLeft(hive), Is.EqualTo(1));

			// 세 판을 비워도 한 번 몫만. 벌은 떠나지 않고 꿀은 사라지지 않음
			sim.AdvanceMinutes(sim.Catalog.Tuning.HiveFillMinutes * 3);
			Assert.That(sim.TakeHoney(hive.Uid, out HoneyDef honey).Ok);
			Assert.That(honey.Goods, Is.EqualTo("honey"));
			Assert.That(sim.Count("honey"), Is.EqualTo(sim.Catalog.Tuning.HiveHoney));
			Assert.That(sim.Count(HomesteadSim.BEESWAX), Is.EqualTo(sim.Catalog.Tuning.HiveWax));
			Assert.That(sim.TakeHoney(hive.Uid, out HoneyDef _).Failure, Is.EqualTo(HomesteadFailure.NotReady));
		}

		[Test]
		public void Hives_are_limited()
		{
			HomesteadSim sim = MakeSim(out HomesteadTestKit.FakePurse purse);
			for (int i = 0; i < sim.Catalog.Tuning.HiveLimit; i++)
			{
				PutHive(sim, purse, FREE_X + i * 2, FREE_Z);
			}
			HomesteadTestKit.Afford(sim, purse, sim.Catalog.Get<PlaceableDef>(HomesteadSim.HIVE).Cost);
			Assert.That(sim.Put(HomesteadSim.HIVE, FREE_X, FREE_Z + 2, 0, out PlacedPiece _).Failure, Is.EqualTo(HomesteadFailure.NoRoom));
		}

		[Test]
		public void Flowerbeds_near_the_hive_make_bloom_honey()
		{
			HomesteadSim sim = MakeSim(out HomesteadTestKit.FakePurse purse);
			PlacedPiece hive = PutHive(sim, purse, FREE_X, FREE_Z);
			int reach = sim.Catalog.Tuning.HiveReach;
			// 둘레 밖의 꽃밭은 세지 않음
			PutFlowerbed(sim, purse, FREE_X + reach + 1, FREE_Z);
			Assert.That(sim.HoneyOf(hive).Goods, Is.EqualTo("honey"));
			PutFlowerbed(sim, purse, FREE_X + reach, FREE_Z + reach);
			Assert.That(sim.HoneyOf(hive).Goods, Is.EqualTo("honey_bloom"));

			sim.AdvanceMinutes(sim.Catalog.Tuning.HiveFillMinutes);
			Assert.That(sim.TakeHoney(hive.Uid, out HoneyDef honey).Ok);
			Assert.That(honey.Goods, Is.EqualTo("honey_bloom"));
			Assert.That(sim.Count("honey_bloom"), Is.EqualTo(sim.Catalog.Tuning.HiveHoney));
		}

		[Test]
		public void Field_crops_near_the_hive_win_by_count()
		{
			HomesteadSim sim = MakeSim(out HomesteadTestKit.FakePurse purse);
			PlacedPiece hive = PutHive(sim, purse, FREE_X, FREE_Z);
			// 밭 칸 셋을 벌통 옆에 둔 것처럼. 화면이 알리는 자리
			List<(int X, int Z)> cells = new();
			for (int i = 0; i < sim.State.Plots.Count; i++)
			{
				cells.Add(i < 3 ? (FREE_X + 1 + i, FREE_Z + 1) : (FREE_X + 20, FREE_Z + 20));
			}
			sim.SetPlotCells(cells);
			PutFlowerbed(sim, purse, FREE_X, FREE_Z + 2);
			Assert.That(sim.HoneyOf(hive).Goods, Is.EqualTo("honey_bloom"));

			sim.State.Plots[0].Crop = "pumpkin";
			// 꽃밭 하나와 호박 하나는 같은 수. 자료에서 뒤의 꿀 (드문 꽃) 이 이김
			Assert.That(sim.HoneyOf(hive).Goods, Is.EqualTo("honey_pumpkin"));
			sim.State.Plots[1].Crop = "moonbean";
			sim.State.Plots[2].Crop = "moonbean";
			Assert.That(sim.HoneyOf(hive).Goods, Is.EqualTo("honey_moon"));
			// 둘레 밖 밭의 작물은 세지 않음
			sim.State.Plots[1].Crop = string.Empty;
			sim.State.Plots[2].Crop = string.Empty;
			sim.State.Plots[3].Crop = "moonbean";
			Assert.That(sim.HoneyOf(hive).Goods, Is.EqualTo("honey_pumpkin"));
		}

		[Test]
		public void Moving_a_hive_keeps_its_fill_and_takes_new_flowers()
		{
			HomesteadSim sim = MakeSim(out HomesteadTestKit.FakePurse purse);
			PlacedPiece hive = PutHive(sim, purse, FREE_X, FREE_Z);
			PutFlowerbed(sim, purse, FREE_X + 10, FREE_Z);
			sim.AdvanceMinutes(sim.Catalog.Tuning.HiveFillMinutes);
			Assert.That(sim.MovePiece(hive.Uid, FREE_X + 9, FREE_Z + 1, 0).Ok);
			Assert.That(sim.IsHiveFull(hive));
			Assert.That(sim.HoneyOf(hive).Goods, Is.EqualTo("honey_bloom"));
		}

		[Test]
		public void Removing_a_hive_forgets_its_fill()
		{
			HomesteadSim sim = MakeSim(out HomesteadTestKit.FakePurse purse);
			PlacedPiece hive = PutHive(sim, purse, FREE_X, FREE_Z);
			Assert.That(sim.RemovePiece(hive.Uid).Ok);
			Assert.That(sim.State.Hives.ContainsKey(hive.Uid), Is.False);
			Assert.That(sim.Count("wood"), Is.EqualTo(sim.Catalog.Get<PlaceableDef>(HomesteadSim.HIVE).Cost.Goods["wood"]));
		}

		[Test]
		public void Wax_makes_the_candle_and_honey_stays_for_food()
		{
			HomesteadCatalog catalog = HomesteadTestKit.LoadCatalog();
			RecipeDef candle = catalog.Get<RecipeDef>("c_candle");
			Assert.That(candle.Inputs.ContainsKey(HomesteadSim.BEESWAX));
			Assert.That(candle.Inputs.ContainsKey("honey"), Is.False);
			Assert.That(catalog.Get<CookDef>("k_pancake").Inputs.ContainsKey("honey"));
			// 의뢰는 목록에서 무작위로 고름. 타운홀 1 부터 얻는 꽃밭 꿀만 (호박꽃, 달콩꽃은 늦게 열림)
			Assert.That(catalog.Get<RequestDef>("q_jam").Goods, Does.Contain("honey_bloom"));
		}

		// 수치 확인. 벌통 둘이 한 판에 내는 값이 밭 몇 칸 몫인지 (밭 감자 한 칸 한 판 43.0, HomesteadFarmBotTest)
		[Test]
		public void Two_hives_are_worth_about_two_plots_per_run()
		{
			HomesteadCatalog catalog = HomesteadTestKit.LoadCatalog();
			int wax = catalog.Get<GoodsDef>(HomesteadSim.BEESWAX).Price * catalog.Tuning.HiveWax;
			foreach (HoneyDef honey in catalog.Honeys)
			{
				int perRun = catalog.Tuning.HiveLimit * (catalog.Get<GoodsDef>(honey.Goods).Price * catalog.Tuning.HiveHoney + wax);
				TestContext.WriteLine($"{honey.Name}: 벌통 {catalog.Tuning.HiveLimit}개 한 판 {perRun}냥");
				Assert.That(perRun, Is.InRange(60, 320));
			}
		}
	}
}
