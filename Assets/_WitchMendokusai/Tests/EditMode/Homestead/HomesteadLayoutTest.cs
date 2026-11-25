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
	}
}
