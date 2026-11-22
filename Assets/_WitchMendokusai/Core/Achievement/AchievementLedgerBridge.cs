namespace WitchMendokusai
{
	/// <summary>
	/// 업적 장부 등록 자리. Domain (QuestManager) 이 등록, 플랫폼 어댑터가 읽음.
	/// 플랫폼 어셈블리가 Domain 을 참조하지 않게 Core 에 둔 다리 (XxxBridge.Register 패턴)
	/// </summary>
	public static class AchievementLedgerBridge
	{
		public static IAchievementLedger Current { get; private set; }

		public static void Register(IAchievementLedger ledger) => Current = ledger;

		public static void Unregister(IAchievementLedger ledger)
		{
			if (Current == ledger)
			{
				Current = null;
			}
		}
	}
}
