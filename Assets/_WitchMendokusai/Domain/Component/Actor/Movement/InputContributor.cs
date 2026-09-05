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

		public InputContributor(UnitObject unitObject, float sprintSpeedMultiplier)
		{
			this.unitObject = unitObject;
			this.sprintSpeedMultiplier = sprintSpeedMultiplier;
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

			context.Velocity.x = direction.x * horizontalSpeed;
			context.Velocity.z = direction.z * horizontalSpeed;
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
			float moveSpeed = unitObject.UnitStat[UnitStatType.MOVEMENT_SPEED] / STAT_PER_UNIT_PER_SECOND;
			if (unitObject.UnitStat[UnitStatType.IS_SPRINTING] > 0)
				moveSpeed *= sprintSpeedMultiplier;

			return moveSpeed;
		}
	}
}
