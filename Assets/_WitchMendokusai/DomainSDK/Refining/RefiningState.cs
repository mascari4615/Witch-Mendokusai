namespace WitchMendokusai.DomainSDK.Refining
{
	// Quality [0..1] = 마도서 페이지 재료 등급, Warmth [-1..1] = 마을 온기 기여(부호 양분).
	// readonly struct → 단계마다 새 인스턴스 반환(aliasing 0, 결정성).
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
