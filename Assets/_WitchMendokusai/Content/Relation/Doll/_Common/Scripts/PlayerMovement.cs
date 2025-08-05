using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	public class PlayerMovement : MonoBehaviour
	{
		[SerializeField] private bool useRotate;
		[SerializeField] private SpriteRenderer playerSprite;
		private Rigidbody playerRigidBody;
		private PlayerObject playerObject;

		private const float ROTATE_SPEED = 150;
		private const float CAMERA_ROTATE_SPEED = 15;
		private float yRotation = 0;

		public Vector3 MoveDirectionLocal { get; private set; }
		public Vector3 MoveDirectionWorld { get; private set; }
		public Vector3 LookDirection { get; private set; }

		private void Awake()
		{
			playerRigidBody = GetComponent<Rigidbody>();
			playerObject = GetComponent<PlayerObject>();
		}

		private void Start()
		{
			UpdateLookDirection(Vector3.right);
		}

		private void Update()
		{
			if (GameManager.Instance.Conditions[GameConditionType.IsDashing])
				return;

			Rotate();
			CalcMoveDirection();
		}

		private void FixedUpdate()
		{
			Move();
		}

		private void Rotate()
		{
			if (useRotate == false)
				return;

			if (Input.GetKey(KeyCode.Q))
				yRotation += Time.deltaTime * ROTATE_SPEED;
			if (Input.GetKey(KeyCode.E))
				yRotation -= Time.deltaTime * ROTATE_SPEED;

			Quaternion targetRotation = Quaternion.Euler(0, yRotation, 0);
			// transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, Time.deltaTime * 5);
			transform.rotation = targetRotation;
			Camera.main.transform.parent.rotation = Quaternion.Lerp(Camera.main.transform.parent.rotation, targetRotation, Time.deltaTime * CAMERA_ROTATE_SPEED);
		}

		public void TeleportTo(Vector3 targetPos)
		{
			transform.position = targetPos;
		}

		private void CalcMoveDirection()
		{
			float h = Input.GetAxisRaw("Horizontal");
			float v = Input.GetAxisRaw("Vertical");

			if (h == 0)
				h = SOManager.Instance.JoystickX.RuntimeValue;
			if (v == 0)
				v = SOManager.Instance.JoystickY.RuntimeValue;

			// moveDirection.x = h;
			// moveDirection.z = v;
			MoveDirectionLocal = new Vector3(h, 0, v).normalized;
			MoveDirectionWorld = ((h * transform.right) + (v * transform.forward)).normalized;

			if (h != 0 || v != 0)
				UpdateLookDirection(MoveDirectionWorld);
		}

		private void UpdateLookDirection(Vector3 newDirection)
		{
			// Debug.Log(newDirection);
			float h = Input.GetAxisRaw("Horizontal");

			LookDirection = newDirection;
			playerSprite.flipX = (h == 0) ? playerSprite.flipX : (h < 0);
		}

		private void Move()
		{
			if (GameManager.Instance.Conditions[GameConditionType.IsChatting] ||
				UIManager.Instance.CurPanel != PanelType.None ||
				TimeManager.Instance.IsPaused)
				return;

			Vector3 moveDirection = MoveDirectionWorld;
			Vector3 finalVelocity;

			if (GameManager.Instance.Conditions[GameConditionType.IsDied])
				finalVelocity = Vector3.zero;
			else if (GameManager.Instance.Conditions[GameConditionType.IsDashing])
				finalVelocity = moveDirection * SOManager.Instance.DashSpeed.RuntimeValue;
			else
				finalVelocity = moveDirection * playerObject.UnitStat[UnitStatType.MOVEMENT_SPEED];

			playerRigidBody.linearVelocity = finalVelocity;
			// playerRigidBody.AddForce(finalVelocity, ForceMode.VelocityChange);
		}
	}
}