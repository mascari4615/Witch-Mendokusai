namespace WitchMendokusai
{
	public enum GameConditionType
	{
		IsPaused = 1 << 0,
		// 텍스트 입력 중 (chat OR dev console 등) — 게임 입력 차단용 단일 게이트
		IsTyping = 1 << 1,

		IsMouseOnUI = 1 << 2,

		IsPlayerCasting = 1 << 3,
		IsDied = 1 << 4,

		IsBuilding = 1 << 5,
		IsInTransition = 1 << 6,
		IsViewingUI = 1 << 7, // 전체화면 UI를 보는 중

		// TASK-WM-165 item9 — 투기장 관전 중 (GameMode.Arena). 플레이어 이동/전투 입력 게이트.
		IsSpectating = 1 << 8,

		// TASK-WM-194 — 특수시공 개척(TD) 모드 중 (GameMode.TowerDefense). IsSpectating 과 동형 —
		// 플레이어가 이 모드엔 존재하지 않으므로 이동/시점 축 게이트. 1 << 9 = 타 TASK 예약 슬롯.
		IsTowerDefenseMode = 1 << 10,
	}
}
