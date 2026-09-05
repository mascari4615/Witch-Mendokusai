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
	/// TASK-WM-049 증분 1 — <see cref="MotorTestHarness"/> 가 EditMode 에서 실제로 Motor 를 굴리는지 자체 증명.
	///
	/// 엣지케이스 회귀 그물(절벽·턱·경사·천장·틈·crease)을 짜기 *전에* 판정 수단부터 못 박는다.
	/// 여기가 RED 면 그 위에 쌓는 모든 엣지케이스 테스트는 판정력이 0 이다 — 그래서 이 두 개가 먼저다.
	///
	/// 확인하는 것: ① EditMode 에서 Physics 쿼리가 런타임 생성 콜라이더를 잡는다
	/// ② Motor 의 중력 적분·낙하가 tick 단위로 진행된다 ③ walkable 바닥에 착지하면 Grounded 로 전이한다.
	/// </summary>
	public sealed class MotorHarnessSmokeTest
	{
		private const float GROUND_TOP_Y = 0f;
		private const float POSITION_TOLERANCE = 0.01f;
		private const float VELOCITY_TOLERANCE = 0.01f;

		/// <summary>바닥이 없으면 Motor 는 계속 Airborne 이고, 중력이 tick 마다 vy 에 누적돼야 한다.</summary>
		[Test]
		public void NoGround_FallsFreelyAndStaysAirborne()
		{
			using (MotorTestHarness harness = new(new Vector3(0f, 5f, 0f).ToUnity()))
			{
				harness.AddContributor(new GravityContributor());

				const int STEP_COUNT = 10;
				harness.StepMany(STEP_COUNT);

				float elapsed = STEP_COUNT * MotorTestHarness.FIXED_DELTA_TIME;
				float expectedVelocityY = Physics.gravity.y * elapsed;

				Assert.That(harness.IsGrounded, Is.False, "바닥이 없는데 Grounded — ground 판정이 허공을 잡았다");
				Assert.That(harness.Context.Velocity.y, Is.EqualTo(expectedVelocityY).Within(VELOCITY_TOLERANCE),
					"중력 누적이 tick 당 g·dt 가 아니다 — GravityContributor 적분 계약 깨짐");
				Assert.That(harness.Position.y, Is.LessThan(5f),
					"vy 는 음수인데 위치가 안 내려갔다 — SweepDescend 가 이동을 적용 안 했거나 하네스가 Motor 결과를 못 읽고 있다");
			}
		}

		/// <summary>walkable 바닥 위로 떨어지면 바닥 윗면에 정확히 서고 Grounded 로 전이해야 한다.</summary>
		[Test]
		public void FallingOntoFlatGround_LandsGroundedAtSurface()
		{
			using (MotorTestHarness harness = new(new Vector3(0f, 5f, 0f).ToUnity()))
			{
				// 윗면이 y=0 이 되도록 두께 1 상자를 y=-0.5 에 둔다.
				harness.AddGround(new Vector3(0f, GROUND_TOP_Y - 0.5f, 0f).ToUnity(), new Vector3(20f, 1f, 20f).ToUnity());
				harness.AddContributor(new GravityContributor());

				// 5m 자유낙하 = 약 1.0s. 넉넉히 2s(100 step) 굴려 착지 후 안정까지 본다.
				harness.StepMany(100);

				Assert.That(harness.IsGrounded, Is.True,
					$"평지에 떨어졌는데 Grounded 가 아니다 (y={harness.Position.y}) — DetectGround stability 판정 확인");
				Assert.That(harness.Position.y, Is.EqualTo(GROUND_TOP_Y).Within(POSITION_TOLERANCE),
					"발이 바닥 윗면에 안 섰다 — SKIN_WIDTH 보정 또는 depenetration 이 위치를 밀었다");
				Assert.That(harness.Context.Velocity.y, Is.EqualTo(0f).Within(VELOCITY_TOLERANCE),
					"착지했는데 vy 가 0 이 아니다 — 다음 tick 에 바닥을 뚫거나 튄다");
				Assert.That(harness.Context.HasGroundNormal, Is.True, "Grounded 인데 ground normal 이 없다");
				Assert.That(harness.Context.GroundNormal.y, Is.GreaterThan(0.5f),
					"평지 ground normal 이 위를 안 본다");
			}
		}
	}
}
