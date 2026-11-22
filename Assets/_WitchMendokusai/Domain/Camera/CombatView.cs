using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 생존 던전 전투 시야의 앞쪽 경계. 카메라가 낮게 기울어 (30도) 앞쪽이 지평선까지 보여도,
	/// 적 생성과 반납, 화면 처치와 얼음, 장식 그리기의 "화면 안" 판정은 카메라 앞 이 깊이까지만.
	/// 경계 밖은 안개로 가림. 깊이와 안개 값의 주인은 CameraManager (던전 카메라일 때 매 프레임)
	/// </summary>
	public static class CombatView
	{
		// GeometryUtility.CalculateFrustumPlanes 의 순서: 왼, 오른, 아래, 위, 가까운, 먼
		private const int FAR_PLANE_INDEX = 5;

		/// <summary>카메라 앞 방향 깊이 (m). 0 이면 경계 없음 (카메라 원래 절두체).</summary>
		public static float MaxDepth { get; private set; }

		public static void SetMaxDepth(float depth) => MaxDepth = Mathf.Max(0f, depth);

		/// <summary>카메라 절두체 평면 6개. 경계가 있으면 먼 평면을 그 깊이로 당긴다.</summary>
		public static void CalculatePlanes(Camera camera, Plane[] planes)
		{
			GeometryUtility.CalculateFrustumPlanes(camera, planes);
			if (MaxDepth <= 0f || MaxDepth >= camera.farClipPlane)
				return;
			Transform cameraTransform = camera.transform;
			Vector3 forward = cameraTransform.forward;
			// 안쪽이 양수 쪽. 먼 평면의 안쪽은 카메라 쪽 (-forward)
			planes[FAR_PLANE_INDEX] = new Plane(-forward, cameraTransform.position + forward * MaxDepth);
		}
	}
}
