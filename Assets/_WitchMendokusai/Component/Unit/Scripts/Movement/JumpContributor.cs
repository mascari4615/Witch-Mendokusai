using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 점프 contributor — 가변 점프 / coyote time / jump buffer / 착지 impact 통합 처리.
	/// GravityContributor 다음 순서로 등록되어, 점프 임펄스 + 추가 air gravity multiplier를 vertical velocity에 적용한다.
	/// </summary>
	public class JumpContributor : IVelocityContributor
	{
		private readonly UnitObject unitObject;
		private readonly float jumpForce;
		private readonly float fallGravityMultiplier;
		private readonly float lowJumpGravityMultiplier;
		private readonly float coyoteTime;
		private readonly float jumpBufferTime;
		private readonly float landingImpactMinFallSpeed;
		private readonly float landingImpactMaxFallSpeed;

		private bool isJumpHeld;
		private float coyoteTimer;
		private float jumpBufferTimer;
		private bool wasGrounded;
		private float lastAirborneFallSpeed;

		// UnitMovement가 매 tick 끝에서 소비하는 일회성 신호
		private bool hasPendingLanded;
		private float pendingLandedImpact;

		public bool IsJumping { get; private set; }
		public bool HasPendingLanded => hasPendingLanded;

		public JumpContributor(
			UnitObject unitObject,
			float jumpForce,
			float fallGravityMultiplier,
			float lowJumpGravityMultiplier,
			float coyoteTime,
			float jumpBufferTime,
			float landingImpactMinFallSpeed,
			float landingImpactMaxFallSpeed)
		{
			this.unitObject = unitObject;
			this.jumpForce = jumpForce;
			this.fallGravityMultiplier = fallGravityMultiplier;
			this.lowJumpGravityMultiplier = lowJumpGravityMultiplier;
			this.coyoteTime = coyoteTime;
			this.jumpBufferTime = jumpBufferTime;
			this.landingImpactMinFallSpeed = landingImpactMinFallSpeed;
			this.landingImpactMaxFallSpeed = landingImpactMaxFallSpeed;
		}

		public void Reset(bool isGrounded)
		{
			wasGrounded = isGrounded;
			lastAirborneFallSpeed = 0f;
			coyoteTimer = isGrounded ? coyoteTime : 0f;
			jumpBufferTimer = 0f;
			isJumpHeld = false;
			IsJumping = false;
			hasPendingLanded = false;
		}

		public void RequestJump()
		{
			if (CanUseJumpState() == false)
				return;
			jumpBufferTimer = jumpBufferTime;
			isJumpHeld = true;
		}

		public void ReleaseJump()
		{
			isJumpHeld = false;
		}

		public float ConsumeLandedImpact()
		{
			hasPendingLanded = false;
			return pendingLandedImpact;
		}

		public void Contribute(MotorContext context, float deltaTime)
		{
			bool isGrounded = context.GroundState == MotorGroundState.Grounded;
			float verticalVelocity = context.Velocity.y;

			coyoteTimer = isGrounded ? coyoteTime : Mathf.Max(0f, coyoteTimer - deltaTime);
			jumpBufferTimer = Mathf.Max(0f, jumpBufferTimer - deltaTime);

			if (isGrounded == false && verticalVelocity < 0f)
				lastAirborneFallSpeed = Mathf.Max(lastAirborneFallSpeed, -verticalVelocity);

			if (isGrounded && wasGrounded == false)
			{
				pendingLandedImpact = Mathf.InverseLerp(landingImpactMinFallSpeed, landingImpactMaxFallSpeed, lastAirborneFallSpeed);
				hasPendingLanded = true;
				lastAirborneFallSpeed = 0f;
				IsJumping = false;
			}

			bool jumpExecutedThisTick = false;
			if (CanUseJumpState() && jumpBufferTimer > 0f && coyoteTimer > 0f)
			{
				jumpBufferTimer = 0f;
				coyoteTimer = 0f;
				verticalVelocity = jumpForce;
				isGrounded = false;
				IsJumping = true;
				jumpExecutedThisTick = true;
			}

			if (isGrounded == false && jumpExecutedThisTick == false)
			{
				// GravityContributor가 이미 base g를 누적함. 여기서는 추가 multiplier만 적용.
				float multiplier = 1f;
				if (verticalVelocity < 0f)
					multiplier = fallGravityMultiplier;
				else if (verticalVelocity > 0f && isJumpHeld == false)
					multiplier = lowJumpGravityMultiplier;

				if (multiplier > 1f)
					verticalVelocity += Physics.gravity.y * (multiplier - 1f) * deltaTime;
			}

			context.Velocity.y = verticalVelocity;
			wasGrounded = isGrounded;
		}

		private bool CanUseJumpState()
		{
			if (unitObject.UnitStat[UnitStatType.DEAD] > 0)
				return false;
			if (unitObject.UnitStat[UnitStatType.FORCE_MOVE] > 0)
				return false;
			return true;
		}
	}
}
