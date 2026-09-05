namespace WitchMendokusai
{
	// DI-managed EffectRunner 가 주입 deps 를 담아 POCO Effect 에 전달 (TASK-WM-107 Slice 2A).
	// POCO Effect 가 static Bridge/매니저를 직접 안 알도록 — 진짜 DI 정신. CriteriaContext 대칭.
	// Slice 2A SOManager · 3-1 PlayerProvider/ObjectPoolManager · 3-2 DataManager (순환=owner-push).
	// CriteriaContext 와는 후속 cleanup slice 에서 단일 GameplayContext 로 통합 (지금 조기 통합 = churn).
	public class EffectContext
	{
		public SOManager SOManager { get; }
		public PlayerProvider PlayerProvider { get; }
		public ObjectPoolManager ObjectPoolManager { get; }
		public DataManager DataManager { get; }

		public EffectContext(SOManager soManager, PlayerProvider playerProvider, ObjectPoolManager objectPoolManager, DataManager dataManager)
		{
			SOManager = soManager;
			PlayerProvider = playerProvider;
			ObjectPoolManager = objectPoolManager;
			DataManager = dataManager;
		}
	}
}
