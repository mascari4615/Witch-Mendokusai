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

		public Vector3 MoveDirection { get; private set; }
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
			TryMove();
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

		private void FixedUpdate()
		{
			if (GameManager.Instance.Conditions[GameConditionType.IsChatting] || UIManager.Instance.CurOverlay != MPanelType.None)
				return;

			if (GameManager.Instance.Conditions[GameConditionType.IsChatting])
			{
				playerRigidBody.linearVelocity = Vector3.zero;
				return;
			}

			Vector3 moveDirection = MoveDirection;
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

		public void TeleportTo(Vector3 targetPos)
		{
			transform.position = targetPos;
		}

		private void TryMove()
		{
			float h = Input.GetAxisRaw("Horizontal");
			float v = Input.GetAxisRaw("Vertical");

			if (h == 0)
				h = SOManager.Instance.JoystickX.RuntimeValue;
			if (v == 0)
				v = SOManager.Instance.JoystickY.RuntimeValue;

			// moveDirection.x = h;
			// moveDirection.z = v;
			Vector3 moveDirection = (h * transform.right) + (v * transform.forward);
			moveDirection = moveDirection.normalized;
			MoveDirection = moveDirection;

			if (h != 0 || v != 0)
				UpdateLookDirection(moveDirection);
		}

		private void UpdateLookDirection(Vector3 newDirection)
		{
			// Debug.Log(newDirection);
			float h = Input.GetAxisRaw("Horizontal");

			LookDirection = newDirection;
			playerSprite.flipX = h == 0 ? playerSprite.flipX : h < 0;
		}
	}
}