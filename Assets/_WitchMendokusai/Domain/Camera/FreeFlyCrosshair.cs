using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 자유비행(FreeFly) 카메라 모드일 때만 화면 중앙 조준점을 표시 — 시선 고정 기준점(멀미 완화). TASK-WM-193.
	///
	/// 본 컴포넌트는 항상 활성인 부모에 두고, 점 비주얼(<see cref="crosshairVisual"/>)만 토글한다.
	/// (visual 자기 자신을 끄면 Update 가 멈춰 다시 못 켜는 문제 회피.)
	/// </summary>
	public class FreeFlyCrosshair : MonoBehaviour
	{
		[Tooltip("중앙 조준점 비주얼 (Canvas 자식 Image). 자유비행 모드에서만 활성.")]
		[SerializeField] private GameObject crosshairVisual;

		private void Update()
		{
			bool show = CameraManager.Instance != null
				&& CameraManager.Instance.CurrentContentMode == ContentCameraMode.FreeFly;

			if (crosshairVisual != null && crosshairVisual.activeSelf != show)
				crosshairVisual.SetActive(show);
		}
	}
}
