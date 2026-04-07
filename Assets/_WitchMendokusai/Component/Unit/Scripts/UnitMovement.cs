using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	public class UnitMovement : MonoBehaviour
	{
		[SerializeField] private float jumpForce = 7f;
		[SerializeField] private float groundCheckDistance = 0.25f;
		[SerializeField] private LayerMask groundLayerMask;

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
			unitObject.UnitStat[UnitStatType.IS_JUMPING] = (!isGrounded && currentVerticalVelocity > 0f) ? 1 : 0;

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

			if (unitObject.UnitStat[UnitStatType.DEAD] > 0)
				return;

			if (unitObject.UnitStat[UnitStatType.FORCE_MOVE] > 0)
				return;

			if (!IsGrounded())
				return;

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