namespace WitchMendokusai
{
	// DI-managed EffectRunner 가 주입 deps 를 담아 POCO Effect 에 전달 (TASK-WM-107 Slice 2A).
	// POCO Effect 가 static Bridge/매니저를 직접 안 알도록 — 진짜 DI 정신.
	// 확장 지점: 후속 Slice 에서 IPublisher / spawner 등 추가 (SO/Data/Player/Pool Bridge 흡수).
	public class EffectContext
	{
		public SOManager SOManager { get; }

		public EffectContext(SOManager soManager)
		{
			SOManager = soManager;
		}
	}
}
