using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	public class PlayerRotation : MonoBehaviour
	{
		[SerializeField] private bool useRotate = true;
		[SerializeField] private Transform meshPivotOf3DModel;
		[SerializeField] private float rotateSmoothTime = 0.1f;

		// TASK-WM-163 — 카메라 yaw/pitch 회전 = CameraManager 단일 권위자로 이관.
		// PlayerRotation 은 body 를 camera yaw 에 맞추고, mesh 를 이동 방향으로만 회전.
		private Vector3 lastMoveDir = Vector3.forward;
		private Rigidbody unitRigidbody;

		private PlayerProvider playerProvider;

		private void Awake()
		{
			unitRigidbody = GetComponent<Rigidbody>();
		}

		[Inject]
		public void Construct(PlayerProvider playerProvider)
		{
			this.playerProvider = playerProvider;
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
			// 카메라 yaw 는 CameraManager 가 소유 — body 만 그 yaw 에 정렬 (이동 기준축 유지).
			if (CameraManager.TryGetExistingInstance(out CameraManager cameraManager) == false)
				return;

			unitRigidbody.MoveRotation(cameraManager.FlatYawRotation);
		}

		private void RotateMesh()
		{
			Vector3 moveDirectionLocal = playerProvider.CurrentObject.UnitMovement.MoveDirectionLocal;
			float h = moveDirectionLocal.x;
			float v = moveDirectionLocal.z;

			{
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

			if (lastMoveDir.sqrMagnitude > 0.0001f)
			{
				Quaternion targetRot = Quaternion.LookRotation(lastMoveDir, Vector3.up);
				meshPivotOf3DModel.rotation = Quaternion.Slerp(meshPivotOf3DModel.rotation, targetRot, 1 - Mathf.Exp(-Time.deltaTime / rotateSmoothTime));
			}
		}
	}
}
