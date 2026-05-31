namespace WitchMendokusai.DomainSDK.Refining
{
	/// <summary>
	/// TASK-WM-172 Phase 0 — 잔재 정련의 표준 3단계. 마계 사체·시들어버린 마을의 잔재가 거치는 가공 체인.
	/// Dissection(해부) → 잔재에서 사용 가능한 부위 추출 / Purification(정화) → 저주·오염 제거 /
	/// Refinement(정련) → 마도서 페이지 재료급으로 품질 끌어올리기. 코어 수치 매핑은 RefiningCoefficients SO가 공급.
	/// </summary>
	public enum RefiningStageKind
	{
		Dissection = 0,
		Purification = 1,
		Refinement = 2,
	}
}
