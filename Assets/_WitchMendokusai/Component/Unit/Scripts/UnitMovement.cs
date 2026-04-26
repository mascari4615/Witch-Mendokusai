using System;
using System.Collections;
using UnityEngine;

namespace WitchMendokusai
{
	public class UnitMovement : MonoBehaviour
	{
		// Ground detection
		[SerializeField] private float groundCheckDistance = 0.25f;
		[SerializeField] private LayerMask groundLayerMask;

		// Cached components
		protected Rigidbody unitRigidBody;
		protected UnitObject unitObject;
		private UnitJumpModule jumpModule;

		// Events
		public event Action<float> OnLanded;

		// Runtime properties
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
			if (TryGetComponent(out jumpModule))
			{
				jumpModule.Setup(unitRigidBody, unitObject);
				jumpModule.OnLanded += HandleLanded;
			}
			unitRigidBody.useGravity = true;
		}

		private void OnEnable()
		{
			UpdateLookDirection(Vector3.right);
			if (jumpModule != null)
				jumpModule.ResetState(IsGrounded());
			else
				unitObject.UnitStat[UnitStatType.IS_JUMPING] = 0;

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

		private void OnDestroy()
		{
			if (jumpModule != null)
				jumpModule.OnLanded -= HandleLanded;
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
			if (IsMovementBlocked())
				return;

			Vector3 moveDirection = MoveDirectionWorld;
			float verticalVelocity = unitRigidBody.linearVelocity.y;
			bool isGrounded = IsGrounded();

			if (jumpModule != null)
			{
				jumpModule.Step(ref isGrounded, ref verticalVelocity, CanUseJumpState(), MoveTick);
				unitObject.UnitStat[UnitStatType.IS_JUMPING] = (!isGrounded && verticalVelocity > 0f) ? 1 : 0;
			}
			else
			{
				unitObject.UnitStat[UnitStatType.IS_JUMPING] = 0;
			}

			unitRigidBody.linearVelocity = BuildFinalVelocity(moveDirection, verticalVelocity);
			// unitRigidBody.AddForce(finalVelocity, ForceMode.VelocityChange);
		}

		private bool IsMovementBlocked()
		{
			return GameManager.Instance.Conditions[GameConditionType.IsTyping] ||
				TimeManager.Instance.IsPaused;
		}

		private Vector3 BuildFinalVelocity(Vector3 moveDirection, float verticalVelocity)
		{
			if (unitObject.UnitStat[UnitStatType.DEAD] > 0)
				return Vector3.zero;

			float horizontalSpeed = GetHorizontalSpeed();
			return new Vector3(
				moveDirection.x * horizontalSpeed,
				verticalVelocity,
				moveDirection.z * horizontalSpeed
			);
		}

		private float GetHorizontalSpeed()
		{
			if (unitObject.UnitStat[UnitStatType.FORCE_MOVE] > 0)
				return SOManager.Instance.DashSpeed.RuntimeValue;

			float moveSpeed = unitObject.UnitStat[UnitStatType.MOVEMENT_SPEED] / 10f;
			if (unitObject.UnitStat[UnitStatType.IS_SPRINTING] > 0)
				moveSpeed *= 2f; // TODO: 스프린트 속도 하드코딩함 - 2026-03-28. KarmoDDrine

			return moveSpeed;
		}

		public void TryJump()
		{
			if (jumpModule == null)
				return;

			if (GameManager.Instance.Conditions[GameConditionType.IsTyping] ||
				TimeManager.Instance.IsPaused)
				return;

			jumpModule.RequestJump(CanUseJumpState());
		}

		public void StopJump()
		{
			if (jumpModule == null)
				return;

			jumpModule.ReleaseJump();
		}

		private bool CanUseJumpState()
		{
			if (unitObject.UnitStat[UnitStatType.DEAD] > 0)
				return false;

			if (unitObject.UnitStat[UnitStatType.FORCE_MOVE] > 0)
				return false;

			return true;
		}

		private void HandleLanded(float impactStrength)
		{
			OnLanded?.Invoke(impactStrength);
		}

		private bool IsGrounded()
		{
			Vector3 origin = transform.position + Vector3.up * 0.1f;
			float distance = groundCheckDistance + 0.1f;

			// Use layer-based detection if explicitly set, otherwise use component-based detection
			if (groundLayerMask.value != 0)
			{
				return Physics.Raycast(origin, Vector3.down, distance, groundLayerMask, QueryTriggerInteraction.Ignore);
			}

			// Component-based detection: look for GroundSurface
			if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, distance, ~0, QueryTriggerInteraction.Ignore))
			{
				GroundSurface surface = hit.collider.GetComponent<GroundSurface>();
				if (surface != null && surface.IsWalkable)
					return true;
			}

			return false;
		}
	}
}