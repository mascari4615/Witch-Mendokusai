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

	public enum UICameraMode
	{
		None = -1,

		NPC = 0,

		Tab = 20,
	}
}
