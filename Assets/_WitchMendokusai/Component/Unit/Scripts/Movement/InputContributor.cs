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

		public InputContributor(UnitObject unitObject)
		{
			this.unitObject = unitObject;
		}

		public void Contribute(MotorContext context, float deltaTime)
		{
			if (context.BlockedByExternal || unitObject.UnitStat[UnitStatType.DEAD] > 0)
			{
				context.Velocity.x = 0f;
				context.Velocity.z = 0f;
				return;
			}

			float horizontalSpeed = GetHorizontalSpeed();
			Vector3 direction = context.MoveDirection;

			context.Velocity.x = direction.x * horizontalSpeed;
			context.Velocity.z = direction.z * horizontalSpeed;
		}

		private float GetHorizontalSpeed()
		{
			if (unitObject.UnitStat[UnitStatType.FORCE_MOVE] > 0)
				return SOManager.Instance.DashSpeed.RuntimeValue;

			float moveSpeed = unitObject.UnitStat[UnitStatType.MOVEMENT_SPEED] / 10f;
			if (unitObject.UnitStat[UnitStatType.IS_SPRINTING] > 0)
				moveSpeed *= 2f; // TODO: 스프린트 속도 하드코딩 — 2026-03-28. KarmoDDrine

			return moveSpeed;
		}
	}
}
