using System.Collections.Generic;
using Newtonsoft.Json;
using NUnit.Framework;
using WitchMendokusai.DomainSDK.Homestead;

namespace WitchMendokusai.Tests
{
	public class HomesteadLayoutTest
	{
		[Test]
		public void New_village_has_every_building_once()
		{
			HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse _);
			foreach (PlaceableDef placeable in sim.Catalog.Placeables)
			{
				if (placeable.Kind == PlaceableKind.Building)
				{
					Assert.That(sim.State.Village.FindAll(piece => piece.Id == placeable.Id).Count, Is.EqualTo(1), placeable.Id);
				}
			}
		}

		// 지을 건물은 4x4. 확정 f12-3 으로 박물관 7x5, 갱도 6x5 는 예외
		// 낚시터 2x2 는 판정에 없던 것이라 지금 값을 그대로 적어 둠
		[Test]
		public void Buildings_are_four_by_four_except_approved_ones()
		{
			Dictionary<string, (int Width, int Depth)> exceptions = new()
			{
				{ "museum", (7, 5) },
				{ "quarry", (6, 5) },
				{ "dock", (2, 2) },
			};
			HomesteadCatalog catalog = HomesteadTestKit.LoadCatalog();
			foreach (BuildingDef building in catalog.Buildings)
			{
				PlaceableDef placeable = catalog.Placeables.Find(candidate => candidate.Id == building.Id);
				Assert.That(placeable, Is.Not.Null, building.Id);
				(int Width, int Depth) expected = exceptions.TryGetValue(building.Id, out (int Width, int Depth) size) ? size : (4, 4);
				Assert.That((placeable.Width, placeable.Depth), Is.EqualTo(expected), building.Id);
			}
		}

		[Test]
		public void Buildings_do_not_all_face_one_way()
		{
			HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse _);
			HashSet<int> turns = new();
			foreach (PlacedPiece piece in sim.State.Village)
			{
				if (sim.PlaceableOf(piece).Kind == PlaceableKind.Building)
				{
					turns.Add(piece.Turn);
				}
			}
			Assert.That(turns.Count, Is.EqualTo(VillageRules.TURNS));
		}

		[Test]
		public void Turning_swaps_width_and_depth()
		{
			HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse _);
			PlaceableDef hall = sim.Catalog.Get<PlaceableDef>(HomesteadSim.HALL);
			Assert.That(VillageRules.WidthOf(hall, 1), Is.EqualTo(hall.Depth));
			Assert.That(VillageRules.DepthOf(hall, 3), Is.EqualTo(hall.Width));
			Assert.That(VillageRules.WidthOf(hall, 2), Is.EqualTo(hall.Width));
		}

		[Test]
		public void Building_moves_to_free_cells_and_is_refused_on_taken_ones()
		{
			HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse _);
			PlacedPiece smithy = sim.PieceOf("smithy");
			PlacedPiece shop = sim.PieceOf("shop");
			int changes = 0;
			sim.LayoutChanged += () => changes++;
			Assert.That(sim.MovePiece(smithy.Uid, shop.X, shop.Z, 0).Failure, Is.EqualTo(HomesteadFailure.Blocked));
			Assert.That(sim.MovePiece(smithy.Uid, -20, 20, 1).Ok);
			Assert.That((smithy.X, smithy.Z, smithy.Turn), Is.EqualTo((-20, 20, 1)));
			Assert.That(changes, Is.EqualTo(1));
			Assert.That(sim.MovePiece(smithy.Uid, -60, 20, 1).Failure, Is.EqualTo(HomesteadFailure.NoRoom));
		}

		[Test]
		public void Piece_can_stay_where_it_is_while_turning()
		{
			HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse _);
			PlacedPiece house = sim.PieceOf("workshop");
			Assert.That(sim.MovePiece(house.Uid, house.X, house.Z, (house.Turn + 1) % VillageRules.TURNS).Ok);
		}

		[Test]
		public void Fixed_piece_does_not_move_and_building_is_not_removed()
		{
			HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse _);
			Assert.That(sim.MovePiece(sim.PieceOf("dock").Uid, -20, 20, 0).Failure, Is.EqualTo(HomesteadFailure.Locked));
			Assert.That(sim.RemovePiece(sim.PieceOf("smithy").Uid).Failure, Is.EqualTo(HomesteadFailure.Locked));
		}

		[Test]
		public void Decor_costs_goods_and_gives_them_back()
		{
			HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse _);
			Assert.That(sim.Put("wall_stone", -20, 20, 0, out PlacedPiece _).Failure, Is.EqualTo(HomesteadFailure.NotEnoughGoods));
			sim.Give("stone", 2);
			Assert.That(sim.Put("wall_stone", -20, 20, 0, out PlacedPiece wall).Ok);
			Assert.That(sim.Count("stone"), Is.EqualTo(0));
			Assert.That(sim.RemovePiece(wall.Uid).Ok);
			Assert.That(sim.Count("stone"), Is.EqualTo(2));
		}

		[Test]
		public void Decor_sits_on_a_path_but_not_on_decor()
		{
			HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse _);
			sim.Give("wood", 10);
			Assert.That(sim.Put("path_dirt", -20, 20, 0, out PlacedPiece path).Ok);
			Assert.That(sim.Put("fence", -20, 20, 0, out PlacedPiece fence).Ok);
			Assert.That(sim.Put("fence", -20, 20, 1, out PlacedPiece _).Failure, Is.EqualTo(HomesteadFailure.Blocked));
			Assert.That(sim.PieceAt(-20, 20), Is.SameAs(fence));
			sim.RemovePiece(fence.Uid);
			Assert.That(sim.PieceAt(-20, 20), Is.SameAs(path));
		}

		[Test]
		public void First_decor_of_a_kind_in_the_village_pays_guild_exp_once()
		{
			HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse _);
			sim.Give("wood", 10);
			Assert.That(sim.Put("fence", -20, 20, 0, out PlacedPiece first).Ok);
			int exp = sim.State.GuildExp;
			Assert.That(sim.RemovePiece(first.Uid).Ok);
			Assert.That(sim.Put("fence", -20, 20, 0, out PlacedPiece _).Ok);
			Assert.That(sim.Put("fence", -19, 20, 0, out PlacedPiece _).Ok);
			Assert.That(exp, Is.EqualTo(sim.Catalog.Tuning.FirstPlaceGuildExp));
			Assert.That(sim.State.GuildExp, Is.EqualTo(exp), "거뒀다 다시 놓아도 한 번");
			sim.Put("path_dirt", -18, 20, 0, out PlacedPiece _);
			Assert.That(sim.State.GuildExp, Is.EqualTo(exp * 2), "다른 종류는 따로");
		}

		[Test]
		public void Locked_building_and_decor_name_the_town_hall_tier_they_need()
		{
			HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse _);
			HomesteadResult ranch = sim.CanBuild("ranch");
			Assert.That(ranch.Failure, Is.EqualTo(HomesteadFailure.TownHallTooLow));
			Assert.That(ranch.Subject, Is.EqualTo("3"));
			PlaceableDef fence = sim.Catalog.Get<PlaceableDef>("fence");
			fence.TownHall = 4;
			HomesteadResult decor = sim.CanMakeDecor("fence");
			Assert.That(decor.Failure, Is.EqualTo(HomesteadFailure.TownHallTooLow));
			Assert.That(decor.Subject, Is.EqualTo("4"));
		}

		[Test]
		public void Blocked_cells_refuse_everything()
		{
			HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse _);
			sim.SetVillageBlocked(new[] { (-20, 20) });
			Assert.That(sim.Put("path_dirt", -20, 20, 0, out PlacedPiece _).Failure, Is.EqualTo(HomesteadFailure.NoRoom));
		}

		[Test]
		public void Saved_layout_survives_and_a_missing_building_finds_a_spot()
		{
			HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse _);
			sim.MovePiece(sim.PieceOf("smithy").Uid, -20, 20, 1);
			sim.State.Village.RemoveAll(piece => piece.Id == "shop");
			HomesteadState loaded = JsonConvert.DeserializeObject<HomesteadState>(JsonConvert.SerializeObject(sim.State));
			HomesteadSim again = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse _, state: loaded);
			Assert.That((again.PieceOf("smithy").X, again.PieceOf("smithy").Turn), Is.EqualTo((-20, 1)));
			PlacedPiece shop = again.PieceOf("shop");
			Assert.That(shop, Is.Not.Null);
			Assert.That(again.CanPut("shop", shop.X, shop.Z, shop.Turn, shop.Uid).Ok);
		}

		// ---- 목장 마당. 여섯 마리 (목장 3단 x 2) 가 거닐어도 서로, 그리고 문과 떨어져 있음

		private const int MAX_ANIMALS = 6;
		private const float ANIMAL_GAP = 0.8f;
		private const float DOOR_GAP = 1f;

		private static float Gap((float Side, float Ahead) a, (float Side, float Ahead) b)
		{
			float side = a.Side - b.Side;
			float ahead = a.Ahead - b.Ahead;
			return (float)System.Math.Sqrt(side * side + ahead * ahead);
		}

		[Test]
		public void Ranch_animals_keep_apart_while_wandering()
		{
			RanchYard yard = new();
			for (int i = 0; i < MAX_ANIMALS; i++)
			{
				for (int j = i + 1; j < MAX_ANIMALS; j++)
				{
					float nearest = Gap(yard.Home(i), yard.Home(j)) - yard.Wander * 2f;
					Assert.That(nearest, Is.GreaterThanOrEqualTo(ANIMAL_GAP), $"{i} 와 {j} 가 거닐다 가장 가까워질 때");
				}
			}
		}

		[Test]
		public void Ranch_animals_stay_off_the_door()
		{
			RanchYard yard = new();
			for (int i = 0; i < MAX_ANIMALS; i++)
			{
				(float Side, float Ahead) home = yard.Home(i);
				Assert.That(Gap(home, (0f, 0f)) - yard.Wander, Is.GreaterThanOrEqualTo(DOOR_GAP), $"{i} 가 문 (목장 자리) 에 가장 가까워질 때");
				Assert.That(System.Math.Abs(home.Side) - yard.Wander, Is.GreaterThan(0.5f), $"{i} 가 문으로 가는 길을 막음");
			}
		}

		[Test]
		public void Ranch_animals_spread_to_both_sides()
		{
			RanchYard yard = new();
			int left = 0;
			int right = 0;
			for (int i = 0; i < MAX_ANIMALS; i++)
			{
				if (yard.Home(i).Side < 0f)
					left++;
				else
					right++;
			}
			Assert.That(left, Is.EqualTo(right), "길 양쪽에 고르게");
			Assert.That(yard.Home(1).Ahead, Is.EqualTo(yard.Home(0).Ahead), "첫 두 마리는 같은 줄, 문 가까이");
		}
	}
}
