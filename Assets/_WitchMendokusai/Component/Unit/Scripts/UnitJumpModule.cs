using System;
using UnityEngine;

namespace WitchMendokusai
{
	public class UnitJumpModule : MonoBehaviour
	{
		// Jump tuning
		[SerializeField] private float jumpForce = 5.6f;
		[SerializeField] private float fallGravityMultiplier = 2.2f;
		[SerializeField] private float lowJumpGravityMultiplier = 3.1f;
		[SerializeField] private float coyoteTime = 0.1f;
		[SerializeField] private float jumpBufferTime = 0.12f;

		// Landing impact calculation
		[SerializeField] private float landingImpactMinFallSpeed = 1.2f;
		[SerializeField] private float landingImpactMaxFallSpeed = 8f;

		// Runtime state
		private bool isJumpHeld;
		private float coyoteTimer;
		private float jumpBufferTimer;
		private bool wasGrounded;
		private float lastAirborneFallSpeed;

		private Rigidbody unitRigidBody;
		private UnitObject unitObject;

		public event Action<float> OnLanded;

		public void Setup(Rigidbody rigidBody, UnitObject owner)
		{
			unitRigidBody = rigidBody;
			unitObject = owner;
		}

		public void ResetState(bool isGrounded)
		{
			wasGrounded = isGrounded;
			lastAirborneFallSpeed = 0f;
			coyoteTimer = isGrounded ? coyoteTime : 0f;
			jumpBufferTimer = 0f;
			isJumpHeld = false;
		}

		public void RequestJump(bool canUseJumpState)
		{
			if (!canUseJumpState)
				return;

			jumpBufferTimer = jumpBufferTime;
			isJumpHeld = true;
		}

		public void ReleaseJump()
		{
			isJumpHeld = false;
		}

		public void Step(ref bool isGrounded, ref float verticalVelocity, bool canUseJumpState, float moveTick)
		{
			coyoteTimer = isGrounded ? coyoteTime : Mathf.Max(0f, coyoteTimer - moveTick);
			jumpBufferTimer = Mathf.Max(0f, jumpBufferTimer - moveTick);

			if (!isGrounded && verticalVelocity < 0f)
				lastAirborneFallSpeed = Mathf.Max(lastAirborneFallSpeed, -verticalVelocity);

			if (isGrounded && !wasGrounded)
			{
				float impactStrength = Mathf.InverseLerp(landingImpactMinFallSpeed, landingImpactMaxFallSpeed, lastAirborneFallSpeed);
				OnLanded?.Invoke(impactStrength);
				lastAirborneFallSpeed = 0f;
			}

			if (canUseJumpState && jumpBufferTimer > 0f && coyoteTimer > 0f)
			{
				ExecuteJump();
				isGrounded = false;
				verticalVelocity = unitRigidBody.linearVelocity.y;
			}

			if (!isGrounded)
				ApplyAirGravity(ref verticalVelocity, moveTick);

			wasGrounded = isGrounded;
		}

		private void ApplyAirGravity(ref float verticalVelocity, float moveTick)
		{
			float gravityMultiplier = 1f;
			if (verticalVelocity < 0f)
				gravityMultiplier = fallGravityMultiplier;
			else if (verticalVelocity > 0f && !isJumpHeld)
				gravityMultiplier = lowJumpGravityMultiplier;

			if (gravityMultiplier > 1f)
				verticalVelocity += Physics.gravity.y * (gravityMultiplier - 1f) * moveTick;
		}

		private void ExecuteJump()
		{
			jumpBufferTimer = 0f;
			coyoteTimer = 0f;

			Vector3 velocity = unitRigidBody.linearVelocity;
			velocity.y = jumpForce;
			unitRigidBody.linearVelocity = velocity;
			unitObject.UnitStat[UnitStatType.IS_JUMPING] = 1;
		}
	}
}
