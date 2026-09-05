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

		/// <summary>
		/// 그 던전이 지금 열려 있나. 스킬 던전은 스킬 재료가 아직 없어 닫혀 있음 (economy.md 표 2)
		///
		/// ★ 화면이 이유를 말하려면 여닫힘과 입장권을 따로 물어야 함. 입장권이 0 인 것과
		///   아직 안 만든 것은 사람에게 다른 말
		/// </summary>
		public static bool IsOpen(IdleDungeonKind kind)
		{
			return kind != IdleDungeonKind.Skill;
		}

		/// <summary>
		/// 한 판 입장 (economy.md 표 2). 입장권 한 장을 쓰고 그 던전 보상을 줌
		///
		/// ★ 무작위 없음. 사람이 누를 때만 도는 자리지만 보상까지 굴리면 저장을 껐다 켜서
		///   다시 뽑는 길이 생김. 던전은 <b>고정 보상</b>이고 재미는 어디를 갈지 고르는 데 둠
		/// ★ 골드는 지금 초당 수입에 견줌. 단계가 오르면 던전도 같이 커져야 늘 갈 이유가 생김
		/// </summary>
		public static bool TryEnter(IdleState state, IdleTuning tuning, IdleDungeonKind kind, out IdleDungeonReward reward)
		{
			reward = default;

			if (IsOpen(kind) == false || TrySpend(state, kind) == false)
			{
				return false;
			}

			double gold = 0d;
			long shards = 0L;
			int gear = 0;
			int tier = IdleDrops.MaxTierAt(state.Stage, state.Ascensions, tuning);

			switch (kind)
			{
				case IdleDungeonKind.Gold:
					gold = IdleModel.IncomePerSecond(state, tuning) * tuning.DungeonGoldSeconds;
					state.Resource += gold;
					break;
				case IdleDungeonKind.Boss:
					shards = tuning.DungeonBossShards > 0L ? tuning.DungeonBossShards : 0L;
					state.PrestigeShards += shards;
					gear = IdleGear.Stow(state, tuning, tier, tuning.DungeonBossGear);
					break;
				case IdleDungeonKind.Gear:
					gear = IdleGear.Stow(state, tuning, tier, tuning.DungeonGearCount);
					break;
			}

			reward = new IdleDungeonReward(kind, 1, gold, shards, gear);
			return true;
		}

		/// <summary>
		/// 남은 입장권을 한 번에 쓴다 (소탕). 한 판씩 들어간 것과 결과가 같아야 함
		///
		/// ★ 가방이 차면 장비는 그만 들어오지만 골드와 조각은 계속 들어옴. 한 판씩 눌렀을 때와 같음
		/// </summary>
		public static bool TrySweep(IdleState state, IdleTuning tuning, IdleDungeonKind kind, out IdleDungeonReward reward)
		{
			reward = new IdleDungeonReward(kind, 0, 0d, 0L, 0);

			int runs = 0;
			double gold = 0d;
			long shards = 0L;
			int gear = 0;

			while (TryEnter(state, tuning, kind, out IdleDungeonReward one))
			{
				runs++;
				gold += one.Gold;
				shards += one.Shards;
				gear += one.Gear;
			}

			if (runs == 0)
			{
				return false;
			}

			reward = new IdleDungeonReward(kind, runs, gold, shards, gear);
			return true;
		}
	}

	/// <summary>던전 한 번(또는 소탕 한 번)이 준 것. 화면이 그대로 적는다</summary>
	public readonly struct IdleDungeonReward
	{
		public IdleDungeonReward(IdleDungeonKind kind, int runs, double gold, long shards, int gear)
		{
			Kind = kind;
			Runs = runs;
			Gold = gold;
			Shards = shards;
			Gear = gear;
		}

		public IdleDungeonKind Kind { get; }

		/// <summary>몇 판을 돌았나. 소탕이면 한 번에 여러 판</summary>
		public int Runs { get; }

		public double Gold { get; }

		public long Shards { get; }

		/// <summary>가방에 실제로 들어간 장비 수. 가방이 차면 준 것보다 적다</summary>
		public int Gear { get; }
	}
}
