namespace WitchMendokusai.DomainSDK.Life
{
	/// <summary>
	/// 4호(플레이어)의 노동 지시 — "이 주민, 이 일을 해" override. 순수 값(DomainSDK).
	/// 자율-우선: 평소엔 지시 없음(주민이 <see cref="WorkProfile.DefaultWork"/> 수행), 4호가 *원할 때만* 박는다.
	/// <see cref="RemainingMinutes"/> 가 0 이하로 떨어지면 만료 → 자율 복귀(<see cref="WorkSelector"/> 가 판정).
	/// </summary>
	public readonly struct WorkAssignment
	{
		public readonly WorkKind RequestedWork;
		public readonly int RemainingMinutes;

		public WorkAssignment(WorkKind requestedWork, int remainingMinutes)
		{
			RequestedWork = requestedWork;
			RemainingMinutes = remainingMinutes;
		}

		/// <summary>아직 유효한 지시인가 — 남은 시간이 있으면.</summary>
		public bool IsActive => RemainingMinutes > 0;

		/// <summary>시간 경과 — 남은 시간을 줄인 새 지시(struct 라 새 값 반환). 만료 시 IsActive=false.</summary>
		public WorkAssignment Tick(int minutes) => new WorkAssignment(RequestedWork, RemainingMinutes - minutes);
	}
}
