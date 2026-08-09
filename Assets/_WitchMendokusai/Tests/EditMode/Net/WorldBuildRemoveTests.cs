using NUnit.Framework;
using WitchMendokusai.Numerics;

namespace WitchMendokusai.Tests.EditMode.Net
{
	/// <summary>
	/// 부수기도 세계가 판정한다 (TASK-WM-217) — FishNet 건설 채널이 하던 일의 판정 쪽.
	/// </summary>
	public sealed class WorldBuildRemoveTests
	{
		[Test]
		public void 가운데를_찍어도_건물_전체가_지워진다()
		{
			WorldSim world = new WorldSim();
			world.TryPlaceBuilding(new Vector3Int(0, 0, 0), new Vector2Int(3, 3), 5);

			Assert.That(world.TryRemoveBuilding(new Vector3Int(-1, 0, 1)), Is.True);
			Assert.That(world.Buildings().Length, Is.EqualTo(0));
			// 지운 자리에는 다시 지어져야 한다 — 칸이 남아 있으면 영영 못 짓는다.
			Assert.That(world.TryPlaceBuilding(new Vector3Int(0, 0, 0), new Vector2Int(3, 3), 5), Is.True);
		}

		[Test]
		public void 빈_칸을_부수면_아무_일도_없다()
		{
			WorldSim world = new WorldSim();

			Assert.That(world.TryRemoveBuilding(new Vector3Int(9, 0, 9)), Is.False);
			Assert.That(world.BuildVersion, Is.EqualTo(0));
		}

		[Test]
		public void 짓고_부술_때마다_수가_오른다()
		{
			WorldSim world = new WorldSim();

			world.TryPlaceBuilding(new Vector3Int(0, 0, 0), new Vector2Int(1, 1), 1);
			int afterPlace = world.BuildVersion;
			world.TryRemoveBuilding(new Vector3Int(0, 0, 0));

			Assert.That(afterPlace, Is.EqualTo(1));
			Assert.That(world.BuildVersion, Is.EqualTo(2));
		}

		[Test]
		public void 옆_건물은_안_지워진다()
		{
			WorldSim world = new WorldSim();
			world.TryPlaceBuilding(new Vector3Int(0, 0, 0), new Vector2Int(1, 1), 1);
			world.TryPlaceBuilding(new Vector3Int(5, 0, 5), new Vector2Int(1, 1), 2);

			world.TryRemoveBuilding(new Vector3Int(0, 0, 0));

			Assert.That(world.Buildings().Length, Is.EqualTo(1));
			Assert.That(world.CountBuildings(2), Is.EqualTo(1));
		}
	}
}
