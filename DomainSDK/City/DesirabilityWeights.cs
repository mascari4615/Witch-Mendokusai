namespace WitchMendokusai
{
	// 욕망도(desirability) 히트맵 가중치 — 전역 수요 / 도로 접근 / 전력 각 기여도. 순수 값
	// (CityMetricField.Desirability 에 주입 → 테스트 격리). 게임 배선에선 CityPaintManager SerializeField
	// 가 공급원(수치 노출 룰 — Yon 이 놀이처럼 tweak). RciDemandCoefficients 동형 — 디폴트값 없음(공급자 명시).
	public readonly struct DesirabilityWeights
	{
		public readonly float DemandWeight; // 전역 존 수요(RciDemand)의 기여 가중
		public readonly float RoadWeight;   // 도로 인접(접근성)의 기여 가중
		public readonly float PowerWeight;  // 전력 공급의 기여 가중

		public DesirabilityWeights(float demandWeight, float roadWeight, float powerWeight)
		{
			DemandWeight = demandWeight;
			RoadWeight = roadWeight;
			PowerWeight = powerWeight;
		}
	}
}
