namespace WitchMendokusai.DomainSDK.Idle
{
	/// <summary>던전 4종 (economy.md 4). 무엇을 얻으러 가나로 나뉜다</summary>
	public enum IdleDungeonKind
	{
		/// <summary>재화 던전. 골드</summary>
		Gold = 0,

		/// <summary>보스 던전. 환생 조각과 장비</summary>
		Boss = 1,

		/// <summary>장비 던전. 장비 (부위 고정)</summary>
		Gear = 2,

		/// <summary>스킬 던전. 스킬 재료</summary>
		Skill = 3,
	}

	/// <summary>
	/// 던전 입장권 (economy.md 3, 4). 재화가 아니라 <b>하루 몇 번</b> 이라는 울타리.
	///
	/// ★ 재화로 만들면 모아 두었다 몰아 쓰는 것이 늘 정답이 되어 매일 들어올 이유가 사라짐.
	///   그래서 날이 바뀌면 <b>상한까지 채우고 끝</b>, 안 쓴 날치는 안 쌓임
	///
	/// ★ 날 경계는 <c>DayResetOffsetSeconds</c> 로 옮긴다. 자정에 끊으면 아직 노는 사람이 하루를
	///   두 번 겪는다. 수집형이 새벽에 끊는 이유 (기본값은 KST 05:00, 판정 대기)
	///
	/// ★ 판정에 실시각을 쓰는 유일한 층. 나머지는 전부 흐른 초로 돈다. 그래서 여기만
	///   <c>nowUnixSeconds</c> 를 받고, 오프라인 정산과 같은 자리에서 부른다 (IdleSession.CatchUp)
	/// </summary>
	public static class IdleDungeons
	{
		/// <summary>던전 수. 화면과 시험이 이 수로 돈다</summary>
		public const int COUNT = 4;

		private const long SECONDS_PER_DAY = 86400L;

		/// <summary>
		/// 그 시각이 속한 날 번호. 경계를 <c>offset</c> 만큼 뒤로 민 셈
		///
		/// ★ 음수 초(1970 이전)도 아래로 내림. C# 나눗셈은 0 쪽으로 자르므로 그대로 쓰면
		///   경계 하나가 두 배로 길어짐
		/// </summary>
		public static long DayIndexOf(long unixSeconds, long offsetSeconds)
		{
			long shifted = unixSeconds - offsetSeconds;
			long day = shifted / SECONDS_PER_DAY;

			if (shifted < 0L && shifted % SECONDS_PER_DAY != 0L)
			{
				day -= 1L;
			}

			return day;
		}

		/// <summary>
		/// 날이 바뀌었으면 입장권을 상한까지. 같은 날이면 아무 일도 없음
		///
		/// ★ 첫 판(마지막 채운 날이 없음)도 채움. 안 그러면 시작하자마자 하루를 기다려야 하는 판
		/// </summary>
		public static void Refill(IdleState state, IdleTuning tuning, long nowUnixSeconds)
		{
			state.EnsureTicketRoom();

			long today = DayIndexOf(nowUnixSeconds, tuning.DayResetOffsetSeconds);

			if (state.TicketDay == today)
			{
				return;
			}

			state.TicketDay = today;

			for (int index = 0; index < state.Tickets.Length; index++)
			{
				state.Tickets[index] = tuning.TicketsPerDay;
			}
		}

		/// <summary>남은 입장권</summary>
		public static long TicketsOf(IdleState state, IdleDungeonKind kind)
		{
			state.EnsureTicketRoom();

			int index = (int)kind;
			return index >= 0 && index < state.Tickets.Length ? state.Tickets[index] : 0L;
		}

		/// <summary>입장권 한 장을 쓴다. 없으면 아무 일도 안 일어난다</summary>
		public static bool TrySpend(IdleState state, IdleDungeonKind kind)
		{
			state.EnsureTicketRoom();

			int index = (int)kind;

			if (index < 0 || index >= state.Tickets.Length || state.Tickets[index] <= 0L)
			{
				return false;
			}

			state.Tickets[index] -= 1L;
			return true;
		}

		/// <summary>다음 채워지기까지 남은 초. 화면이 날짜 계산을 다시 하지 않게</summary>
		public static double SecondsUntilRefill(IdleState state, IdleTuning tuning, long nowUnixSeconds)
		{
			long today = DayIndexOf(nowUnixSeconds, tuning.DayResetOffsetSeconds);

			if (state.TicketDay != today)
			{
				return 0d;
			}

			long nextBoundary = (today + 1L) * SECONDS_PER_DAY + tuning.DayResetOffsetSeconds;
			return nextBoundary - nowUnixSeconds;
		}
	}
}
