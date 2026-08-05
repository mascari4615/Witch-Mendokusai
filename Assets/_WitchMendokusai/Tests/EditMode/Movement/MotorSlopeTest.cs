using NUnit.Framework;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-049 증분 4 — 경사면. 걸을 수 있는 비탈에서는 서고, 못 걸을 비탈에서는 미끄러진다.
	///
	/// Motor 의 경계는 GROUND_NORMAL_Y_MIN = 0.5 = 60°. 이 경계가 흔들리면 지형이 조금만 가팔라도
	/// 「올라가지는데 안 서짐」이나 「벽인데 서짐」이 생긴다. 경계 양쪽을 각각 못 박는다.
	///
	/// 이 시험들은 수평 입력 contributor 를 *일부러 안 붙인다* — 붙이면 매 tick 수평 속도를 덮어써서
	/// 미끄러짐(SlideAlongSurface 가 남긴 속도)을 지워버리기 때문. 여기서 보고 싶은 건 중력과 지면만이다.
	/// </summary>
	public sealed class MotorSlopeTest
	{
		private const float WALKABLE_SLOPE_DEGREES = 30f;   // normal.y = cos30 = 0.87 >= 0.5
		private const float TOO_STEEP_SLOPE_DEGREES = 70f;  // normal.y = cos70 = 0.34 <  0.5
		private const float DROP_HEIGHT = 4f;
		private const float SETTLE_TOLERANCE = 0.05f;
		private const float WALK_SPEED = 2f;

		/// <summary>
		/// 지금 밟고 있는 면의 오르막 방향. 「+z 가 오르막」처럼 손으로 박으면 회전 부호를 한 번만
		/// 잘못 읽어도 시험이 조용히 반대를 재고, 그 상태로 GREEN 이 나올 수도 있다(실제로 처음에 그랬다).
		/// 중력을 면에 투영하면 내리막이 나온다 = 기하에서 유도. 지형이 바뀌어도 안 뒤집힌다.
		/// </summary>
		private static Vector3 UphillDirection(MotorTestHarness harness)
		{
			Vector3 downhill = Vector3.ProjectOnPlane(Vector3.down, harness.Context.GroundNormal);
			Assert.That(downhill.sqrMagnitude, Is.GreaterThan(0.0001f),
				"면이 평평하다 — 비탈 시험인데 기울기가 0");
			return -downhill.normalized;
		}

		/// <summary>걸을 수 있는 비탈 = 착지하고, 그 자리에 머문다. 저절로 흘러내리면 RED.</summary>
		[Test]
		public void LandingOnWalkableSlope_StaysGroundedAndDoesNotSlide()
		{
			using (MotorTestHarness harness = new(new Vector3(0f, DROP_HEIGHT, 0f)))
			{
				harness.AddSlope(Vector3.zero, new Vector3(20f, 1f, 20f), WALKABLE_SLOPE_DEGREES);
				harness.AddContributor(new GravityContributor());

				harness.StepMany(150);

				Assert.That(harness.IsGrounded, Is.True,
					$"30° 비탈에 착지 못 했다 (y={harness.Position.y}) — walkable 판정이 경계를 잘못 잡았다");

				Vector3 settled = harness.Position;
				harness.StepMany(60);
				Vector3 drift = harness.Position - settled;

				Assert.That(drift.magnitude, Is.LessThan(SETTLE_TOLERANCE),
					$"걸을 수 있는 비탈인데 저절로 흘러내린다 (60 tick 동안 {drift.magnitude}m 이동) — " +
					"입력 없이 움직이면 서 있는 게 아니다");
			}
		}

		/// <summary>
		/// 비탈을 *걸어 올라간다*. 높이를 얻으면서, 한 tick 도 Airborne 으로 안 튀어야 한다.
		///
		/// 왜 따로 박나 — 위의 「가만히 서 있기」 시험은 접지 판정이 멈춰 있을 때만 본다. 실제로 가장 흔한
		/// 건 비탈을 걸어 다니는 쪽이고, 거기선 매 tick 수평 이동 뒤 접지를 다시 정해야 한다. 여기가 튀면
		/// 오르막에서 발소리·점프·애니메이션이 딸꾹질한다 — 화면에서 보기 전엔 모르는 종류다.
		/// </summary>
		[Test]
		public void WalkingUpWalkableSlope_GainsHeight_NeverGoesAirborne()
		{
			using (MotorTestHarness harness = new(new Vector3(0f, DROP_HEIGHT, 0f)))
			{
				harness.AddSlope(Vector3.zero, new Vector3(40f, 1f, 40f), WALKABLE_SLOPE_DEGREES);
				harness.AddContributor(new ConstantHorizontalContributor(harness));
				harness.AddContributor(new GravityContributor());

				harness.StepMany(150); // 착지·안정
				Assert.That(harness.IsGrounded, Is.True, "출발 전 접지 실패 — 시험 전제가 안 섰다");
				float startY = harness.Position.y;

				harness.SetHorizontalIntent(UphillDirection(harness), WALK_SPEED);
				for (int step = 0; step < 120; step++)
				{
					harness.Step();
					Assert.That(harness.IsGrounded, Is.True,
						$"오르막 보행 {step} tick 에 Airborne 으로 튐 (y={harness.Position.y})");
				}

				Assert.That(harness.Position.y, Is.GreaterThan(startY + 0.3f),
					$"비탈을 걸었는데 높이를 못 얻었다 ({startY} → {harness.Position.y}) — 오르막에서 미끄러지거나 막혔다");
			}
		}

		/// <summary>
		/// 비탈을 *걸어 내려간다*. 발이 면에서 떨어지면 안 된다.
		/// 여기가 깨지면 내리막에서 붕 떠서 미끄러지듯 내려간다(접지 판정과 실제 위치가 벌어지는 그 증상).
		/// </summary>
		[Test]
		public void WalkingDownWalkableSlope_StaysGrounded_DoesNotGlide()
		{
			using (MotorTestHarness harness = new(new Vector3(0f, DROP_HEIGHT, 0f)))
			{
				harness.AddSlope(Vector3.zero, new Vector3(40f, 1f, 40f), WALKABLE_SLOPE_DEGREES);
				harness.AddContributor(new ConstantHorizontalContributor(harness));
				harness.AddContributor(new GravityContributor());

				harness.StepMany(150);
				Assert.That(harness.IsGrounded, Is.True, "출발 전 접지 실패 — 시험 전제가 안 섰다");
				float startY = harness.Position.y;

				harness.SetHorizontalIntent(-UphillDirection(harness), WALK_SPEED);
				for (int step = 0; step < 120; step++)
				{
					harness.Step();
					Assert.That(harness.IsGrounded, Is.True,
						$"내리막 보행 {step} tick 에 Airborne 으로 튐 (y={harness.Position.y}) — 발이 면에서 떨어졌다");
				}

				Assert.That(harness.Position.y, Is.LessThan(startY - 0.3f),
					$"내리막인데 높이가 안 내려갔다 ({startY} → {harness.Position.y})");
			}
		}

		/// <summary>못 걸을 비탈 = 붙잡히지 않고 계속 내려가야 한다. 여기 서 버리면 벽을 밟고 서는 셈.</summary>
		[Test]
		public void LandingOnTooSteepSlope_SlidesDown_DoesNotStick()
		{
			using (MotorTestHarness harness = new(new Vector3(0f, DROP_HEIGHT, 0f)))
			{
				harness.AddSlope(Vector3.zero, new Vector3(20f, 1f, 20f), TOO_STEEP_SLOPE_DEGREES);
				harness.AddContributor(new GravityContributor());

				harness.StepMany(100);
				Vector3 midway = harness.Position;

				harness.StepMany(100);

				Assert.That(harness.IsGrounded, Is.False,
					$"70° 비탈에 서 버렸다 (y={harness.Position.y}) — 못 걸을 경사가 접지로 인정됐다");
				Assert.That(harness.Position.y, Is.LessThan(midway.y),
					$"미끄러지다 멈췄다 (y {midway.y} → {harness.Position.y}) — 가파른 면이 캐릭터를 붙잡고 있다");
			}
		}
	}
}
