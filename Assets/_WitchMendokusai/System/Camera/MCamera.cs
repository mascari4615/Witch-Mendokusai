using UnityEngine;
using Unity.Cinemachine;

[RequireComponent(typeof(CinemachineCamera))]
public class MCamera : MonoBehaviour
{
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

		originalZoom = currentZoom = positionComposer.CameraDistance;
	}

	public void Zoom()
	{
		float amount = -Input.mouseScrollDelta.y * zoomSpeed * Time.deltaTime;
		currentZoom = Mathf.Clamp(currentZoom + amount, minZoom, maxZoom);
		positionComposer.CameraDistance = currentZoom;
	}

	public void ResetCameraDistance()
	{
		positionComposer.CameraDistance = originalZoom;
	}
}