using UnityEngine;
using VContainer;

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

		private InputManager inputManager;
		private PlayerProvider playerProvider;

		private void Awake()
		{
			unitRigidbody = GetComponent<Rigidbody>();
		}

		[Inject]
		public void Construct(InputManager inputManager, PlayerProvider playerProvider)
		{
			this.inputManager = inputManager;
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
			yRotation += Time.deltaTime * ROTATE_SPEED * inputManager.CameraRotateInput;

			Quaternion targetRotation = Quaternion.Euler(0, yRotation, 0);
			unitRigidbody.MoveRotation(targetRotation);
			Camera.main.transform.parent.rotation = Quaternion.Lerp(Camera.main.transform.parent.rotation, targetRotation, Time.deltaTime * CAMERA_ROTATE_SPEED);
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
