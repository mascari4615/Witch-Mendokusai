using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	public class PlayerRotation : MonoBehaviour
	{
		[SerializeField] private bool useRotate = true;
		[SerializeField] private Transform meshPivotOf3DModel;
		[SerializeField] private float rotateSmoothTime = 0.1f;

		private const float ROTATE_SPEED = 150;
		private const float CAMERA_ROTATE_SPEED = 15;
		private float yRotation = 0;
		private Vector3 lastMoveDir = Vector3.forward;
		private Rigidbody unitRigidbody;

		private void Awake()
		{
			unitRigidbody = GetComponent<Rigidbody>();
		}

		private void Update()
		{
			if (useRotate == false)
				return;

			RotateCamera();
			RotateMesh();
		}

		private void RotateCamera()
		{
			// CameraRotateInput 은 InputManager 가 axis 게이트(IsTyping 등) 적용 후 노출. 회전 입력 없으면 0.
			yRotation += Time.deltaTime * ROTATE_SPEED * InputManager.Instance.CameraRotateInput;

			Quaternion targetRotation = Quaternion.Euler(0, yRotation, 0);
			// isKinematic Rigidbody의 회전은 Rigidbody API로. transform.rotation 직접 set은
			// Motor가 queue한 MovePosition과 충돌해 위치 갱신을 무효화할 수 있다 (Q/E + W 멈춤, 공중 정지 버그 원인).
			unitRigidbody.MoveRotation(targetRotation);
			Camera.main.transform.parent.rotation = Quaternion.Lerp(Camera.main.transform.parent.rotation, targetRotation, Time.deltaTime * CAMERA_ROTATE_SPEED);
		}

		private void RotateMesh()
		{
			// 1. 이동 방향
			Vector3 moveDirectionLocal = PlayerProvider.Instance.CurrentObject.UnitMovement.MoveDirectionLocal;
			float h = moveDirectionLocal.x;
			float v = moveDirectionLocal.z;

			// 2. 카메라 기준으로 변환
			{
				// 카메라의 y축만 반영
				Camera cam = Camera.main;
				Vector3 camForward = cam.transform.forward;
				camForward.y = 0;
				camForward.Normalize();
				Vector3 camRight = cam.transform.right;
				camRight.y = 0;
				camRight.Normalize();
				Vector3 moveDir = (camForward * v + camRight * h).normalized;
				lastMoveDir = moveDir;
			}

			// 3. 목표 각도 계산
			if (lastMoveDir.sqrMagnitude > 0.0001f)
			{
				Quaternion targetRot = Quaternion.LookRotation(lastMoveDir, Vector3.up);
				// 4. 부드럽게 회전 (Slerp)
				meshPivotOf3DModel.rotation = Quaternion.Slerp(meshPivotOf3DModel.rotation, targetRot, 1 - Mathf.Exp(-Time.deltaTime / rotateSmoothTime));
			}
		}
	}
}