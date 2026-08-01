namespace WitchMendokusai
{
	/// <summary> 특수시공 개척(TD) 진행 국면. Prepare(건설) ↔ Assault(웨이브) 왕복 후 Concluded. </summary>
	public enum TowerDefensePhase
	{
		Prepare = 0,   // 건설 페이즈 — 자원으로 채집건물/타워 배치. 제한시간 후 자동 개시.
		Assault = 1,   // 웨이브 진행 중 — 적 전멸까지.
		Concluded = 2, // 승패 확정.
	}

	/// <summary> TD 매치 결과. </summary>
	public enum TowerDefenseOutcome
	{
		InProgress = 0,
		Victory = 1, // 규정 웨이브 전부 격퇴.
		Defeat = 2,  // 코어 파괴.
	}

	/// <summary>
	/// 코어가 셸에게 알리는 이번 틱의 상태 전이. 셸(MonoBehaviour)이 스폰/UI/정리를 이 신호로만 수행 →
	/// 진행 규칙은 전부 순수 코어에 남고 셸은 actuation 만 (EditMode 로 규칙 전량 검증 가능).
	/// </summary>
	public enum TowerDefenseSignal
	{
		None = 0,
		WaveStarted = 1, // 셸: 이번 웨이브 적 스폰 → ConfirmWaveSpawned 호출.
		WaveCleared = 2, // 셸: 정산 연출 + 다음 건설 페이즈 UI.
		Victory = 3,
		Defeat = 4,
	}
}
