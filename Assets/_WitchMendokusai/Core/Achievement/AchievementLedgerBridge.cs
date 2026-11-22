namespace WitchMendokusai
{
	/// <summary>
	/// 업적 장부 등록 자리. Domain (AchievementManager) 이 등록, 플랫폼 어댑터가 읽음.
	/// 플랫폼 어셈블리가 Domain 을 참조하지 않게 Core 에 둔 다리 (XxxBridge.Register 패턴)
	/// </summary>
	public static class AchievementLedgerBridge
	{
		public static IAchievementLedger Current { get; private set; }

		/// <summary>등록할 때마다 오름. 같은 실행 안에서 다시 로드하거나 새 게임이면 장부가 바뀌므로 플랫폼이 이 값을 보고 다시 맞춤</summary>
		public static int Revision { get; private set; }

		public static void Register(IAchievementLedger ledger)
		{
			Current = ledger;
			Revision++;
		}

		public static void Unregister(IAchievementLedger ledger)
		{
			if (Current == ledger)
			{
				Current = null;
			}
		}
	}
}
