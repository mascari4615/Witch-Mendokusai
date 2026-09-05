using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 마인크래프트 크리에이티브식 자유비행 카메라 — 카메라만 6DOF 독립 (아바타는 지상 정지). TASK-WM-193.
	///
	/// 마우스로 시선(yaw/pitch), WASD/MoveInput 으로 시선 기준 수평 이동, (2단계)Space/Shift 로 상하.
	/// 충돌 무시(자유 통과), 공중 정지. 아바타 입력은 카메라 모드 진입 시 별도로 게이트(2단계).
	/// </summary>
	public class FreeFlyCameraController : FreeCameraControllerBase
	{
		[Header("자유비행 — 이동")]
		[Tooltip("WASD 시선 기준 수평 이동 속도 (m/sec).")]
		[SerializeField] private float moveSpeed = 12f;
		[Tooltip("Space/Shift 상하 이동 속도 (m/sec).")]
		[SerializeField] private float verticalSpeed = 8f;

		[Header("자유비행 — 시선")]
		[Tooltip("마우스 1픽셀당 회전량 (deg).")]
		[SerializeField] private float lookSensitivity = 0.15f;
		[Tooltip("pitch 하한 (위로 본 한계, 음수 = 위).")]
		[SerializeField] private float minPitch = -85f;
		[Tooltip("pitch 상한 (아래로 본 한계).")]
		[SerializeField] private float maxPitch = 85f;

		private Vector3 position;
		private float yaw;
		private float pitch;
		private bool initialized;

		protected override void Drive()
		{
			if (initialized == false)
			{
				position = transform.position;
				Vector3 euler = transform.eulerAngles;
				pitch = NormalizePitch(euler.x);
				yaw = euler.y;
				initialized = true;
			}

			// 마우스 = 시선 (yaw/pitch)
			Vector2 look = InputManager.LookDelta;
			yaw += look.x * lookSensitivity;
			pitch += -look.y * lookSensitivity;
			pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

			Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);

			// WASD = 시선 기준 수평 이동 (카메라 전용 축 — 플레이어 Move 와 분리)
			Vector2 move = InputManager.CameraMoveInput;
			Vector3 forward = rotation * Vector3.forward;
			Vector3 right = rotation * Vector3.right;
			position += (forward * move.y + right * move.x) * moveSpeed * SpeedMultiplier * Time.deltaTime;

			// Space/Shift = 월드 상하 이동 (시선 무관, 절대 up)
			position += Vector3.up * InputManager.CameraVerticalInput * verticalSpeed * SpeedMultiplier * Time.deltaTime;

			transform.SetPositionAndRotation(position, rotation);
		}

		/// <summary>eulerAngles.x 는 0~360 — pitch clamp 위해 -180~180 으로 정규화.</summary>
		private static float NormalizePitch(float eulerX)
		{
			return eulerX > 180f ? eulerX - 360f : eulerX;
		}
	}
}
