namespace WitchMendokusai.DomainSDK.Life
{
	/// <summary>
	/// 주민이 "지금 무슨 일을 할지" 고르는 순수 함수 코어 (DomainSDK, 결정적, EditMode 직접 테스트).
	/// <see cref="ActivitySelector"/> 의 노동판 형제 — 단, 욕구는 여기서 안 본다.
	/// 욕구 우선 게이트(배고프면 일 멈추고 먹으러 감)는 호출자(LifeAgent)가 ActivitySelector 를 먼저 돌려
	/// 결핍이 없을 때만 WorkSelector 를 부르는 것으로 보장 — 압박 없음 = 욕구가 노동을 항상 이김.
	/// </summary>
	public static class WorkSelector
	{
		/// <summary>
		/// 할 일 — ① 4호 지시(<paramref name="assignment"/>)가 유효하면 그것 ② 없으면 자율(<see cref="WorkProfile.DefaultWork"/>).
		/// 지시가 만료(RemainingMinutes ≤ 0)면 자율로 복귀 — 프로액티브 기본 지시 없음(자율-우선 정신).
		/// </summary>
		public static WorkKind SelectWork(WorkProfile profile, WorkAssignment? assignment)
		{
			if (assignment.HasValue && assignment.Value.IsActive)
			{
				return assignment.Value.RequestedWork;
			}

			return profile.DefaultWork;
		}
	}
}
