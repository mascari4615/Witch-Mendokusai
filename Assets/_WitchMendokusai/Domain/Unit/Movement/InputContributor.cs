using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 입력 → horizontal velocity 변환 contributor.
	/// 매 tick context.Velocity의 horizontal 성분을 *덮어쓴다* (이전 tick 입력은 버린다).
	/// vertical 성분은 GravityContributor / JumpContributor 담당.
	/// </summary>
	public class InputContributor : IVelocityContributor
	{
		private readonly UnitObject unitObject;
		private readonly float sprintSpeedMultiplier;
		private readonly GroundMovementTuning tuning;

		public InputContributor(UnitObject unitObject, float sprintSpeedMultiplier, GroundMovementTuning tuning = null)
		{
			this.unitObject = unitObject;
			this.sprintSpeedMultiplier = sprintSpeedMultiplier;
			this.tuning = tuning;
		}

		public void Contribute(MotorContext context, float deltaTime)
		{
			if (context.BlockedByExternal || unitObject.UnitStat[UnitStatType.DEAD] > 0)
			{
				context.Velocity.x = 0f;
				context.Velocity.z = 0f;
				return;
			}

			// ExternalImpulse(dash/knockback)가 이미 horizontal을 채웠으면 input 기여 보류.
			if (context.IsExternallyDriven)
				return;

			float horizontalSpeed = GetHorizontalSpeed();
			Vector3 direction = context.MoveDirection;

			Vector3 targetVelocity = new(direction.x * horizontalSpeed, 0f, direction.z * horizontalSpeed);
			if (tuning != null && tuning.Enabled)
			{
				Vector3 currentVelocity = new(context.Velocity.x, 0f, context.Velocity.z);
				float acceleration = context.GroundState == MotorGroundState.Grounded
					? (direction.sqrMagnitude > 0f ? tuning.Acceleration : tuning.Deceleration)
					: tuning.AirAcceleration;
				targetVelocity = Vector3.MoveTowards(currentVelocity, targetVelocity, acceleration * deltaTime);
			}
			context.Velocity.x = targetVelocity.x;
			context.Velocity.z = targetVelocity.z;
		}

		/// <summary>
		/// 이동 속도 스탯 → 초당 월드 단위 환산. 스탯 30 = 초당 3.
		///
		/// ★ 밖으로 낸 이유: 「초당 몇 칸」으로 설계된 값(개척의 영웅 속도 등)을 스탯으로 바꿔 넣어야
		///   하는 곳이 있는데, 그쪽이 10 을 따로 적으면 여기를 고치는 순간 두 곳이 조용히 갈라진다.
		/// </summary>
		public const float STAT_PER_UNIT_PER_SECOND = 10f;

		private float GetHorizontalSpeed()
		{
			float moveSpeed = (unitObject.UnitStat[UnitStatType.MOVEMENT_SPEED] + unitObject.UnitStat[UnitStatType.MOVEMENT_SPEED_BONUS]) / STAT_PER_UNIT_PER_SECOND;
			if (tuning != null && tuning.Enabled && unitObject.UnitStat[UnitStatType.IS_CROUCHING] > 0)
				return moveSpeed * tuning.CrouchSpeedMultiplier;
			if (unitObject.UnitStat[UnitStatType.IS_SPRINTING] > 0)
				moveSpeed *= sprintSpeedMultiplier;

			return moveSpeed;
		}
	}
}
