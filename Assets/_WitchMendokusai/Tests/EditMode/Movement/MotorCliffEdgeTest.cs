using NUnit.Framework;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-049 증분 2 — 절벽 끝 회귀 락. TASK-WM-029 가 고친 그 버그 클래스를 코드로 붙잡아 둔다.
	///
	/// TASK-WM-029 의 증상은 「절벽 끝에서 캐릭터가 안 떨어지고 떠 있음」이었고, 뿌리는 CapsuleCast 의
	/// sphere edge 가 절벽면 모서리를 spurious contact 로 잡은 것이었다. 고침은 vertical 을 발 중심
	/// raycast 로 분리한 것. 그 고침이 살아있다는 걸 매번 사람이 절벽까지 걸어가서 확인할 수는 없다.
	///
	/// 여기서 못 박는 계약 두 짝 — 둘이 서로를 견제한다:
	/// ① 캡슐이 절벽 밖으로 *일부* 삐져나가도, 발 밑(캡슐 축)에 땅이 있으면 계속 서 있는다.
	///    (너무 성급히 떨어지면 절벽 근처가 미끄럽게 느껴진다)
	/// ② 발 밑에서 땅이 사라지면 떨어진다. 떠 있지 않는다.
	///    (WM-029 의 원 증상)
	/// </summary>
	public sealed class MotorCliffEdgeTest
	{
		private const float CLIFF_EDGE_Z = 0f;
		private const float GROUND_TOP_Y = 0f;
		private const float WALK_SPEED = 3f;
		private const float POSITION_TOLERANCE = 0.01f;

		/// <summary>윗면 y=0, z 는 -10 ~ 0 (z=0 이 절벽 끝) 인 지면판.</summary>
		private static void AddCliffPlate(MotorTestHarness harness)
		{
			harness.AddGround(new Vector3(0f, GROUND_TOP_Y - 0.5f, CLIFF_EDGE_Z - 5f), new Vector3(20f, 1f, 10f));
		}

		/// <summary>
		/// 캡슐 반지름의 절반 이상이 허공에 나가 있어도, 캡슐 축 바로 아래에 땅이 있으면 서 있어야 한다.
		/// 이게 깨지면 절벽 *근처* 가 통째로 못 서는 땅이 된다.
		/// </summary>
		[Test]
		public void StandingAtCliffEdge_WithCapsuleOverhang_StaysGrounded()
		{
			// 발은 절벽 안쪽 0.1m, 캡슐(r=0.5)은 절벽 밖으로 0.4m 삐져나간 자세.
			Vector3 start = new(0f, GROUND_TOP_Y, CLIFF_EDGE_Z - 0.1f);
			using (MotorTestHarness harness = new(start))
			{
				AddCliffPlate(harness);
				harness.AddContributor(new GravityContributor());

				harness.StepMany(50);

				Assert.That(harness.IsGrounded, Is.True,
					$"캡슐이 삐져나갔다고 떨어졌다 (y={harness.Position.y}) — stability 판정이 캡슐 부피를 보고 있다. " +
					"발(캡슐 축) 기준이어야 한다");
				Assert.That(harness.Position.y, Is.EqualTo(GROUND_TOP_Y).Within(POSITION_TOLERANCE),
					"절벽 끝에 서 있는데 높이가 흔들린다");
			}
		}

		/// <summary>
		/// TASK-WM-029 원 증상 회귀 락 — 발 밑 땅이 없어지면 떨어져야 한다. 떠 있으면 RED.
		/// </summary>
		[Test]
		public void WalkingOffCliffEdge_Falls_DoesNotHover()
		{
			Vector3 start = new(0f, GROUND_TOP_Y, CLIFF_EDGE_Z - 2f);
			using (MotorTestHarness harness = new(start))
			{
				AddCliffPlate(harness);
				harness.AddContributor(new ConstantHorizontalContributor(harness));
				harness.AddContributor(new GravityContributor());

				harness.SetHorizontalIntent(Vector3.forward, WALK_SPEED);

				// 절벽까지 2m + 낙하 관찰. 100 step = 2s.
				harness.StepMany(100);

				Assert.That(harness.Position.z, Is.GreaterThan(CLIFF_EDGE_Z),
					"절벽 끝까지 못 걸어갔다 — 이 시험이 절벽을 안 건드리고 있다(시험 자체가 무의미)");
				Assert.That(harness.IsGrounded, Is.False,
					$"허공인데 Grounded (y={harness.Position.y}) — WM-029 「절벽 끝 떠있음」 재발");
				Assert.That(harness.Position.y, Is.LessThan(GROUND_TOP_Y - 2f),
					$"떨어지긴 하는데 너무 안 떨어졌다 (y={harness.Position.y}) — 뭔가가 낙하를 잡고 있다");
			}
		}

		/// <summary>
		/// 반대 방향 걷기 = 절벽에서 멀어지는 쪽. 안쪽으로 걷는 내내 한 번도 Airborne 이 되면 안 된다.
		/// 평지 보행 중 간헐 Airborne(= 애니메이션/점프 판정이 튀는 원인)을 잡는 그물.
		/// </summary>
		[Test]
		public void WalkingInlandOnFlatGround_NeverGoesAirborne()
		{
			Vector3 start = new(0f, GROUND_TOP_Y, CLIFF_EDGE_Z - 2f);
			using (MotorTestHarness harness = new(start))
			{
				AddCliffPlate(harness);
				harness.AddContributor(new ConstantHorizontalContributor(harness));
				harness.AddContributor(new GravityContributor());

				harness.SetHorizontalIntent(Vector3.back, WALK_SPEED);

				for (int step = 0; step < 100; step++)
				{
					harness.Step();
					Assert.That(harness.IsGrounded, Is.True,
						$"평지 보행 {step} 번째 tick 에 Airborne 으로 튐 (z={harness.Position.z}, y={harness.Position.y})");
				}

				Assert.That(harness.Position.y, Is.EqualTo(GROUND_TOP_Y).Within(POSITION_TOLERANCE),
					"평지를 걸었는데 높이가 흘렀다 — tick 마다 미세하게 가라앉거나 뜨고 있다");
			}
		}
	}
}
