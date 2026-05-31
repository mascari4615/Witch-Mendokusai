namespace WitchMendokusai
{
	// 통근 시민의 하루 상태 (집↔직장 사이클). 6 동기 격상 1단계 = enum.
	// 비전-중립 — 시민이 사역마/언데드/사람인지(스킨)는 무관. INC-7 이동이 이 상태를 전이시킨다.
	public enum CitizenState
	{
		AtHome = 0,
		GoingToWork = 1,
		AtWork = 2,
		GoingHome = 3,
	}
}
