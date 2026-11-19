using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 궁극기 판이 매 프레임 카메라를 향하게 함
	/// - Upright: 세운 채 좌우로만 돎 (기둥, 꽂힌 검). 아니면 카메라 면과 나란히 (하늘의 달, 떨어지는 카드)
	/// - Roll: 카메라를 향한 채 화면 안에서 기울이는 각 (도). 연출이 매 프레임 바꿈
	/// </summary>
	public sealed class UltimateBillboard : MonoBehaviour
	{
		public bool Upright { get; set; }
		public float Roll { get; set; }

		private void LateUpdate()
		{
			Camera mainCamera = Camera.main;
			if (mainCamera == null)
				return;
			Quaternion facing = Upright ? Quaternion.Euler(0f, mainCamera.transform.eulerAngles.y, 0f) : mainCamera.transform.rotation;
			transform.rotation = facing * Quaternion.Euler(0f, 0f, Roll);
		}
	}
}
