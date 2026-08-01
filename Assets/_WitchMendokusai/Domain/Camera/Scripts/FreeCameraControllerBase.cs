using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 아바타 추종 궤도를 벗어난 자유 위치 카메라 공통 베이스 (CityView 부감 / FreeFly 자유비행). TASK-WM-193.
	///
	/// 1인칭 카메라처럼 vcam transform 을 직접구동(<see cref="Transform.SetPositionAndRotation"/>) — Cinemachine
	/// Follow/positionComposer 체인 우회(jitter 근절, <see cref="MCamera"/> positionComposer = null 전제).
	/// 자기 <see cref="ContentCameraMode"/> 가 활성일 때만 LateUpdate 에서 <see cref="Drive"/>. 비활성 프레임은 no-op.
	///
	/// 모드 식별은 같은 GameObject 의 <see cref="MCamera"/> 단일 출처에서 read — 두 곳 박기 회피(수치 노출 룰).
	/// </summary>
	[RequireComponent(typeof(MCamera))]
	public abstract class FreeCameraControllerBase : MonoBehaviour
	{
		[Header("공통 — 가속 (Ctrl)")]
		[Tooltip("Ctrl 누를 때 이동 속도 배수 (캐릭터 sprint 직관).")]
		[SerializeField] protected float boostMultiplier = 3f;

		private MCamera cachedCamera;

		/// <summary>같은 GameObject 의 MCamera (lazy — init-order 안전).</summary>
		protected MCamera Camera => cachedCamera != null ? cachedCamera : (cachedCamera = GetComponent<MCamera>());

		protected ContentCameraMode ContentCameraMode => Camera.ContentCameraMode;

		protected InputManager InputManager => WitchMendokusai.InputManager.Instance;

		/// <summary>이 컨트롤러의 모드가 현재 활성 content 카메라인지.</summary>
		protected bool IsActiveMode =>
			CameraManager.Instance != null
			&& CameraManager.Instance.CurrentContentMode == ContentCameraMode;

		/// <summary>Ctrl 누름 시 boostMultiplier, 아니면 1. 이동 속도에 곱함.</summary>
		protected float SpeedMultiplier => InputManager != null && InputManager.IsCameraBoost ? boostMultiplier : 1f;

		protected virtual void LateUpdate()
		{
			if (IsActiveMode == false)
				return;
			if (InputManager == null)
				return;

			Drive();
		}

		/// <summary>활성 프레임마다 vcam transform 직접구동 — 서브클래스가 거동 구현.</summary>
		protected abstract void Drive();
	}
}
