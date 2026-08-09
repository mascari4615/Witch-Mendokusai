using NUnit.Framework;
using UnityEngine;
// ★ 좌표는 판정 쪽 (TASK-WM-214).
using Vector3 = WitchMendokusai.Numerics.Vector3;
using Vector2 = WitchMendokusai.Numerics.Vector2;
using Vector2Int = WitchMendokusai.Numerics.Vector2Int;
using Vector3Int = WitchMendokusai.Numerics.Vector3Int;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-049 증분 6 — 벽 미끄러짐과 안쪽 모서리(crease).
	///
	/// 벽에 비스듬히 부딪히면 벽을 따라 미끄러져야 한다. 안 미끄러지면 벽 근처 조작이 뚝뚝 끊긴다.
	/// 두 벽이 만나는 안쪽 모서리에 처박으면 *조용히 멈춰야* 한다 — 이 대목이 KCC 에서 가장 흔하게
	/// 떨림(두 벽이 서로 밀어내며 매 tick 위치가 튐)이 나는 자리다. 떨림은 화면에서 보기 전엔
	/// 모르는 종류의 결함이라, 사람 눈 대신 「멈춘 뒤 더 안 움직인다」를 시험으로 박는다.
	/// </summary>
	public sealed class MotorWallAndCreaseTest
	{
		private const float GROUND_TOP_Y = 0f;
		private const float WALK_SPEED = 3f;
		private const float WALL_HEIGHT = 3f;
		private const float JITTER_TOLERANCE = 0.01f;

		private static void AddGroundPlate(MotorTestHarness harness)
		{
			harness.AddGround(new Vector3(0f, GROUND_TOP_Y - 0.5f, 0f), new Vector3(40f, 1f, 40f));
		}

		/// <summary>z 가 0 이상인 영역을 채우는 벽 = +z 진행을 막는 면이 z=0 에 선다.</summary>
		private static void AddWallBlockingForward(MotorTestHarness harness)
		{
			harness.AddGround(new Vector3(0f, WALL_HEIGHT * 0.5f, 5f), new Vector3(40f, WALL_HEIGHT, 10f));
		}

		/// <summary>x 가 0 이상인 영역을 채우는 벽 = +x 진행을 막는 면이 x=0 에 선다.</summary>
		private static void AddWallBlockingRight(MotorTestHarness harness)
		{
			harness.AddGround(new Vector3(5f, WALL_HEIGHT * 0.5f, 0f), new Vector3(10f, WALL_HEIGHT, 40f));
		}

		/// <summary>벽에 45° 로 부딪히면 벽을 따라 옆으로 흘러야 한다. 멈춰 서면 RED.</summary>
		[Test]
		public void WalkingDiagonallyIntoWall_SlidesAlongIt()
		{
			using (MotorTestHarness harness = new(new Vector3(0f, GROUND_TOP_Y, -3f)))
			{
				AddGroundPlate(harness);
				AddWallBlockingForward(harness);
				harness.AddContributor(new ConstantHorizontalContributor(harness));
				harness.AddContributor(new GravityContributor());

				harness.SetHorizontalIntent(new Vector3(1f, 0f, 1f), WALK_SPEED);
				harness.StepMany(150);

				Assert.That(harness.Position.z, Is.LessThan(0f),
					$"벽을 파고들었다 (z={harness.Position.z}) — 캡슐 반지름만큼 앞에서 막혀야 한다");
				Assert.That(harness.Position.x, Is.GreaterThan(2f),
					$"벽을 따라 안 미끄러지고 멈췄다 (x={harness.Position.x}) — 벽 접선 투영이 죽었다");
				Assert.That(harness.IsGrounded, Is.True, "벽을 따라 걷는 동안 접지를 잃었다");
			}
		}

		/// <summary>
		/// 두 벽이 만드는 안쪽 모서리에 대각선으로 처박기. 수직 crease 라 옆으로 흘러갈 곳이 없으므로
		/// 조용히 멈추는 게 옳다. 여기서 봐야 할 건 멈춘 위치보다 *멈춘 뒤에도 안 움직이는가* 다.
		/// </summary>
		[Test]
		public void PushingIntoInnerCorner_ComesToRest_DoesNotJitter()
		{
			using (MotorTestHarness harness = new(new Vector3(-3f, GROUND_TOP_Y, -3f)))
			{
				AddGroundPlate(harness);
				AddWallBlockingForward(harness);
				AddWallBlockingRight(harness);
				harness.AddContributor(new ConstantHorizontalContributor(harness));
				harness.AddContributor(new GravityContributor());

				harness.SetHorizontalIntent(new Vector3(1f, 0f, 1f), WALK_SPEED);
				harness.StepMany(150);

				Assert.That(harness.Position.x, Is.LessThan(0f),
					$"오른쪽 벽을 파고들었다 (x={harness.Position.x})");
				Assert.That(harness.Position.z, Is.LessThan(0f),
					$"앞쪽 벽을 파고들었다 (z={harness.Position.z})");
				Assert.That(harness.Position.x, Is.GreaterThan(-1.5f).And.LessThan(0f),
					$"모서리까지 못 갔다 (x={harness.Position.x}) — 시험이 모서리를 안 건드리고 있다");

				// 계속 밀고 있는데도 더는 안 움직여야 한다 = 떨림 없음.
				Vector3 resting = harness.Position;
				harness.StepMany(40);
				Vector3 drift = harness.Position - resting;

				Assert.That(drift.magnitude, Is.LessThan(JITTER_TOLERANCE),
					$"모서리에서 떨고 있다 (40 tick 동안 {drift.magnitude}m 이동) — " +
					"두 벽 접촉 해소가 매 tick 서로를 밀어내고 있다");
			}
		}
	}
}
