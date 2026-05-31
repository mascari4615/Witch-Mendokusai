namespace WitchMendokusai.DomainSDK.Refining
{
	/// <summary>
	/// TASK-WM-172 Phase 0 — 정련 체인 누적 상태. Quality(0..1) · Warmth(-1..1) · CompletedStages.
	/// readonly struct — 단계마다 새 RefiningState 반환(determinism / aliasing 0). RciDemand 선례.
	///
	/// Quality = 마도서 페이지 재료 등급(0=원자재 / 1=최고급). 누적 +.
	/// Warmth  = 마을 온기 게이지 기여(-1=함부로 누적 / 0=중립 / +1=애도 누적). 부호 양분.
	/// </summary>
	public readonly struct RefiningState
	{
		public readonly float Quality;
		public readonly float Warmth;
		public readonly int CompletedStages;

		public RefiningState(float quality, float warmth, int completedStages)
		{
			Quality = quality;
			Warmth = warmth;
			CompletedStages = completedStages;
		}
	}
}
