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

		// 스프린트 속도 배율. UnitMovement 의 [SerializeField] 에서 주입 — JumpContributor 와 같은 경로.
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

		private float GetHorizontalSpeed()
		{
			float moveSpeed = unitObject.UnitStat[UnitStatType.MOVEMENT_SPEED] / 10f;
			if (unitObject.UnitStat[UnitStatType.IS_SPRINTING] > 0)
				moveSpeed *= sprintSpeedMultiplier;

			return moveSpeed;
		}
	}
}
