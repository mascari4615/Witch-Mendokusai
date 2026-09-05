using System.Collections.Generic;
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
	/// TASK-WM-049 후속 — 점프 조작감(<see cref="JumpContributor"/>) 회귀 락.
	///
	/// 코요테 타임·점프 버퍼·가변 점프는 *있어도 눈에 안 보이고 없어도 눈에 안 보이는* 종류다.
	/// 사람은 「왜인지 점프가 안 먹는 것 같다」고만 말한다. 그래서 숫자로 박아둔다.
	///
	/// - 코요테: 발판에서 막 벗어난 직후에도 점프를 받아준다 (절벽 끝에서 눌렀는데 안 뛰는 것 방지)
	/// - 버퍼: 착지 직전에 누른 점프를 기억했다가 닿는 순간 실행 (연속 점프가 씹히는 것 방지)
	/// - 가변 점프: 버튼을 일찍 떼면 덜 뜬다
	/// </summary>
	public sealed class JumpContributorTest
	{
		private const float DELTA_TIME = MotorTestHarness.FIXED_DELTA_TIME;
		private const float JUMP_FORCE = 5.6f;
		private const float FALL_GRAVITY_MULTIPLIER = 2f;
		private const float LOW_JUMP_GRAVITY_MULTIPLIER = 3f;
		private const float COYOTE_TIME = 0.1f;
		private const float JUMP_BUFFER_TIME = 0.12f;
		private const float IMPACT_MIN_FALL_SPEED = 1.2f;
		private const float IMPACT_MAX_FALL_SPEED = 8f;
		private const float TOLERANCE = 0.001f;

		/// <summary>
		/// <see cref="JumpContributor"/> 가 <c>UnitObject</c> 를 요구하는 건 「죽었나」 한 줄 때문이다.
		/// EditMode 에선 Awake 가 안 돌아 빈 서브클래스로 충분하다 — 프로덕션 코드는 안 건드린다.
		/// </summary>
		private sealed class JumpTestUnit : UnitObject
		{
		}

		// 한 시험이 유닛을 둘 이상 만들 수 있다(가변 점프 비교처럼 「누른 쪽 vs 뗀 쪽」).
		// 하나만 들고 있으면 두 번째를 만들 때 첫 번째가 새고, 그걸 피하려고 시험 안에서
		// TearDown 을 직접 부르게 된다 — 읽는 사람이 「왜 여기서?」를 멈춰 생각하게 만든다.
		private readonly List<GameObject> spawnedUnits = new();

		[TearDown]
		public void TearDown()
		{
			foreach (GameObject unit in spawnedUnits)
			{
				if (unit != null)
					Object.DestroyImmediate(unit);
			}

			spawnedUnits.Clear();
		}

		private JumpContributor MakeContributor(bool startGrounded)
		{
			GameObject unitGameObject = new("JumpContributorTest.Unit");
			spawnedUnits.Add(unitGameObject);
			JumpTestUnit unit = unitGameObject.AddComponent<JumpTestUnit>();

			JumpContributor contributor = new(
				unit,
				JUMP_FORCE,
				FALL_GRAVITY_MULTIPLIER,
				LOW_JUMP_GRAVITY_MULTIPLIER,
				COYOTE_TIME,
				JUMP_BUFFER_TIME,
				IMPACT_MIN_FALL_SPEED,
				IMPACT_MAX_FALL_SPEED);
			contributor.Reset(startGrounded);
			return contributor;
		}

		private static MotorContext Context(MotorGroundState groundState, float verticalVelocity)
		{
			MotorContext context = new();
			context.GroundState = groundState;
			context.Velocity = new Vector3(0f, verticalVelocity, 0f).ToUnity();
			return context;
		}

		[Test]
		public void RequestJump_WhileGrounded_AppliesJumpForce()
		{
			JumpContributor jump = MakeContributor(startGrounded: true);
			MotorContext context = Context(MotorGroundState.Grounded, 0f);

			jump.RequestJump();
			jump.Contribute(context, DELTA_TIME);

			Assert.That(context.Velocity.y, Is.EqualTo(JUMP_FORCE).Within(TOLERANCE), "땅에서 눌렀는데 안 뛴다");
			Assert.That(jump.IsJumping, Is.True);
		}

		/// <summary>발판에서 막 벗어난 직후 — 아직 받아줘야 한다. 절벽 끝 점프가 씹히는 그 증상.</summary>
		[Test]
		public void CoyoteTime_StillJumps_JustAfterLeavingGround()
		{
			JumpContributor jump = MakeContributor(startGrounded: true);

			// 접지 한 tick 으로 코요테 창을 채운 뒤 공중으로.
			jump.Contribute(Context(MotorGroundState.Grounded, 0f), DELTA_TIME);

			MotorContext airborne = Context(MotorGroundState.Airborne, -0.5f);
			jump.Contribute(airborne, DELTA_TIME); // 코요테 창 안 (0.02 < 0.1)

			jump.RequestJump();
			MotorContext jumpTick = Context(MotorGroundState.Airborne, -0.5f);
			jump.Contribute(jumpTick, DELTA_TIME);

			Assert.That(jumpTick.Velocity.y, Is.EqualTo(JUMP_FORCE).Within(TOLERANCE),
				"발판에서 막 벗어났는데 점프가 씹혔다 — 코요테 창이 안 산다");
		}

		/// <summary>창이 지나면 안 받아준다. 안 그러면 공중 점프가 되어버린다.</summary>
		[Test]
		public void CoyoteTime_Expires_AfterWindow()
		{
			JumpContributor jump = MakeContributor(startGrounded: true);
			jump.Contribute(Context(MotorGroundState.Grounded, 0f), DELTA_TIME);

			// 코요테(0.1s) 를 확실히 넘긴다.
			for (int i = 0; i < 10; i++)
				jump.Contribute(Context(MotorGroundState.Airborne, -1f), DELTA_TIME);

			jump.RequestJump();
			MotorContext lateTick = Context(MotorGroundState.Airborne, -1f);
			jump.Contribute(lateTick, DELTA_TIME);

			Assert.That(lateTick.Velocity.y, Is.LessThanOrEqualTo(0f),
				"코요테 창이 지났는데 뛰었다 — 공중 점프가 열렸다");
		}

		/// <summary>착지 직전에 누른 점프를 기억했다가 닿는 순간 실행. 연속 점프가 씹히는 것 방지.</summary>
		[Test]
		public void JumpBuffer_FiresOnLanding_WhenPressedJustBefore()
		{
			JumpContributor jump = MakeContributor(startGrounded: false);

			jump.RequestJump(); // 아직 공중 — 지금은 못 뛴다
			MotorContext airborne = Context(MotorGroundState.Airborne, -3f);
			jump.Contribute(airborne, DELTA_TIME);
			Assert.That(airborne.Velocity.y, Is.LessThan(0f), "공중인데 버퍼만으로 뛰어버렸다");

			// 버퍼(0.12s) 안에 착지.
			MotorContext landed = Context(MotorGroundState.Grounded, 0f);
			jump.Contribute(landed, DELTA_TIME);

			Assert.That(landed.Velocity.y, Is.EqualTo(JUMP_FORCE).Within(TOLERANCE),
				"착지 직전에 누른 점프가 씹혔다 — 버퍼가 안 산다");
		}

		/// <summary>버튼을 일찍 떼면 덜 뜬다 = 가변 점프. 안 그러면 모든 점프가 최대 높이가 된다.</summary>
		[Test]
		public void ReleasingEarly_AppliesLowJumpGravity_SoTheJumpIsShorter()
		{
			JumpContributor held = MakeContributor(startGrounded: true);
			held.RequestJump();
			held.Contribute(Context(MotorGroundState.Grounded, 0f), DELTA_TIME);
			MotorContext heldRise = Context(MotorGroundState.Airborne, JUMP_FORCE);
			held.Contribute(heldRise, DELTA_TIME);

			JumpContributor released = MakeContributor(startGrounded: true);
			released.RequestJump();
			released.Contribute(Context(MotorGroundState.Grounded, 0f), DELTA_TIME);
			released.ReleaseJump();
			MotorContext releasedRise = Context(MotorGroundState.Airborne, JUMP_FORCE);
			released.Contribute(releasedRise, DELTA_TIME);

			Assert.That(releasedRise.Velocity.y, Is.LessThan(heldRise.Velocity.y),
				"버튼을 떼도 똑같이 뜬다 — 가변 점프가 죽었다");
		}

		/// <summary>착지 충격은 떨어진 속도에 비례해야 한다. 카메라 흔들림·소리가 여기 물려 있다.</summary>
		[Test]
		public void Landing_ReportsImpact_ScaledByFallSpeed()
		{
			JumpContributor jump = MakeContributor(startGrounded: false);

			// 빠르게 떨어지는 중 — 최대 낙하 속도를 기억시킨다.
			jump.Contribute(Context(MotorGroundState.Airborne, -IMPACT_MAX_FALL_SPEED), DELTA_TIME);

			MotorContext landed = Context(MotorGroundState.Grounded, 0f);
			jump.Contribute(landed, DELTA_TIME);

			Assert.That(jump.HasPendingLanded, Is.True, "착지했는데 착지 신호가 안 떴다");
			Assert.That(jump.ConsumeLandedImpact(), Is.EqualTo(1f).Within(0.01f),
				"최대 낙하 속도로 떨어졌는데 충격이 1 이 아니다");
			Assert.That(jump.HasPendingLanded, Is.False, "소비했는데 신호가 남았다 — 다음 tick 에 또 터진다");
		}
	}
}
