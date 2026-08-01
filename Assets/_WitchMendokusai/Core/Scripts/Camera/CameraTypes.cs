namespace WitchMendokusai
{
	public enum ContentCameraMode
	{
		None = -1,

		Normal = 0,
		Dungeon = 1,

		// SimCity Phase 1 (TASK-WM-164): 도시 부감 시점. append-only (Arena 는 다음 값 — session-bus 합의).
		CityView = 2,

		// 마계 투기장 (TASK-WM-165): 아레나 관전(자유 궤도 orbit + 선수 포커스). append-only.
		Arena = 3,

		// 마을 경영 자유비행 (TASK-WM-193): 카메라만 6DOF 독립(아바타 지상 정지). 전용 컨트롤러가 vcam 직접구동.
		// 부감(심시티식)은 CityView(=2, SimCity Phase1 시드) 재활용 — 별도 enum 불필요. append-only.
		FreeFly = 4,

		// 특수시공 개척 (TASK-WM-194): 유한 무대를 내려다보는 부감. append-only.
		// ★ 게임 속 게임도 *진입한 순간 그 게임이 주체*다 — 본편 화면 위에 카메라를 덧대는 방식은
		//   "밑에서 본편이 계속 돌고 있는" 상태라 화면·좌표 기준이 두 개로 갈라진다(데미지 숫자가
		//   숨은 본편 카메라로 투영되던 버그가 그 증상). 그래서 개척도 투기장도 *정식 content 카메라*다.
		TowerDefense = 5,
	}

	/// <summary>
	/// 게임 모드 → content 카메라 **단일 매핑 정본**.
	///
	/// ★ 왜 필요한가 (TASK-WM-194 실측): 모드 컨트롤러들이 각자 CameraManager 를 찔러댔고,
	///   그중 하나(BuildManager)가 *자기 모드가 아니면 무조건 Normal 로* 되돌렸다. 구독 순서상
	///   나중에 도는 쪽이 이기므로, 개척이 카메라를 개척으로 바꿔도 곧바로 Normal 로 덮여
	///   **화면이 전혀 안 바뀌었다**(개척 vcam 은 무대로 이동해 있는데 승격이 안 됨).
	///   "누가 카메라를 정하는가"를 한 곳으로 모으면 이 종류의 덮어쓰기가 구조적으로 불가능해진다.
	/// </summary>
	public static class GameModeCamera
	{
		public static ContentCameraMode For(GameMode mode)
		{
			return mode switch
			{
				GameMode.Arena => ContentCameraMode.Arena,
				GameMode.TowerDefense => ContentCameraMode.TowerDefense,
				// 건설/지대/도로/발전소 = 본편 위에서 하는 작업이라 평소 시점 유지.
				_ => ContentCameraMode.Normal,
			};
		}
	}

	public enum UICameraMode
	{
		None = -1,

		NPC = 0,

		Tab = 20,
	}
}
