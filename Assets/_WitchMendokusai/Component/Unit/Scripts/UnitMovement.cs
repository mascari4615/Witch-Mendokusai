using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	public class UnitMovement : MonoBehaviour
	{
		[SerializeField] private float jumpForce = 5.6f;
		[SerializeField] private float fallGravityMultiplier = 2.2f;
		[SerializeField] private float lowJumpGravityMultiplier = 3.1f;
		[SerializeField] private float coyoteTime = 0.1f;
		[SerializeField] private float jumpBufferTime = 0.12f;
		[SerializeField] private float groundCheckDistance = 0.25f;
		[SerializeField] private LayerMask groundLayerMask;
		private bool isJumpHeld;
		private float coyoteTimer;
		private float jumpBufferTimer;

		protected Rigidbody unitRigidBody;
		protected UnitObject unitObject;

		public float MoveTick { get; set; } = 0.02f;
		// public Vector3 Destination { get; set; } = Vector3.zero;

		public Vector3 MoveDirectionLocal { get; private set; }
		public Vector3 MoveDirectionWorld { get; private set; }
		public Vector3 LookDirection { get; private set; }
		public bool IsLookingRight => unitObject.SpriteRenderer.flipX == false;

		private void Awake()
		{
			unitRigidBody = GetComponent<Rigidbody>();
			unitObject = GetComponent<UnitObject>();
			unitRigidBody.useGravity = true;
		}

		private void OnEnable()
		{
			UpdateLookDirection(Vector3.right);

			StartCoroutine(MoveCoroutine());
		}

		private IEnumerator MoveCoroutine()
		{
			WaitForSeconds wait = new(MoveTick);

			while (true)
			{
				// SetDestination
				Move();
				yield return wait;
			}
		}

		private void OnDisable()
		{
			StopAllCoroutines();
		}

		public void SetMoveDirection(Vector3 input) => SetMoveDirection(new Vector2(input.x, input.z));
		public void SetMoveDirection(Vector2 input)
		{
			float h = input.x;
			float v = input.y;

			// if (h == 0)
			// 	h = SOManager.Instance.JoystickX.RuntimeValue;
			// if (v == 0)
			// 	v = SOManager.Instance.JoystickY.RuntimeValue;

			// moveDirection.x = h;
			// moveDirection.z = v;
			MoveDirectionLocal = new Vector3(h, 0, v).normalized;
			MoveDirectionWorld = ((h * transform.right) + (v * transform.forward)).normalized;

			unitObject.SpriteRenderer.flipX = (h == 0) ? unitObject.SpriteRenderer.flipX : (h < 0);

			if (h != 0 || v != 0)
				UpdateLookDirection(MoveDirectionWorld);
		}

		private void UpdateLookDirection(Vector3 newDirection)
		{
			LookDirection = newDirection;
		}

		private void Move()
		{
			if (GameManager.Instance.Conditions[GameConditionType.IsChatting] ||
				TimeManager.Instance.IsPaused)
				return;

			Vector3 moveDirection = MoveDirectionWorld;
			Vector3 finalVelocity;
			float currentVerticalVelocity = unitRigidBody.linearVelocity.y;
			bool isGrounded = IsGrounded();
			coyoteTimer = isGrounded ? coyoteTime : Mathf.Max(0f, coyoteTimer - MoveTick);
			jumpBufferTimer = Mathf.Max(0f, jumpBufferTimer - MoveTick);
			unitObject.UnitStat[UnitStatType.IS_JUMPING] = (!isGrounded && currentVerticalVelocity > 0f) ? 1 : 0;

			if (CanUseJumpState() && jumpBufferTimer > 0f && coyoteTimer > 0f)
			{
				ExecuteJump();
				isGrounded = false;
				currentVerticalVelocity = unitRigidBody.linearVelocity.y;
			}

			if (!isGrounded)
			{
				float gravityMultiplier = 1f;
				if (currentVerticalVelocity < 0f)
					gravityMultiplier = fallGravityMultiplier;
				else if (currentVerticalVelocity > 0f && !isJumpHeld)
					gravityMultiplier = lowJumpGravityMultiplier;

				if (gravityMultiplier > 1f)
					currentVerticalVelocity += Physics.gravity.y * (gravityMultiplier - 1f) * MoveTick;
			}

			if (unitObject.UnitStat[UnitStatType.DEAD] > 0)
				finalVelocity = Vector3.zero;
			else if (unitObject.UnitStat[UnitStatType.FORCE_MOVE] > 0)
				finalVelocity = new Vector3(
					moveDirection.x * SOManager.Instance.DashSpeed.RuntimeValue,
					currentVerticalVelocity,
					moveDirection.z * SOManager.Instance.DashSpeed.RuntimeValue
				);
			else
			{
				float moveSpeed = unitObject.UnitStat[UnitStatType.MOVEMENT_SPEED] / 10f;
				if (unitObject.UnitStat[UnitStatType.IS_SPRINTING] > 0)
					moveSpeed *= 2f; // TODO: 스프린트 속도 하드코딩함 - 2026-03-28. KarmoDDrine
				finalVelocity = new Vector3(
					moveDirection.x * moveSpeed,
					currentVerticalVelocity,
					moveDirection.z * moveSpeed
				);
			}

			unitRigidBody.linearVelocity = finalVelocity;
			// unitRigidBody.AddForce(finalVelocity, ForceMode.VelocityChange);
		}

		public void TryJump()
		{
			if (GameManager.Instance.Conditions[GameConditionType.IsChatting] ||
				TimeManager.Instance.IsPaused)
				return;

			if (!CanUseJumpState())
				return;

			jumpBufferTimer = jumpBufferTime;
			isJumpHeld = true;
		}

		public void StopJump()
		{
			isJumpHeld = false;
		}

		private bool CanUseJumpState()
		{
			if (unitObject.UnitStat[UnitStatType.DEAD] > 0)
				return false;

			if (unitObject.UnitStat[UnitStatType.FORCE_MOVE] > 0)
				return false;

			return true;
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

		private bool IsGrounded()
		{
			if (groundLayerMask.value == 0)
				groundLayerMask = LayerMask.GetMask("GROUND");

			Vector3 origin = transform.position + Vector3.up * 0.1f;
			return Physics.Raycast(origin, Vector3.down, groundCheckDistance + 0.1f, groundLayerMask, QueryTriggerInteraction.Ignore);
		}
	}
}