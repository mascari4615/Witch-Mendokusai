namespace WitchMendokusai.DomainSDK.Aspect
{
	/// <summary>
	/// 처치에서 봉인된 두루마리가 떨어지는지. 처치 드롭 표 (한 번에 하나) 와 따로 굴려 심장이나 재료와 서로 빼앗지 않음.
	/// 확률은 백분율. roll 은 0 이상 1 미만 무작위 두 개 (떨어지는지, 어느 종류인지). 시험에서 고정하려고 밖에서 받음
	/// </summary>
	public static class ScrollDropRules
	{
		public static bool TryDrop(bool isBoss, bool isElite, ScrollDropChances chances, double dropRoll, double kindRoll, out bool isPaper)
		{
			// 보스이면서 정예인 처치는 보스 확률 하나만
			float percent = isBoss ? chances.BossPercent : isElite ? chances.ElitePercent : 0f;
			isPaper = kindRoll < chances.PaperShare;
			return dropRoll * 100.0 < percent;
		}
	}

	public readonly struct ScrollDropChances
	{
		public ScrollDropChances(float elitePercent, float bossPercent, float paperShare)
		{
			ElitePercent = elitePercent;
			BossPercent = bossPercent;
			PaperShare = paperShare;
		}

		public float ElitePercent { get; }
		public float BossPercent { get; }

		/// <summary>떨어질 때 종이 두루마리일 몫 (0 에서 1). 나머지는 번갈아</summary>
		public float PaperShare { get; }
	}
}
