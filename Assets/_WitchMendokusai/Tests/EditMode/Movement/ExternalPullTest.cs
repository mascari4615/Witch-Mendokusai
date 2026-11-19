using NUnit.Framework;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	public sealed class ExternalPullTest
	{
		private sealed class PullTestUnit : UnitObject { }
		private GameObject unitGameObject;
		private PullTestUnit unit;
		private const float STEP = 0.02f;
		private const float EPSILON = 0.001f;
		private const float PULL_RATIO = 0.8f;

		[SetUp]
		public void SetUp()
		{
			unitGameObject = new GameObject("ExternalPullTest");
			unit = unitGameObject.AddComponent<PullTestUnit>();
			unit.UnitStat[UnitStatType.MOVEMENT_SPEED] = 30;
		}

		[TearDown]
		public void TearDown() => Object.DestroyImmediate(unitGameObject);

		private static GroundMovementTuning Tuning()
			=> JsonUtility.FromJson<GroundMovementTuning>(
				"{\"enabled\":true,\"acceleration\":10,\"deceleration\":20,\"airAcceleration\":2,\"crouchSpeedMultiplier\":0.4}");

		private static void Step(ExternalPullContributor pull, InputContributor input, MotorContext context)
		{
			pull.Before.Contribute(context, STEP);
			input.Contribute(context, STEP);
			pull.After.Contribute(context, STEP);
		}

		[Test]
		public void WalkingAway_EscapesPullBelowWalkSpeed()
		{
			ExternalPullContributor pull = new(unit);
			InputContributor input = new(unit, 2f, Tuning());
			MotorContext context = new() { GroundState = MotorGroundState.Grounded, MoveDirection = Vector3.forward };
			float pullSpeed = SlimeKingPatternMath.PullSpeed(InputContributor.GetBaseSpeed(unit), PULL_RATIO);
			for (int i = 0; i < 200; i++)
			{
				pull.Set(Vector3.back * pullSpeed, STEP * 2f);
				Step(pull, input, context);
			}
			// 걷기 3 에서 끌림 2.4 를 빼고 앞으로 0.6. 가속 계산에 끌림이 쌓이면 0 이하
			Assert.That(context.Velocity.z, Is.EqualTo(3f - pullSpeed).Within(EPSILON));
			Assert.That(context.Velocity.z, Is.GreaterThan(0f));
		}

		[Test]
		public void StandingStill_DriftsAtPullSpeed()
		{
			ExternalPullContributor pull = new(unit);
			InputContributor input = new(unit, 2f, Tuning());
			MotorContext context = new() { GroundState = MotorGroundState.Grounded, MoveDirection = Vector3.zero };
			for (int i = 0; i < 100; i++)
			{
				pull.Set(Vector3.left * 2f, STEP * 2f);
				Step(pull, input, context);
			}
			Assert.That(context.Velocity.x, Is.EqualTo(-2f).Within(EPSILON));
		}

		[Test]
		public void NotRefreshed_ExpiresAndLeavesNoResidue()
		{
			ExternalPullContributor pull = new(unit);
			InputContributor input = new(unit, 2f, Tuning());
			MotorContext context = new() { GroundState = MotorGroundState.Grounded, MoveDirection = Vector3.zero };
			pull.Set(Vector3.left * 2f, STEP * 2f);
			for (int i = 0; i < 100; i++)
				Step(pull, input, context);
			Assert.That(pull.IsActive, Is.False);
			Assert.That(context.Velocity.x, Is.EqualTo(0f).Within(EPSILON));
		}

		[Test]
		public void ExternalImpulse_IsNotChangedByPull()
		{
			ExternalPullContributor pull = new(unit);
			InputContributor input = new(unit, 2f, Tuning());
			MotorContext context = new() { IsExternallyDriven = true, Velocity = Vector3.forward * 9f };
			pull.Set(Vector3.left * 2f, 1f);
			Step(pull, input, context);
			Step(pull, input, context);
			Assert.That(context.Velocity.x, Is.EqualTo(0f));
			Assert.That(context.Velocity.z, Is.EqualTo(9f));
		}

		[Test]
		public void DeadUnit_IsNotPulled()
		{
			ExternalPullContributor pull = new(unit);
			InputContributor input = new(unit, 2f, Tuning());
			unit.UnitStat[UnitStatType.DEAD] = 1;
			MotorContext context = new() { GroundState = MotorGroundState.Grounded };
			pull.Set(Vector3.left * 2f, 1f);
			Step(pull, input, context);
			Assert.That(context.Velocity.x, Is.EqualTo(0f));
		}

		[Test]
		public void CrouchWalkSpeed_KeepsPullEscapable()
		{
			InputContributor input = new(unit, 2f, Tuning());
			unit.UnitStat[UnitStatType.IS_CROUCHING] = 1;
			float walk = input.GetWalkSpeed();
			Assert.That(walk, Is.EqualTo(3f * 0.4f).Within(EPSILON));
			Assert.That(SlimeKingPatternMath.PullSpeed(walk, PULL_RATIO), Is.LessThan(walk));
		}
	}
}
