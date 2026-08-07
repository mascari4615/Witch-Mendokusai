namespace WitchMendokusai.DomainSDK.Workshop
{
	/// <summary>
	/// TASK-WM-170 Phase 0 — 듀얼루프(낮 마계 / 밤 공방)의 현재 단계. 순수 enum (DomainSDK).
	/// 낮 = 채집·전투(WM-165 투기장/탐험 재활용 입력원), 밤 = 공방 운영(제조·판매).
	/// 본격 슬라이스에선 TimeManager / WorldClock 의 시각이 이쪽으로 매핑.
	/// </summary>
	public enum DayNightPhase
	{
		Day = 0,
		Night = 1,
	}
}
