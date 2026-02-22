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

		private const float zoomSpeed = 100f;
		[SerializeField] private float minZoom = 2f;
		[SerializeField] private float maxZoom = 10f;

		private float originalZoom;
		private float currentZoom;

		private void Awake()
		{
			Init();
		}

		public void Init()
		{
			// CinemachineCamera = GetComponent<CinemachineCamera>();
			// positionComposer = GetComponent<CinemachinePositionComposer>();

			if (positionComposer == null)
				return;

			originalZoom = currentZoom = positionComposer.CameraDistance;
		}

		public void Zoom()
		{
			if (positionComposer == null)
				return;

			float amount = -Input.mouseScrollDelta.y * zoomSpeed * Time.deltaTime;
			currentZoom = Mathf.Clamp(currentZoom + amount, minZoom, maxZoom);
			positionComposer.CameraDistance = currentZoom;
		}

		public void ResetCameraDistance()
		{
			if (positionComposer == null)
				return;

			positionComposer.CameraDistance = originalZoom;
		}
	}
}