namespace WitchMendokusai
{
	/// <summary>
	/// 세계의 시간이 흐르는 규칙 (TASK-WM-217 단계 4 준비).
	///
	/// ★ 왜 판정 층인가: 지금은 시계가 <b>FishNet 호스트</b>에 매달려 있다(WorldClockNetworkBridge).
	///   MMO 에선 「내가 없어도 세계의 밤이 온다」여야 하므로 시계는 <b>서버가 굴려야</b> 한다 —
	///   서버가 유니티가 아니려면 시간 규칙이 엔진 밖에 있어야 한다.
	///
	/// 자릿수(하루 몇 시간·한 계절 며칠·한 해 몇 계절)는 게임의 <c>WorldClockSO</c> 가 정본이고,
	/// 여기는 그 값을 <b>받아서</b> 쓴다 — 같은 수를 두 곳에 적지 않는다.
	/// </summary>
	public sealed class WorldCalendar
	{
		public const int MINUTES_PER_HOUR = 60;

		private float minuteRemainder;

		public WorldCalendar(int hoursPerDay, int daysPerSeason, int seasonsPerYear, int startHour = 0, int startMinute = 0)
		{
			HoursPerDay = hoursPerDay > 0 ? hoursPerDay : 24;
			DaysPerSeason = daysPerSeason > 0 ? daysPerSeason : 28;
			SeasonsPerYear = seasonsPerYear > 0 ? seasonsPerYear : 4;

			Year = 1;
			Season = 0;
			Day = 1;
			Hour = startHour;
			Minute = startMinute;
		}

		public int HoursPerDay { get; }
		public int DaysPerSeason { get; }
		public int SeasonsPerYear { get; }

		public int Year { get; private set; }
		public int Season { get; private set; }
		public int Day { get; private set; }
		public int Hour { get; private set; }
		public int Minute { get; private set; }

		/// <summary>
		/// 시간을 흘린다. 소수점 아래는 <b>버리지 않고 모아 둔다</b> —
		/// 버리면 20번/초로 조금씩 흘릴 때 하루가 영영 안 온다.
		/// 자정을 넘었으면 true(하루가 바뀌는 순간에 걸리는 일들이 있다).
		/// </summary>
		public bool AdvanceMinutes(float minutes)
		{
			if (minutes <= 0f)
				return false;

			minuteRemainder += minutes;
			int whole = (int)minuteRemainder;
			if (whole <= 0)
				return false;

			minuteRemainder -= whole;

			int startDay = TotalDays();
			ApplyMinutes(whole);
			return TotalDays() != startDay;
		}

		/// <summary>기억에서 되살린다 — 자릿수를 벗어난 값은 접어 넣는다(망가진 파일이 세계를 깨지 못한다).</summary>
		public void Set(int year, int season, int day, int hour, int minute)
		{
			Year = year > 0 ? year : 1;
			Season = Wrap(season, SeasonsPerYear);
			Day = Wrap(day - 1, DaysPerSeason) + 1;
			Hour = Wrap(hour, HoursPerDay);
			Minute = Wrap(minute, MINUTES_PER_HOUR);
			minuteRemainder = 0f;
		}

		/// <summary>세계가 시작한 뒤 몇 분 지났나 — 「얼마나 흘렀나」를 재는 자리.</summary>
		public int TotalMinutes() => (TotalDays() * HoursPerDay + Hour) * MINUTES_PER_HOUR + Minute;

		/// <summary>세계가 시작한 뒤 며칠 지났나 — 「하루가 바뀌었나」를 재는 데 쓴다.</summary>
		public int TotalDays() => ((Year - 1) * SeasonsPerYear + Season) * DaysPerSeason + (Day - 1);

		private static int Wrap(int value, int size)
		{
			if (size <= 0)
				return 0;

			int wrapped = value % size;
			return wrapped < 0 ? wrapped + size : wrapped;
		}

		private void ApplyMinutes(int minutesToAdd)
		{
			int newMinute = Minute + minutesToAdd;
			Minute = newMinute % MINUTES_PER_HOUR;

			int hourCarry = newMinute / MINUTES_PER_HOUR;
			if (hourCarry <= 0)
				return;

			int newHour = Hour + hourCarry;
			Hour = newHour % HoursPerDay;

			int dayCarry = newHour / HoursPerDay;
			if (dayCarry <= 0)
				return;

			int newDay = Day + dayCarry;
			Day = ((newDay - 1) % DaysPerSeason) + 1;

			int seasonCarry = (newDay - 1) / DaysPerSeason;
			if (seasonCarry <= 0)
				return;

			int newSeason = Season + seasonCarry;
			Season = newSeason % SeasonsPerYear;
			Year += newSeason / SeasonsPerYear;
		}
	}
}
