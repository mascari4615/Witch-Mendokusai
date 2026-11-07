using NUnit.Framework;
using WitchMendokusai.Net;
using WitchMendokusai.Numerics;

namespace WitchMendokusai.Tests.EditMode.Net
{
	// 게임 창의 내 걸음 루프 (WorldDollBinder.SendMyStep) 를 세계 계산과 같이 돌린다 (2026-11-07)
	// - 세계가 4m 미만 옮긴 뒤 placed 없이 돌면 옛 자리로 도로 걷는 것을 재현
	// - placed 를 따르면 새 자리에 머무는 것을 확인
	public sealed class PlacedFollowTests
	{
		private const float TICK = 0.1f;

		// SendMyStep 과 같은 순서: 세계 자리 기준 걸음, 그 뒤 끌어당김
		private static void Tick(WorldSim world, WorldDoll doll, ref Vector3 screen, PlacedFollow follow)
		{
			Vector3 known = doll.Position;
			if (follow != null && follow.TryHold(screen, known, out Vector3 placedSpot))
			{
				screen = placedSpot;
				return;
			}

			if (MoveIntent.TryStep(known, screen, WorldSim.MAX_STEP, out Vector3 step))
				world.TryMove(doll.Id, step);

			if (PositionCorrection.Resolve(screen, known, TICK, out Vector3 corrected) != PositionCorrection.Action.Keep)
				screen = corrected;
		}

		private static float Flat(Vector3 a, Vector3 b) => new Vector3(a.x - b.x, 0f, a.z - b.z).magnitude;

		// 걸어서 2m 옮긴 뒤 세계가 처음 자리로 다시 세움 (쓰러짐 뒤 다시 세움과 같은 꼴)
		private static (WorldSim world, WorldDoll doll, Vector3 screen, Vector3 home) WalkThenPlaceBack()
		{
			WorldSim world = new WorldSim();
			WorldDoll doll = world.Join();
			Vector3 home = doll.Position;
			Vector3 screen = new Vector3(home.x + 2f, 0f, home.z);
			for (int i = 0; i < 40; i++)
				Tick(world, doll, ref screen, null);

			Assert.That(Flat(doll.Position, screen), Is.LessThan(0.05f), "걸음 단계에서 세계와 화면이 맞아야 한다");
			doll.PlaceAt(home);
			return (world, doll, screen, home);
		}

		[Test]
		public void WithoutPlaced_WalksBackToOldSpot()
		{
			(WorldSim world, WorldDoll doll, Vector3 screen, Vector3 home) = WalkThenPlaceBack();
			for (int i = 0; i < 30; i++)
				Tick(world, doll, ref screen, null);

			// 재현: 세계 자리가 다시 세운 곳을 떠나 옛 자리 쪽으로 감
			Assert.That(Flat(doll.Position, home), Is.GreaterThan(1f));
		}

		[Test]
		public void FollowingPlaced_StaysAtNewSpot()
		{
			(WorldSim world, WorldDoll doll, Vector3 screen, Vector3 home) = WalkThenPlaceBack();
			PlacedFollow follow = new PlacedFollow();
			follow.Receive(home.x, home.z);
			for (int i = 0; i < 30; i++)
				Tick(world, doll, ref screen, follow);

			Assert.That(Flat(doll.Position, home), Is.LessThan(0.05f));
			Assert.That(Flat(screen, home), Is.LessThan(0.05f));
			Assert.That(follow.IsHolding, Is.False);
		}

		[Test]
		public void StaleList_HoldsStepsUntilWorldShowsSpot()
		{
			PlacedFollow follow = new PlacedFollow();
			follow.Receive(0f, 0f);
			Vector3 oldKnown = new Vector3(2f, 0f, 0f);

			Assert.That(follow.TryHold(oldKnown, oldKnown, out Vector3 first), Is.True);
			Assert.That(Flat(first, Vector3.zero), Is.LessThan(0.001f));
			Assert.That(follow.IsHolding, Is.True, "목록이 아직 옛 자리면 계속 붙잡음");

			Assert.That(follow.TryHold(first, Vector3.zero, out Vector3 _), Is.True);
			Assert.That(follow.IsHolding, Is.False, "목록이 새 자리를 보이면 놓음");
			Assert.That(follow.TryHold(first, Vector3.zero, out Vector3 _), Is.False);
		}

		[Test]
		public void HoldEndsAfterMostTicks()
		{
			PlacedFollow follow = new PlacedFollow();
			follow.Receive(0f, 0f);
			Vector3 far = new Vector3(3f, 0f, 0f);
			for (int i = 0; i < PlacedFollow.MOST_HOLD_TICKS; i++)
				Assert.That(follow.TryHold(far, far, out Vector3 _), Is.True);

			Assert.That(follow.IsHolding, Is.False);
		}
	}
}
