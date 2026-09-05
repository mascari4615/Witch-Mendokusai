namespace WitchMendokusai.DomainSDK.Refining
{
	// POCO 디폴트값 없음 — 게임 배선은 RefiningCoefficientsSO 가 공급(수치 노출 룰).
	// Careful 의 품질·온기 델타가 Fast 보다 + 쪽이어야 "효율 vs 윤리" 가 의미 있는 선택이 된다.
	public readonly struct RefiningCoefficients
	{
		public readonly float InitialQuality;
		public readonly float FastQualityDelta;
		public readonly float CarefulQualityDelta;
		public readonly float FastWarmthDelta;
		public readonly float CarefulWarmthDelta;

		public RefiningCoefficients(float initialQuality, float fastQualityDelta, float carefulQualityDelta, float fastWarmthDelta, float carefulWarmthDelta)
		{
			InitialQuality = initialQuality;
			FastQualityDelta = fastQualityDelta;
			CarefulQualityDelta = carefulQualityDelta;
			FastWarmthDelta = fastWarmthDelta;
			CarefulWarmthDelta = carefulWarmthDelta;
		}
	}
}
