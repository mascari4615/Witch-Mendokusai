using System.Linq;
using NUnit.Framework;
using WitchMendokusai;
using WitchMendokusai.Numerics;
using WitchMendokusai.Server;

namespace WitchMendokusai.ServerTests
{
	/// <summary>
	/// 나갔던 <b>자리</b>에서 다시 시작하나 (TASK-WM-372).
	///
	/// ★ 왜: 가방이 남는 것은 이미 지킨다(BagSurvivesRestart). 그런데 <b>자리</b>는 아무도 안 봤다 —
	///   그게 안 남으면 사람은 매번 원점에서 다시 걸어야 한다. 멀리 간 사람일수록 크게 잃는다
	///   (걸어서 간 거리는 그 사람이 쓴 시간이다).
	///   ⚠ 자리는 <b>신원</b>에 붙는다 — 인형 번호는 접속마다 새로 나므로 그걸로 적으면 남의 자리를 물려받는다.
	/// </summary>
	public sealed class ComeBackToSameSpotTests
	{
		private static WorldSim FreshWorld()
		{
			return new WorldSim
			{
				Gatherables = new WorldGatherables(WorldSeeds.Gatherables()),
				Buildables = ServerBuildingCatalog.Catalog,
			};
		}

		[Test]
		public void 나갔다_다시_오면_그_자리다()
		{
			WorldSim world = FreshWorld();
			WorldDoll first = world.Join(identityId: 7, ServerItemCatalog.Catalog);
			world.TryMove(first.Id, new Vector3(12f, 0f, -8f));
			Vector3 leftAt = world.PositionOf(first.Id);
			world.Leave(first.Id);

			WorldDoll again = world.Join(identityId: 7, ServerItemCatalog.Catalog);

			Assert.That(world.PositionOf(again.Id).x, Is.EqualTo(leftAt.x).Within(0.001f),
				"자리가 안 남으면 사람은 매번 원점에서 다시 걷는다");
			Assert.That(world.PositionOf(again.Id).z, Is.EqualTo(leftAt.z).Within(0.001f));
		}

		/// <summary>★ 껐다 켠 뒤에도 — 기억에 적히고 되살아나야 뜻이 있다.</summary>
		[Test]
		public void 세계를_껐다_켜도_그_자리다()
		{
			WorldSim before = FreshWorld();
			WorldDoll doll = before.Join(identityId: 9, ServerItemCatalog.Catalog);
			before.TryMove(doll.Id, new Vector3(-5f, 0f, 21f));
			Vector3 leftAt = before.PositionOf(doll.Id);
			before.Leave(doll.Id);

			WorldSim after = FreshWorld();
			after.Load(before.Save(), ServerItemCatalog.Catalog);
			WorldDoll again = after.Join(identityId: 9, ServerItemCatalog.Catalog);

			Assert.That(after.PositionOf(again.Id).x, Is.EqualTo(leftAt.x).Within(0.001f),
				"껐다 켜면 자리가 사라지면, 배포마다 모두가 원점으로 끌려간다");
			Assert.That(after.PositionOf(again.Id).z, Is.EqualTo(leftAt.z).Within(0.001f));
		}

		/// <summary>남의 자리를 물려받지 않는다 — 자리는 신원에 붙는다.</summary>
		[Test]
		public void 다른_사람은_그_자리를_안_물려받는다()
		{
			WorldSim world = FreshWorld();
			WorldDoll mine = world.Join(identityId: 3, ServerItemCatalog.Catalog);
			world.TryMove(mine.Id, new Vector3(30f, 0f, 30f));
			world.Leave(mine.Id);

			WorldDoll stranger = world.Join(identityId: 4, ServerItemCatalog.Catalog);

			Assert.That(world.PositionOf(stranger.Id), Is.EqualTo(Vector3.zero),
				"처음 온 사람은 처음 자리에서 시작한다 — 남이 있던 곳이 아니라");
		}
	}
}
