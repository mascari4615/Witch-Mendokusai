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
	}
}
