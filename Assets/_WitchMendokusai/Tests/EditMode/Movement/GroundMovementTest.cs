using NUnit.Framework;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	public sealed class GroundMovementTest
	{
		private sealed class MovementTestUnit : UnitObject { }
		private GameObject unitGameObject;
		private MovementTestUnit unit;
		private const float STEP = 0.02f;
		private const float EPSILON = 0.001f;

		[TestCase(0.15f, 0.05f)]
		[TestCase(0.5f, 0.25f)]
		public void DisabledUnit_DiscardsPreviousImpulseAndHitStop(float impulseSeconds, float pauseSeconds)
		{
			UnitMovement movement = unitGameObject.GetComponent<UnitMovement>();
			System.Reflection.FieldInfo impulseField = typeof(UnitMovement).GetField("externalImpulse",
				System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
			impulseField.SetValue(movement, new ExternalImpulseContributor());
			InputContributor input = new(unit, 2f) { MinimumHorizontalSpeed = 3f };
			typeof(UnitMovement).GetField("inputContributor", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(movement, input);
			movement.ApplyImpulse(Vector3.right * 10f, impulseSeconds);
			movement.Pause(pauseSeconds);
			Assert.That(movement.IsExternallyDriven, Is.True);
			Assert.That(movement.IsPaused, Is.True);

			// EditMode에서는 런타임 메시지 대신 수명 콜백 직접 실행
			System.Reflection.MethodInfo onDisable = typeof(UnitMovement).GetMethod("OnDisable",
				System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
			onDisable.Invoke(movement, null);

			Assert.That(movement.IsExternallyDriven, Is.False);
			Assert.That(movement.IsPaused, Is.False);
			Assert.That(input.MinimumHorizontalSpeed, Is.Zero, "Pooled movement must not retain an attack state's speed floor");
			movement.ApplyImpulse(Vector3.left * 5f, impulseSeconds);
			movement.Pause(pauseSeconds);
			Assert.That(movement.IsExternallyDriven, Is.True, "New impulse must still work after reuse");
			Assert.That(movement.IsPaused, Is.True, "New hit-stop must still work after reuse");
		}

		[SetUp]
		public void SetUp()
		{
			unitGameObject = new GameObject("GroundMovementTest");
			unit = unitGameObject.AddComponent<MovementTestUnit>();
			unit.UnitStat[UnitStatType.MOVEMENT_SPEED] = 30;
		}

		[TearDown]
		public void TearDown() => Object.DestroyImmediate(unitGameObject);

		private static GroundMovementTuning Tuning()
			=> JsonUtility.FromJson<GroundMovementTuning>(
				"{\"enabled\":true,\"acceleration\":10,\"deceleration\":20,\"airAcceleration\":2,\"crouchSpeedMultiplier\":0.4}");

		[Test]
		public void AnalogIntent_ReachesProportionalSpeed()
		{
			InputContributor input = new(unit, 2f, Tuning());
			MotorContext context = new() { GroundState = MotorGroundState.Grounded, MoveDirection = Vector3.forward * 0.25f };
			for (int i = 0; i < 50; i++) input.Contribute(context, STEP);
			Assert.That(context.Velocity.z, Is.EqualTo(0.75f).Within(EPSILON));
		}

		[Test]
		public void LowAnalogIntent_ActuallyMovesMotor()
		{
			using (MotorTestHarness harness = new(Vector3.zero))
			{
				harness.AddGround(new Vector3(0f, -0.5f, 0f), new Vector3(20f, 1f, 20f));
				harness.AddContributor(new InputContributor(unit, 2f, Tuning()));
				harness.AddContributor(new GravityContributor());
				harness.Context.MoveDirection = Vector3.forward * 0.1f;
				harness.StepMany(50);
				Assert.That(harness.Position.z, Is.GreaterThan(0.25f));
				Assert.That(harness.IsGrounded, Is.True);
			}
		}

		[Test]
		public void GroundAccelerationAndRelease_HaveFiniteRates()
		{
			InputContributor input = new(unit, 2f, Tuning());
			MotorContext context = new() { GroundState = MotorGroundState.Grounded, MoveDirection = Vector3.forward };
			input.Contribute(context, STEP);
			Assert.That(context.Velocity.z, Is.EqualTo(0.2f).Within(EPSILON));
			for (int i = 0; i < 20; i++) input.Contribute(context, STEP);
			context.MoveDirection = Vector3.zero;
			input.Contribute(context, STEP);
			Assert.That(context.Velocity.z, Is.EqualTo(2.6f).Within(EPSILON));
		}

		[Test]
		public void AirSteering_DoesNotSnapToReverseDirection()
		{
			InputContributor input = new(unit, 2f, Tuning());
			MotorContext context = new() { GroundState = MotorGroundState.Airborne, MoveDirection = Vector3.back, Velocity = new Vector3(0f, -1f, 3f) };
			input.Contribute(context, STEP);
			Assert.That(context.Velocity.z, Is.EqualTo(2.96f).Within(EPSILON));
			Assert.That(context.Velocity.y, Is.EqualTo(-1f));
		}

		[Test]
		public void Crouch_TakesPrecedenceOverSprint()
		{
			unit.UnitStat[UnitStatType.IS_CROUCHING] = 1;
			unit.UnitStat[UnitStatType.IS_SPRINTING] = 1;
			InputContributor input = new(unit, 2f, Tuning());
			MotorContext context = new() { GroundState = MotorGroundState.Grounded, MoveDirection = Vector3.forward };
			for (int i = 0; i < 50; i++) input.Contribute(context, STEP);
			Assert.That(context.Velocity.z, Is.EqualTo(1.2f).Within(EPSILON));
		}

		[Test]
		public void LegacyUnits_KeepImmediateStatSpeed()
		{
			InputContributor input = new(unit, 2f);
			MotorContext context = new() { MoveDirection = Vector3.forward };
			input.Contribute(context, STEP);
			Assert.That(context.Velocity.z, Is.EqualTo(3f).Within(EPSILON));
		}

		[TestCase(20, 0, 3f)]
		[TestCase(50, 0, 5f)]
		[TestCase(20, 20, 4f)]
		public void TemporaryMinimumSpeed_PreservesNaturalAndBonusStats(int natural, int bonus, float expected)
		{
			unit.UnitStat[UnitStatType.MOVEMENT_SPEED] = natural;
			unit.UnitStat[UnitStatType.MOVEMENT_SPEED_BONUS] = bonus;
			InputContributor input = new(unit, 2f) { MinimumHorizontalSpeed = 3f };
			MotorContext context = new() { MoveDirection = Vector3.forward };
			input.Contribute(context, STEP);
			Assert.That(context.Velocity.z, Is.EqualTo(expected).Within(EPSILON));
			Assert.That(unit.UnitStat[UnitStatType.MOVEMENT_SPEED], Is.EqualTo(natural));
			Assert.That(unit.UnitStat[UnitStatType.MOVEMENT_SPEED_BONUS], Is.EqualTo(bonus));
		}

		[Test]
		public void StatReinitialization_DoesNotSubtractAnExpiredApproachBonus()
		{
			InputContributor input = new(unit, 2f) { MinimumHorizontalSpeed = 3f };
			MotorContext context = new() { MoveDirection = Vector3.forward };
			input.Contribute(context, STEP);
			UnitStat replacement = new();
			replacement[UnitStatType.MOVEMENT_SPEED] = 20;
			unit.UnitStat.Set(replacement);
			input.MinimumHorizontalSpeed = 0f;
			input.Contribute(context, STEP);
			Assert.That(context.Velocity.z, Is.EqualTo(2f).Within(EPSILON));
			Assert.That(unit.UnitStat[UnitStatType.MOVEMENT_SPEED_BONUS], Is.Zero);
		}

		[Test]
		public void ExternalImpulse_IsNotOverwrittenBySteering()
		{
			InputContributor input = new(unit, 2f, Tuning()) { MinimumHorizontalSpeed = 100f };
			MotorContext context = new() { IsExternallyDriven = true, MoveDirection = Vector3.back, Velocity = Vector3.forward * 9f };
			input.Contribute(context, STEP);
			Assert.That(context.Velocity.z, Is.EqualTo(9f));
		}

		[Test]
		public void Crouch_KeepsFeetAndCannotStandThroughCeiling()
		{
			CapsuleCollider capsule = unitGameObject.AddComponent<CapsuleCollider>();
			capsule.height = 2f;
			capsule.radius = 0.25f;
			capsule.center = Vector3.up;
			CapsulePosture posture = new(capsule);
			posture.SetCrouching(true, 0.5f);
			Assert.That(capsule.center.y - capsule.height * 0.5f, Is.Zero.Within(EPSILON));
			GameObject ceiling = new("GroundMovementTest.Ceiling");
			try
			{
				ceiling.transform.position = Vector3.up * 1.5f;
				ceiling.AddComponent<BoxCollider>().size = new Vector3(3f, 0.2f, 3f);
				Physics.SyncTransforms();
				posture.SetCrouching(false, 0.5f);
				Assert.That(posture.IsCrouching, Is.True);
				Assert.That(capsule.height, Is.EqualTo(1f));
				ceiling.transform.position = Vector3.up * 4f;
				Physics.SyncTransforms();
				posture.SetCrouching(false, 0.5f);
				Assert.That(posture.IsCrouching, Is.False);
				Assert.That(capsule.height, Is.EqualTo(2f));
			}
			finally { Object.DestroyImmediate(ceiling); }
		}

		[Test]
		public void InputBlock_DiscardsBufferedJump()
		{
			JumpContributor jump = new(unit, 5.6f, 2f, 3f, 0.1f, 0.12f, 1f, 8f);
			MotorContext context = new() { GroundState = MotorGroundState.Grounded, BlockedByExternal = true };
			jump.Reset(true);
			jump.RequestJump();
			jump.Contribute(context, STEP);
			Assert.That(context.Velocity.y, Is.Zero);
			context.BlockedByExternal = false;
			jump.Contribute(context, STEP);
			Assert.That(context.Velocity.y, Is.Zero);
		}
	}
}
