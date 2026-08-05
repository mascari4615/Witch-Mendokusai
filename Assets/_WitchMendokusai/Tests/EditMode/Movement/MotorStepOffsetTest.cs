using NUnit.Framework;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-049 증분 3 — 작은 턱. 오를 수 있는 턱은 올라가고, 벽은 못 올라가고, 내려가는 턱은
	/// 발이 땅에 닿아야 한다.
	///
	/// Motor 의 STEP_OFFSET_HEIGHT(0.15) 가 그 경계다. 이 경계는 눈에 안 보이는 규칙이라, 지형이
	/// 조금 바뀔 때마다 「여긴 왜 안 올라가지」/「여긴 왜 벽을 타지」가 조용히 생긴다. 경계 양쪽을
	/// 다 못 박아 둔다.
	/// </summary>
	public sealed class MotorStepOffsetTest
	{
		private const float LOWER_TOP_Y = 0f;
		private const float SEAM_Z = 0f;
		private const float WALK_SPEED = 2f;
		private const float POSITION_TOLERANCE = 0.02f;

		/// <summary>z 가 -10~0 인 아래쪽 지면(윗면 y=0).</summary>
		private static void AddLowerPlate(MotorTestHarness harness)
		{
			harness.AddGround(new Vector3(0f, LOWER_TOP_Y - 0.5f, SEAM_Z - 5f), new Vector3(20f, 1f, 10f));
		}

		/// <summary>z 가 0~10 이고 윗면이 <paramref name="topY"/> 인 판. 이음매 z=0 에 수직면이 선다.</summary>
		private static void AddFarPlate(MotorTestHarness harness, float topY)
		{
			harness.AddGround(new Vector3(0f, topY - 0.5f, SEAM_Z + 5f), new Vector3(20f, 1f, 10f));
		}

		/// <summary>STEP_OFFSET_HEIGHT(0.15) 보다 낮은 턱 = 걸어서 그냥 올라가야 한다.</summary>
		[Test]
		public void WalkingIntoLowStep_StepsUpAndStaysGrounded()
		{
			const float STEP_TOP_Y = 0.1f;

			using (MotorTestHarness harness = new(new Vector3(0f, LOWER_TOP_Y, SEAM_Z - 1f)))
			{
				AddLowerPlate(harness);
				AddFarPlate(harness, STEP_TOP_Y);
				harness.AddContributor(new ConstantHorizontalContributor(harness));
				harness.AddContributor(new GravityContributor());

				harness.SetHorizontalIntent(Vector3.forward, WALK_SPEED);
				harness.StepMany(120);

				Assert.That(harness.Position.z, Is.GreaterThan(SEAM_Z),
					$"턱을 못 넘고 막혔다 (z={harness.Position.z}) — 0.1 짜리 턱은 STEP_OFFSET_HEIGHT 안이라 올라가야 한다");
				Assert.That(harness.Position.y, Is.EqualTo(STEP_TOP_Y).Within(POSITION_TOLERANCE),
					$"턱 위에 안 섰다 (y={harness.Position.y})");
				Assert.That(harness.IsGrounded, Is.True, "턱을 올라온 뒤 Airborne");
			}
		}

		/// <summary>STEP_OFFSET_HEIGHT 보다 높은 벽 = 못 올라가고 막혀야 한다. 타고 오르면 RED.</summary>
		[Test]
		public void WalkingIntoTallWall_IsBlocked_DoesNotClimb()
		{
			const float WALL_TOP_Y = 0.5f;

			using (MotorTestHarness harness = new(new Vector3(0f, LOWER_TOP_Y, SEAM_Z - 1f)))
			{
				AddLowerPlate(harness);
				AddFarPlate(harness, WALL_TOP_Y);
				harness.AddContributor(new ConstantHorizontalContributor(harness));
				harness.AddContributor(new GravityContributor());

				harness.SetHorizontalIntent(Vector3.forward, WALK_SPEED);
				harness.StepMany(120);

				Assert.That(harness.Position.y, Is.EqualTo(LOWER_TOP_Y).Within(POSITION_TOLERANCE),
					$"0.5 짜리 벽을 타고 올라갔다 (y={harness.Position.y}) — step offset 이 자기 높이 한계를 안 지킨다");
				Assert.That(harness.Position.z, Is.LessThan(SEAM_Z - 0.4f),
					$"벽을 파고들었다 (z={harness.Position.z}) — 캡슐 반지름만큼 앞에서 막혀야 한다");
			}
		}

		/// <summary>
		/// 내려가는 작은 턱. 걸어 내려가면 아래 땅에 *발이 닿아야* 한다.
		///
		/// 여기가 RED 면 Motor 는 「Grounded 인데 공중에 떠 있는」 상태를 허용한다는 뜻이다:
		/// DetectGround 의 stability 판정이 발밑 0.2m 까지를 접지로 쳐주는데, Grounded 인 동안엔
		/// 중력이 0 으로 눌리고 ground stick 은 Airborne 일 때만 도니까 아무도 캐릭터를 내려놓지 않는다.
		/// = TASK-WM-029-B 가 말한 「ground 판정이 4 군데 흩어져 한 tick 안에서 stale」의 실물.
		/// </summary>
		[Test]
		public void WalkingOffLowStepDown_LandsOnLowerGround_DoesNotGlide()
		{
			const float DROP_TOP_Y = -0.1f;

			using (MotorTestHarness harness = new(new Vector3(0f, LOWER_TOP_Y, SEAM_Z - 1f)))
			{
				AddLowerPlate(harness);
				AddFarPlate(harness, DROP_TOP_Y);
				harness.AddContributor(new ConstantHorizontalContributor(harness));
				harness.AddContributor(new GravityContributor());

				harness.SetHorizontalIntent(Vector3.forward, WALK_SPEED);
				harness.StepMany(120);

				Assert.That(harness.Position.z, Is.GreaterThan(SEAM_Z + 0.5f),
					$"턱을 못 지나갔다 (z={harness.Position.z}) — 이 시험이 내리막을 안 건드리고 있다");
				Assert.That(harness.IsGrounded, Is.True,
					$"내려온 뒤 Airborne (y={harness.Position.y})");
				Assert.That(harness.Position.y, Is.EqualTo(DROP_TOP_Y).Within(POSITION_TOLERANCE),
					$"아래 땅에 발이 안 닿았다 (y={harness.Position.y}, 땅={DROP_TOP_Y}) — 떠서 걷고 있다");
			}
		}
	}
}
