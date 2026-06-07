using UnityEngine;

namespace WitchMendokusai
{
	public class LookAtScreenCenter : MonoBehaviour
	{
		// 스크린 스페이스 중앙을 바라보도록 한다.
		// 이때 회전값은 정반대 방향으로 이루어진다.

		private void Update()
		{
			// Camera.main 은 로딩 중/카메라 부재(네트워크 프록시 등) 시 null → 매 프레임 NRE 플러드였음.
			// 가드: 카메라 없으면 스킵(빌보드 불가 = 무해, 카메라 등장 시 재개). TASK-WM-191 멀티 프록시서 발견.
			Camera mainCamera = Camera.main;
			if (mainCamera == null)
			{
				return;
			}
			transform.LookAt(mainCamera.transform.position);
			transform.Rotate(0, 180, 0);
		}
	}
}