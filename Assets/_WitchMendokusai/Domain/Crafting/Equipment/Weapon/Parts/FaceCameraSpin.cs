using UnityEngine;

namespace WitchMendokusai
{
	// 카메라를 바라보며 제자리에서 돎. 쓰는 곳: 알리사 그림자 표식 표시
	public class FaceCameraSpin : MonoBehaviour
	{
		[SerializeField] private float degreesPerSecond = 160f;

		private void LateUpdate()
		{
			Camera viewCamera = Camera.main;
			if (viewCamera != null)
				transform.rotation = viewCamera.transform.rotation * Quaternion.Euler(0f, 0f, Time.time * degreesPerSecond);
		}
	}
}
