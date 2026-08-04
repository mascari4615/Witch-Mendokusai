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
