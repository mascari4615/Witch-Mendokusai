namespace WitchMendokusai.DomainSDK.Workshop
{
	/// <summary>
	/// TASK-WM-170 Phase 0 — 듀얼루프 단계 전환 상태. 순수 POCO (DomainSDK, MonoBehaviour 무관).
	/// Phase 전환 = Day → Night → Day(다음날 인덱스+1). Dave the Diver 공식의 낮↔밤 교대를 추상화.
	///
	/// 본격 슬라이스에선 TimeManager.OnTimeOfDayChanged 같은 기존 시간 시스템이 Advance() 호출.
	/// 여긴 순수 상태기 — 트리거(누가 Advance 호출하는가)는 Domain/Core 책임.
	/// </summary>
	public sealed class DayNightCycle
	{
		public DayNightPhase Phase { get; private set; }

		/// <summary>0-based 누계 일수. 매 Night→Day 전환 시 +1 (1사이클 닫힘 카운터).</summary>
		public int DayIndex { get; private set; }

		public DayNightCycle()
			: this(DayNightPhase.Day, 0)
		{
		}

		public DayNightCycle(DayNightPhase startPhase, int startDayIndex)
		{
			Phase = startPhase;
			DayIndex = startDayIndex;
		}

		public void Advance()
		{
			if (Phase == DayNightPhase.Day)
			{
				Phase = DayNightPhase.Night;
			}
			else
			{
				Phase = DayNightPhase.Day;
				DayIndex = DayIndex + 1;
			}
		}
	}
}
