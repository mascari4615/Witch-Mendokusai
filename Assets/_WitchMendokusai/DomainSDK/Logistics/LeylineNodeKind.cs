namespace WitchMendokusai
{
	// 레이라인 거점의 의미적 역할. 게이트(공급) → 공방(소비) 가 Phase 0 기본 흐름,
	// Relay 는 중간 환승 (긴 거리를 짧은 엣지로 잘게 나눠 깔 때).
	// None = sentinel (RoadType 패턴 답습, 데드 인터페이스 0 필드 선제 추가 회피 — 향후
	// Storage/Junction 등 first-use 시 확장).
	public enum LeylineNodeKind
	{
		None = -1,

		Source = 0,
		Sink = 1,
		Relay = 2,
	}
}
