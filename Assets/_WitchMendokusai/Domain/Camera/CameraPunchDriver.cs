using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// CameraManager 당김의 시점 잡이. 처음 당길 때 CameraManager 가 자기 오브젝트에 붙임
	/// - LateUpdate 순서 50: 각 카메라 (MCamera, 순서 0) 의 거리 갱신 뒤, CameraManager (순서 100) 의 최종 계산 앞
	/// - Update 에서 지난 프레임에 얹은 값을 회수해 다른 쪽이 원래 값을 읽게 함
	/// </summary>
	[DefaultExecutionOrder(50)]
	public class CameraPunchDriver : MonoBehaviour
	{
		private CameraManager cameraManager;

		public void Bind(CameraManager owner) => cameraManager = owner;

		private void Update() => cameraManager.RemoveZoomPunches();

		private void LateUpdate() => cameraManager.ApplyZoomPunches(Time.unscaledDeltaTime);

		private void OnDisable() => cameraManager.RemoveZoomPunches();
	}
}
