namespace WitchMendokusai
{
	// RCI 수요식 계수 — 순수 값 (RciDemandModel 에 주입 → 테스트 격리). 게임 배선에선 RciDemandSO
	// (Domain, DataSO) 가 공급원(수치 노출 룰 — Yon 이 놀이처럼 tweak). POCO 라 디폴트값 없음 —
	// 공급자가 명시(하드코딩 회피, 같은 수치 두 곳 박기 X).
	public readonly struct RciDemandCoefficients
	{
		public readonly float ResidentsPerJob;     // 일자리 1칸이 부양하는 주민 수
		public readonly float ShopsPerResident;    // 주민 1명당 필요 상업 칸
		public readonly float IndustryPerResident; // 주민 1명당 필요 산업 칸
		public readonly float ImmigrationBaseline; // 외부 이주 기반 주거 수요(빈 도시 부트스트랩 — ExportBaseline 의 주거 대응)
		public readonly float ExportBaseline;      // 외부 수출 기반 산업 수요(빈 도시 부트스트랩)
		public readonly float DemandGain;          // gap → 수요 정규화 scale

		public RciDemandCoefficients(float residentsPerJob, float shopsPerResident, float industryPerResident, float immigrationBaseline, float exportBaseline, float demandGain)
		{
			ResidentsPerJob = residentsPerJob;
			ShopsPerResident = shopsPerResident;
			IndustryPerResident = industryPerResident;
			ImmigrationBaseline = immigrationBaseline;
			ExportBaseline = exportBaseline;
			DemandGain = demandGain;
		}
	}
}
