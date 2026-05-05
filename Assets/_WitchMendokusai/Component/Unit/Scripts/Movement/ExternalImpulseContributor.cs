using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 외부 horizontal impulse contributor — Dash / Knockback / 폭발 등 외부 force 채널.
	/// caller가 Push(velocity, duration) 호출 시 duration 동안 horizontal velocity를 덮어쓰고
	/// MotorContext.IsExternallyDriven=true로 표시한다. InputContributor / JumpContributor는 이 플래그를
	/// 보고 자기 기여를 보류 (input 무시 / 점프 차단).
	///
	/// 등록 순서: contributors 리스트 *맨 앞* — Input 보다 먼저 horizontal 채워야 Input 가드가 동작.
	///
	/// 단일 슬롯 모델 — 새 Push는 활성 impulse를 *덮어쓴다* (latest wins). 두 dash가 동시에 활성이 되는
	/// 시나리오는 현재 게임 디자인에 없음. 다중 impulse 합성이 필요해지면 확장.
	/// </summary>
	public class ExternalImpulseContributor : IVelocityContributor
	{
		private Vector3 horizontalVelocity;
		private float remainingTime;

		public bool IsActive => remainingTime > 0f;

		public void Push(Vector3 worldHorizontalVelocity, float duration)
		{
			horizontalVelocity = new Vector3(worldHorizontalVelocity.x, 0f, worldHorizontalVelocity.z);
			remainingTime = Mathf.Max(0f, duration);
		}

		public void Cancel()
		{
			remainingTime = 0f;
		}

		public void Contribute(MotorContext context, float deltaTime)
		{
			if (remainingTime <= 0f)
			{
				context.IsExternallyDriven = false;
				return;
			}

			context.IsExternallyDriven = true;
			context.Velocity.x = horizontalVelocity.x;
			context.Velocity.z = horizontalVelocity.z;
			remainingTime -= deltaTime;
		}
	}
}
