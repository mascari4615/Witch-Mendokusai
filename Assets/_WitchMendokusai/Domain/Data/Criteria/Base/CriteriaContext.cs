namespace WitchMendokusai
{
	// DI-managed caller 가 주입 deps 를 담아 POCO Criteria 에 전달 (TASK-WM-107 Slice 2C).
	// POCO Criteria 가 static Bridge/매니저를 직접 안 알도록 — 진짜 DI 정신. EffectContext 대칭.
	// 확장 지점: 후속 Slice 에서 PlayerProvider / DataManager 추가 (Stat/GameStat/DungeonStat Bridge 흡수).
	// EffectContext 와는 후속 cleanup slice 에서 단일 GameplayContext 로 통합 (지금 조기 통합 = churn).
	public class CriteriaContext
	{
		public SOManager SOManager { get; }

		public CriteriaContext(SOManager soManager)
		{
			SOManager = soManager;
		}
	}
}
