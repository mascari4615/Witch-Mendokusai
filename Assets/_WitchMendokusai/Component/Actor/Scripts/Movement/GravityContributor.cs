using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// Airborne 시 중력 가속도를 vertical velocity에 누적.
	/// Grounded일 때는 누적 X (떨어지는 동안에만 적용).
	/// γ3에서 slope sliding 도입 시 GroundState 분기 추가 예정.
	/// </summary>
	public class GravityContributor : IVelocityContributor
	{
		public void Contribute(MotorContext context, float deltaTime)
		{
			if (context.GroundState == MotorGroundState.Grounded)
			{
				// 지면 위에서 negative vertical velocity 잔존 방지 — 다음 tick gravity 누적이 의미를 갖도록
				if (context.Velocity.y < 0f)
					context.Velocity.y = 0f;
				return;
			}

			context.Velocity.y += Physics.gravity.y * deltaTime;
		}
	}
}
