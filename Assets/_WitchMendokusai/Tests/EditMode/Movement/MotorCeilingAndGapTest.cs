using NUnit.Framework;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-049 증분 5 — 천장과 좁은 틈.
	///
	/// 천장 = <see cref="Motor"/> 의 상승 sweep. 여기가 깨지면 점프가 천장을 뚫거나, 천장에 머리를
	/// 박고도 계속 올라가려 해서 붙어버린다.
	///
	/// 좁은 틈 = 문간·바위 사이처럼 캡슐이 겨우 지나는 폭. 여기가 깨지면 「문에 끼임」이 된다.
	/// 아깝지만 *못 지나가는* 폭은 시험으로 안 박았다 — 캡슐이 양쪽 벽에 동시에 물리는 대칭 쐐기라
	/// 어느 쪽으로 밀려나느냐가 sweep 순서에 달렸고, 그걸 단정하면 구현을 못 바꾸게 만드는
	/// 과잉 고정이 된다. 여기서 지킬 값어치가 있는 계약은 「지나갈 수 있는 틈은 안 끼인다」쪽이다.
	/// </summary>
	public sealed class MotorCeilingAndGapTest
	{
		private const float GROUND_TOP_Y = 0f;
		private const float CEILING_BOTTOM_Y = 2.5f;
		private const float JUMP_VELOCITY = 6f;
		private const float WALK_SPEED = 2f;
		private const float POSITION_TOLERANCE = 0.05f;

		private static void AddGroundPlate(MotorTestHarness harness)
		{
			harness.AddGround(new Vector3(0f, GROUND_TOP_Y - 0.5f, 0f), new Vector3(20f, 1f, 40f));
		}

		/// <summary>
		/// 캡슐 높이 2 + 천장 밑면 2.5 = 머리 위 여유 0.5. 점프는 그보다 높이 뛸 힘을 준다.
		/// 발이 0.5 를 넘어가면 머리가 천장을 지난 것 = 뚫었다.
		/// </summary>
		[Test]
		public void JumpingIntoCeiling_StopsBelowIt_ThenFallsBack()
		{
			float headroom = CEILING_BOTTOM_Y - MotorTestHarness.CAPSULE_HEIGHT;

			using (MotorTestHarness harness = new(new Vector3(0f, GROUND_TOP_Y, 0f)))
			{
				AddGroundPlate(harness);
				harness.AddGround(new Vector3(0f, CEILING_BOTTOM_Y + 0.5f, 0f), new Vector3(20f, 1f, 40f));
				harness.AddContributor(new GravityContributor());

				// 첫 tick 에 접지 확정시킨 뒤 점프 — 공중에서 시작하면 무엇을 재는지 흐려진다.
				harness.Step();
				Assert.That(harness.IsGrounded, Is.True, "점프 전 접지 실패 — 시험 전제가 안 섰다");

				harness.SetVerticalVelocity(JUMP_VELOCITY);

				float highest = harness.Position.y;
				for (int step = 0; step < 150; step++)
				{
					harness.Step();
					highest = Mathf.Max(highest, harness.Position.y);
				}

				Assert.That(highest, Is.LessThanOrEqualTo(headroom + POSITION_TOLERANCE),
					$"천장을 뚫었다 (최고 y={highest}, 머리 여유={headroom}) — 상승 sweep 이 천장을 안 잡았다");
				Assert.That(harness.IsGrounded, Is.True,
					$"천장 맞고 내려와서 다시 못 섰다 (y={harness.Position.y}) — 천장에 붙었을 가능성");
				Assert.That(harness.Position.y, Is.EqualTo(GROUND_TOP_Y).Within(POSITION_TOLERANCE),
					"착지 높이가 어긋났다");
			}
		}

		/// <summary>캡슐 지름(1.0)보다 조금 넓은 틈은 그냥 지나가야 한다. 문간 끼임 회귀 락.</summary>
		[Test]
		public void WalkingThroughSnugGap_PassesThrough_DoesNotWedge()
		{
			const float GAP_WIDTH = 1.2f;
			const float WALL_THICKNESS = 1f;
			float wallCenterX = (GAP_WIDTH * 0.5f) + (WALL_THICKNESS * 0.5f);

			using (MotorTestHarness harness = new(new Vector3(0f, GROUND_TOP_Y, -3f)))
			{
				AddGroundPlate(harness);
				// z 0~10 구간 양옆에 벽 — 캐릭터는 x=0 한가운데로 들어간다.
				harness.AddGround(new Vector3(-wallCenterX, 1.5f, 5f), new Vector3(WALL_THICKNESS, 3f, 10f));
				harness.AddGround(new Vector3(wallCenterX, 1.5f, 5f), new Vector3(WALL_THICKNESS, 3f, 10f));
				harness.AddContributor(new ConstantHorizontalContributor(harness));
				harness.AddContributor(new GravityContributor());

				harness.SetHorizontalIntent(Vector3.forward, WALK_SPEED);
				harness.StepMany(150);

				Assert.That(harness.Position.z, Is.GreaterThan(2f),
					$"틈에 끼여 못 나갔다 (z={harness.Position.z}) — 지름 1.0 캡슐이 1.2 틈을 못 지난다");
				Assert.That(Mathf.Abs(harness.Position.x), Is.LessThan(0.2f),
					$"틈 안에서 옆으로 밀려났다 (x={harness.Position.x}) — 양쪽 벽 접촉 해소가 한쪽으로 튄다");
				Assert.That(harness.IsGrounded, Is.True, "틈을 지나는 동안 접지를 잃었다");
			}
		}
	}
}
