using UnityEngine;
using Unity.Cinemachine;

namespace WitchMendokusai
{
	[RequireComponent(typeof(CinemachineCamera))]
	public class MCamera : MonoBehaviour
	{
		// TODO: 둘 중 하나만 보이도록 Editor 스크립트 작성
		[field: SerializeField] public ContentCameraMode ContentCameraMode { get; private set; }
		[field: SerializeField] public UICameraMode UICameraMode { get; private set; }

		[field: SerializeField] public CinemachineBlendDefinition.Styles BlendStyle { get; private set; }
		[field: SerializeField] public CinemachineCamera CinemachineCamera { get; private set; }
		[field: SerializeField] private CinemachinePositionComposer positionComposer;

		[SerializeField] private float minZoom = 2f;
		[SerializeField] private float maxZoom = 10f;
		[Tooltip("스크롤 한 단위당 target 거리 변화량 (Time.deltaTime 없음).")]
		[SerializeField] private float zoomWheelSensitivity = 3f;
		[Tooltip("줌이 목표 거리에 닿을 때까지 걸리는 대략적 시간(초). 0에 가깝게 하면 거의 즉시.")]
		[SerializeField] private float zoomSmoothTime = 0.12f;

		private float targetZoom;
		private float zoomSmoothVelocity;

		[SerializeField] private bool adjustPitchWithZoom;
		[SerializeField] private float pitchAtFarZoom = 25f;
		[SerializeField] private float pitchAtNearZoom = 0;
		[Tooltip("가까운 줌일 때 Target Offset에 더함 (Y: 위로 등). 멀면 0, 가까우면 전체.")]
		[SerializeField] private Vector3 targetOffsetBonusAtNear;

		private float originalZoom;
		private float currentZoom;
		private Vector3 originalLocalEuler;
		private Vector3 targetOffsetBase;
		private Vector3 originalDamping;
		private bool initialized;
		private float originalFieldOfView;
		public OrbitCameraProfile OrbitProfile { get; private set; }

		public void SetOrbitProfile(OrbitCameraProfile profile)
		{
			Init();
			OrbitProfile = profile;
			if (profile == null && positionComposer != null)
			{
				positionComposer.Damping = originalDamping;
				positionComposer.TargetOffset = targetOffsetBase;
				CinemachineCamera.Lens.FieldOfView = originalFieldOfView;
				ResetCameraDistance();
			}
		}

		private void Awake()
		{
			Init();
		}

		private void LateUpdate()
		{
			if (positionComposer == null)
				return;

			if (OrbitProfile != null)
			{
				float blend = OrbitProfile.BlendTime <= 0f ? 1f : 1f - Mathf.Exp(-Time.deltaTime / OrbitProfile.BlendTime);
				positionComposer.CameraDistance = Mathf.Lerp(positionComposer.CameraDistance, OrbitProfile.Distance, blend);
				positionComposer.TargetOffset = Vector3.Lerp(positionComposer.TargetOffset, OrbitProfile.TargetOffset, blend);
				positionComposer.Damping = OrbitProfile.FollowDamping;
				CinemachineCamera.Lens.FieldOfView = Mathf.Lerp(CinemachineCamera.Lens.FieldOfView, OrbitProfile.FieldOfView, blend);
				Vector3 angles = transform.localEulerAngles;
				transform.localRotation = Quaternion.Euler(Mathf.LerpAngle(angles.x, OrbitProfile.Pitch, blend), angles.y, angles.z);
				return;
			}

			if (zoomSmoothTime > 0.0001f)
				currentZoom = Mathf.SmoothDamp(currentZoom, targetZoom, ref zoomSmoothVelocity, zoomSmoothTime, Mathf.Infinity, Time.deltaTime);
			else
			{
				currentZoom = targetZoom;
				zoomSmoothVelocity = 0f;
			}

			if (adjustPitchWithZoom)
				ApplyZoomFraming(originalLocalEuler.y, originalLocalEuler.z);
			else
				positionComposer.CameraDistance = currentZoom;
		}

		public void Init()
		{
			if (positionComposer == null || initialized)
				return;
			initialized = true;

			originalZoom = currentZoom = targetZoom = positionComposer.CameraDistance;
			originalDamping = positionComposer.Damping;
			originalFieldOfView = CinemachineCamera.Lens.FieldOfView;
			targetOffsetBase = positionComposer.TargetOffset;
			zoomSmoothVelocity = 0f;
			Vector3 euler0 = transform.localEulerAngles;

			if (adjustPitchWithZoom)
			{
				targetOffsetBase = positionComposer.TargetOffset;
				ApplyZoomFraming(euler0.y, euler0.z);
			}

			originalLocalEuler = transform.localEulerAngles;
		}

		public void Zoom()
		{
			if (positionComposer == null)
				return;

			if (InputManager.TryGetExistingInstance(out InputManager inputManager) == false)
				return;

			if (inputManager.IsZoomModifierHeld == false)
				return;

			float step = -inputManager.ScrollWheelDelta * zoomWheelSensitivity;
			targetZoom = Mathf.Clamp(targetZoom + step, minZoom, maxZoom);
		}

		// 기본 내려다보는 각 (도) 바꾸기. ResetCameraDistance 가 되돌리는 값도 같이. 개발자 창 시험용
		public void SetBasePitch(float pitch)
		{
			Init();
			originalLocalEuler.x = pitch;
			Vector3 angles = transform.localEulerAngles;
			transform.localEulerAngles = new Vector3(pitch, angles.y, angles.z);
		}

		public float BasePitch => originalLocalEuler.x;

		/// <summary> 손으로 맞춘 거리. 궤도 설정을 거둔 뒤 되돌릴 때 씀 </summary>
		public float ZoomTarget
		{
			get => targetZoom;
			set => targetZoom = Mathf.Clamp(value, minZoom, maxZoom);
		}

		public void ResetCameraDistance()
		{
			if (positionComposer == null)
				return;

			targetZoom = originalZoom;
			currentZoom = originalZoom;
			zoomSmoothVelocity = 0f;

			if (adjustPitchWithZoom)
				ApplyZoomFraming(originalLocalEuler.y, originalLocalEuler.z);
			else
			{
				positionComposer.CameraDistance = originalZoom;
				transform.localEulerAngles = originalLocalEuler;
			}
		}

		private void ApplyZoomFraming(float preserveY, float preserveZ)
		{
			float t = Mathf.InverseLerp(maxZoom, minZoom, currentZoom);
			positionComposer.CameraDistance = currentZoom;
			positionComposer.TargetOffset = targetOffsetBase + Vector3.Lerp(Vector3.zero, targetOffsetBonusAtNear, t);
			float pitchX = Mathf.LerpAngle(pitchAtFarZoom, pitchAtNearZoom, t);
			transform.localEulerAngles = new Vector3(pitchX, preserveY, preserveZ);
		}
	}
}
