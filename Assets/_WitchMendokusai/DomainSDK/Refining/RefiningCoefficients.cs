namespace WitchMendokusai.DomainSDK.Refining
{
	/// <summary>
	/// TASK-WM-172 Phase 0 — 정련 체인 계수. RefiningChain 에 주입(테스트 격리). 게임 배선에선 RefiningCoefficientsSO
	/// (Domain, DataSO)가 공급원(수치 노출 룰 — 욘이 공방을 tweak). POCO 라 디폴트값 없음 — 공급자가 명시.
	///
	/// 빠름 vs 정성의 비대칭이 코어: Careful 가 Fast 보다 품질 델타↑, 온기 델타도 +쪽. Fast 는 빠르지만 온기 -.
	/// "효율 vs 윤리"가 단순 손익이 아니라 톤 정합 — 수치 자체에 비대칭이 박혀 있어야 의미 있는 선택이 된다.
	/// </summary>
	public readonly struct RefiningCoefficients
	{
		public readonly float InitialQuality;      // 잔재 원자재 시작 품질(0..1). 마계 사체 = 낮음 / 정화된 잔재 = 높음.
		public readonly float FastQualityDelta;    // Fast 단계 1회의 품질 가산(작은 +). 빠르지만 깊이가 얕다.
		public readonly float CarefulQualityDelta; // Careful 단계 1회의 품질 가산(큰 +). 정성이 등급을 끌어올림.
		public readonly float FastWarmthDelta;     // Fast 단계 1회의 온기 가산(음수). 함부로 다룸 = 온기 소진.
		public readonly float CarefulWarmthDelta;  // Careful 단계 1회의 온기 가산(양수). 애도 = 온기 충전.

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
