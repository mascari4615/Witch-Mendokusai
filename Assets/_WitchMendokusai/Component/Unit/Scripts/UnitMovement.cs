using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	public class UnitMovement : MonoBehaviour
	{
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

			if (unitObject.UnitStat[UnitStatType.DEAD] > 0)
				finalVelocity = Vector3.zero;
			else if (unitObject.UnitStat[UnitStatType.FORCE_MOVE] > 0)
				finalVelocity = moveDirection * SOManager.Instance.DashSpeed.RuntimeValue;
			else
				finalVelocity = moveDirection * (unitObject.UnitStat[UnitStatType.MOVEMENT_SPEED] / 10f);

			unitRigidBody.linearVelocity = finalVelocity;
			// unitRigidBody.AddForce(finalVelocity, ForceMode.VelocityChange);
		}
	}
}