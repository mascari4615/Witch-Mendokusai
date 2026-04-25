using UnityEngine;
using UnityEngine.InputSystem;
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

		private void Awake()
		{
			Init();
		}

		private void LateUpdate()
		{
			if (positionComposer == null)
				return;

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
			if (positionComposer == null)
				return;

			originalZoom = currentZoom = targetZoom = positionComposer.CameraDistance;
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

			if (Keyboard.current == null || Keyboard.current.ctrlKey.isPressed == false)
				return;

			float step = -(Mouse.current != null ? Mouse.current.scroll.ReadValue().y : 0f) * zoomWheelSensitivity;
			targetZoom = Mathf.Clamp(targetZoom + step, minZoom, maxZoom);
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
