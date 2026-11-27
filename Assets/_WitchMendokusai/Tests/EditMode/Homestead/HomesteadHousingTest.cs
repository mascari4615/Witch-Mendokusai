using NUnit.Framework;
using WitchMendokusai.DomainSDK.Homestead;

namespace WitchMendokusai.Tests
{
	// 방 꾸미기 규칙. 넓히기와 벽 가구
	public class HomesteadHousingTest
	{
		private static HomesteadSim ExpandedOnce(out PlacedFurniture north, out PlacedFurniture east, out PlacedFurniture table)
		{
			HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse purse);
			sim.AddFurniture("wall_window", 1);
			sim.AddFurniture("thread_rack", 1);
			sim.AddFurniture("wood_table", 1);
			HouseTierDef room = sim.HouseTier;
			FurnitureDef window = sim.Catalog.Get<FurnitureDef>("wall_window");
			FurnitureDef rack = sim.Catalog.Get<FurnitureDef>("thread_rack");
			float x = 1.2f;
			float z = room.Depth / 2f;
			RoomRules.SnapToWall(window, room, ref x, ref z, out float yaw);
			Assert.That(sim.Place("wall_window", x, z, yaw, out north).Ok);
			x = room.Width / 2f;
			z = -0.8f;
			RoomRules.SnapToWall(rack, room, ref x, ref z, out yaw);
			Assert.That(sim.Place("thread_rack", x, z, yaw, out east).Ok);
			Assert.That(sim.Place("wood_table", 0.5f, 0.5f, 0f, out table).Ok);
			sim.TryGetNextHouseTier(out HouseTierDef next);
			HomesteadTestKit.Afford(sim, purse, next.Cost);
			Assert.That(sim.ExpandHouse().Ok);
			return sim;
		}

		[Test]
		public void Wall_furniture_follows_its_wall_when_the_room_grows()
		{
			HomesteadSim sim = ExpandedOnce(out PlacedFurniture north, out PlacedFurniture east, out PlacedFurniture _);
			HouseTierDef room = sim.HouseTier;
			FurnitureDef window = sim.Catalog.Get<FurnitureDef>("wall_window");
			FurnitureDef rack = sim.Catalog.Get<FurnitureDef>("thread_rack");
			Assert.That(north.Z, Is.EqualTo(room.Depth / 2f - window.Depth / 2f).Within(0.001f), "북쪽 벽에 등을 붙임");
			Assert.That(north.X, Is.EqualTo(1.2f).Within(0.001f), "벽을 따라선 그 자리");
			Assert.That(north.Yaw, Is.EqualTo(180f));
			Assert.That(east.X, Is.EqualTo(room.Width / 2f - rack.Depth / 2f).Within(0.001f), "동쪽 벽에 등을 붙임");
			Assert.That(east.Z, Is.EqualTo(-0.8f).Within(0.001f));
			Assert.That(east.Yaw, Is.EqualTo(270f));
		}

		[Test]
		public void Floor_furniture_stays_put_when_the_room_grows()
		{
			ExpandedOnce(out PlacedFurniture _, out PlacedFurniture _, out PlacedFurniture table);
			Assert.That(table.X, Is.EqualTo(0.5f));
			Assert.That(table.Z, Is.EqualTo(0.5f));
		}
	}
}
