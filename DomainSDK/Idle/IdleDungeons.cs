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
	/// 던전 입장권 (economy.md 3, 4) 과 던전 안 판 (changes/idle-dungeon-run v2)
	///
	/// ★ 입장권은 재화가 아니라 <b>하루 몇 번</b> 이라는 울타리. 재화로 만들면 모아 두었다 몰아 쓰는 것이
	///   늘 정답이 되어 매일 들어올 이유가 사라짐. 그래서 날이 바뀌면 <b>상한까지 채우고 끝</b>, 안 쓴 날치는 안 쌓임
	///
	/// ★ 날 경계는 <c>DayResetOffsetSeconds</c> 로 옮긴다. 자정에 끊으면 아직 노는 사람이 하루를
	///   두 번 겪는다. 수집형이 새벽에 끊는 이유 (기본값은 KST 05:00, 판정 대기)
	///
	/// ★ 판정에 실시각을 쓰는 유일한 층. 나머지는 전부 흐른 초로 돈다. 그래서 여기만
	///   <c>nowUnixSeconds</c> 를 받고, 오프라인 정산과 같은 자리에서 부른다 (IdleSession.CatchUp)
	///
	/// ★ 판은 본판 위에 따로 도는 두 번째 전장 (사용자 2026-09-08). 본판은 그대로 다 돌고,
	///   던전은 자기 전장과 자기 체력으로. 세기는 칸의 절대값, 보상은 칸의 고정량
	/// </summary>
	public static partial class IdleDungeons
	{
		/// <summary>던전 수. 화면과 시험이 이 수로 돈다</summary>
		public const int COUNT = 4;

		/// <summary>던전마다 난이도 수와 난이도마다 스테이지 수 (사용자 2026-09-08). 깬 칸 마스크의 비트 자리</summary>
		public const int DIFFICULTY_COUNT = 3;

		public const int STAGE_COUNT = 5;

		/// <summary>판이 끝난 뒤 결과가 보이기까지. 보스가 한 방이라도 쓰러지는 것은 보게 (사용자 2026-09-20)</summary>
		public const double RESULT_DELAY_SECONDS = 1d;

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

		/// <summary>던전 규칙. 값은 SO 가 준 IdleTuning.Dungeons, 없으면 코드 기본값</summary>
		public static IdleDungeonSpec SpecOf(IdleTuning tuning, IdleDungeonKind kind)
		{
			IdleDungeonSpec[] specs = tuning.Dungeons;
			if (specs != null)
			{
				for (int index = 0; index < specs.Length; index++)
				{
					if (specs[index] != null && specs[index].Kind == kind)
					{
						return specs[index];
					}
				}
			}

			IdleDungeonSpec[] defaults = IdleDungeonSpec.Defaults();
			return defaults[(int)kind];
		}

		/// <summary>
		/// 그 던전이 지금 열려 있나. 스킬 던전은 스킬 재료가 아직 없어 닫혀 있음 (economy.md 표 2)
		///
		/// ★ 화면이 이유를 말하려면 여닫힘과 입장권을 따로 물어야 함. 입장권이 0 인 것과
		///   아직 안 만든 것은 사람에게 다른 말
		/// </summary>
		public static bool IsOpen(IdleTuning tuning, IdleDungeonKind kind)
		{
			return SpecOf(tuning, kind).Open;
		}

		/// <summary>그 칸의 규칙. 던전이 닫혔거나 칸이 없으면 null</summary>
		public static IdleDungeonStageSpec CellOf(IdleTuning tuning, IdleDungeonKind kind, int difficulty, int stage)
		{
			IdleDungeonSpec spec = SpecOf(tuning, kind);
			return spec.Open ? spec.CellOf(difficulty, stage) : null;
		}

		/// <summary>칸의 일련 번호. 깬 칸 마스크의 비트 자리이자 사진의 DungeonCells 자리. 던전 4 x 난이도 3 x 스테이지 5 로 60</summary>
		public static int CellIndexOf(IdleDungeonKind kind, int difficulty, int stage)
		{
			return ((int)kind * DIFFICULTY_COUNT + difficulty) * STAGE_COUNT + stage;
		}

		private static int BitOf(IdleDungeonKind kind, int difficulty, int stage)
		{
			return CellIndexOf(kind, difficulty, stage);
		}

		/// <summary>한 번이라도 끝까지 깬 칸인가. 소탕이 열리는 조건 (사용자 2026-09-08)</summary>
		public static bool IsCleared(IdleState state, IdleDungeonKind kind, int difficulty, int stage)
		{
			if (difficulty < 0 || difficulty >= DIFFICULTY_COUNT || stage < 0 || stage >= STAGE_COUNT)
			{
				return false;
			}

			return (state.DungeonCleared & (1L << BitOf(kind, difficulty, stage))) != 0L;
		}

		/// <summary>
		/// 들어갈 수 있는 칸인가. 앞 스테이지를 깨야 다음, 앞 난이도의 마지막을 깨야 다음 난이도 (refs/dungeons.md 2)
		/// </summary>
		public static bool IsUnlocked(IdleState state, IdleTuning tuning, IdleDungeonKind kind, int difficulty, int stage)
		{
			IdleDungeonSpec spec = SpecOf(tuning, kind);
			if (spec.Open == false || difficulty >= DIFFICULTY_COUNT || stage >= STAGE_COUNT || spec.CellOf(difficulty, stage) == null)
			{
				return false;
			}

			if (stage > 0)
			{
				return IsCleared(state, kind, difficulty, stage - 1);
			}

			if (difficulty == 0)
			{
				return true;
			}

			int lastOfPrevious = spec.StagesOf(difficulty - 1) - 1;
			return lastOfPrevious >= 0 && IsCleared(state, kind, difficulty - 1, lastOfPrevious);
		}

		/// <summary>
		/// 한 판 입장 (changes/idle-dungeon-run v2). 입장권 한 장에 <b>판 시작</b>. 보상은 안에서 싸워 얻음
		///
		/// ★ 이미 판이 살아 있으면 안 됨. 던전 안에서 던전을 못 감
		/// ★ 본판 전장은 손대지 않음. 던전은 자기 전장과 자기 체력 (입장 때 성장한 최대치까지 가득) 으로 따로 돔
		/// </summary>
		public static bool TryStart(IdleState state, IdleTuning tuning, IdleDungeonKind kind, int difficulty, int stage)
		{
			if (state.Dungeon.Active || IsUnlocked(state, tuning, kind, difficulty, stage) == false || TrySpend(state, kind) == false)
			{
				return false;
			}

			IdleDungeonStageSpec cell = CellOf(tuning, kind, difficulty, stage);
			IdleDungeonRun run = state.Dungeon;
			run.Clear();
			run.Active = true;
			run.Kind = kind;
			run.Difficulty = difficulty;
			run.Stage = stage;
			run.TimeLimitSeconds = cell.TimeLimitSeconds;
			run.SecondsLeft = cell.TimeLimitSeconds;
			run.Waves = cell.Waves;

			state.EnsureSeatRoom(tuning);
			IdleSquad.HealAll(state, tuning, run.Arena);
			return true;
		}

		/// <summary>지금 판의 칸 규칙. 판이 없으면 null</summary>
		public static IdleDungeonStageSpec CurrentCell(IdleState state, IdleTuning tuning)
		{
			IdleDungeonRun run = state.Dungeon;
			return run.Active ? CellOf(tuning, run.Kind, run.Difficulty, run.Stage) : null;
		}

		/// <summary>던전 전장의 다음 웨이브. 세기는 칸의 본판 구역 환산 (절대값), 보스 던전은 보스 하나</summary>
		public static void WaveOf(IdleState state, IdleTuning tuning, out bool boss, out int count, out int level,
			out double health, out double damagePerSecond)
		{
			IdleDungeonRun run = state.Dungeon;
			IdleDungeonStageSpec cell = CurrentCell(state, tuning);
			boss = run.Kind == IdleDungeonKind.Boss;
			level = cell != null && cell.Level > 0 ? cell.Level : 1;
			int size = cell != null && cell.WaveSize > 0 ? cell.WaveSize : tuning.WaveSize;
			count = boss ? 1 : (size > 0 ? size : 1);
			double healthMultiplier = cell != null && cell.HealthMultiplier > 0d ? cell.HealthMultiplier : 1d;
			double damageMultiplier = cell != null && cell.DamageMultiplier > 0d ? cell.DamageMultiplier : 1d;
			health = IdleModel.TargetHealthAt(level, tuning) * healthMultiplier * (boss ? tuning.BossHealthMultiplier : 1d);
			damagePerSecond = tuning.EnemyDamageByStage.At(level - 1) * damageMultiplier;
		}

		/// <summary>
		/// 시간이 흐름. 시간 제한이 있고 다 됐으면 끝 (재화 던전은 그때가 클리어). 반환은 끝났나 (끝난 판도 true, 전장은 안 돎)
		///
		/// ★ 끝난 뒤에는 결과까지의 초만 센다. 전장은 멈춰 있어야 쓰러진 자리가 보임
		/// </summary>
		public static bool TickRun(IdleState state, IdleTuning tuning, double delta)
		{
			IdleDungeonRun run = state.Dungeon;
			if (run.Active == false)
			{
				return false;
			}

			if (run.Finished)
			{
				run.SecondsSinceFinish += delta;
				return true;
			}

			if (run.TimeLimitSeconds <= 0d)
			{
				return false;
			}

			run.SecondsLeft -= delta;
			if (run.SecondsLeft > 0d)
			{
				return false;
			}

			run.SecondsLeft = 0d;
			EndRun(state, tuning, run.Kind == IdleDungeonKind.Gold);
			return true;
		}

		/// <summary>던전 안 처치 하나. 재화 던전은 처치마다 골드. 보스를 잡으면 조각과 장비를 주고 끝. 반환은 끝났나</summary>
		public static bool OnKill(IdleState state, IdleTuning tuning, bool boss)
		{
			IdleDungeonRun run = state.Dungeon;
			IdleDungeonStageSpec cell = CurrentCell(state, tuning);
			if (run.Active == false || run.Finished || cell == null)
			{
				return false;
			}

			run.Kills += 1L;

			if (run.Kind == IdleDungeonKind.Gold && cell.GoldSecondsPerKill > 0d)
			{
				double gold = IdleModel.IncomePerSecond(state, tuning) * cell.GoldSecondsPerKill;
				state.Resource += gold;
				run.Gold += gold;
			}

			if (run.Kind == IdleDungeonKind.Boss && boss)
			{
				long shards = cell.Shards > 0L ? cell.Shards : 0L;
				run.Shards += shards;
				state.PrestigeShards += shards;
				run.Gear += IdleGear.Stow(state, tuning, GearTierOf(state, tuning, cell), cell.GearCount);
				EndRun(state, tuning, true);
				return true;
			}

			return false;
		}

		/// <summary>던전 안 웨이브 하나를 다 잡음. 장비 던전은 웨이브마다 장비, 다 밀면 끝. 반환은 끝났나</summary>
		public static bool OnWaveCleared(IdleState state, IdleTuning tuning)
		{
			IdleDungeonRun run = state.Dungeon;
			IdleDungeonStageSpec cell = CurrentCell(state, tuning);
			if (run.Active == false || run.Finished || cell == null)
			{
				return false;
			}

			run.WavesCleared += 1;

			if (run.Kind == IdleDungeonKind.Gear)
			{
				run.Gear += IdleGear.Stow(state, tuning, GearTierOf(state, tuning, cell), cell.GearCount);

				if (run.Waves > 0 && run.WavesCleared >= run.Waves)
				{
					EndRun(state, tuning, true);
					return true;
				}
			}

			return false;
		}

		/// <summary>
		/// 사람이 나가기를 누름. 싸우는 중이면 얻은 것 들고 즉시 복귀, 판은 실패 (사용자 2026-09-08, 소탕 안 열림).
		/// 판이 끝나 결과를 보는 중이면 그제야 본판으로 (사용자 2026-09-20: 던전 안에서 결과 보고 클릭해야 나옴)
		/// </summary>
		public static bool Leave(IdleState state, IdleTuning tuning)
		{
			IdleDungeonRun run = state.Dungeon;
			if (run.Active == false)
			{
				return false;
			}

			if (run.Finished == false)
			{
				EndRun(state, tuning, false);
			}

			return Dismiss(state);
		}

		/// <summary>결과를 다 보고 본판으로. 판이 끝나 있을 때만</summary>
		public static bool Dismiss(IdleState state)
		{
			IdleDungeonRun run = state.Dungeon;
			if (run.Active == false || run.Finished == false)
			{
				return false;
			}

			run.Clear();
			return true;
		}

		/// <summary>
		/// 판 끝. 얻은 것은 이미 상태에 들어가 있음. 결과를 남기고 판은 Finished 로 멈춤 (Dismiss 가 지움).
		/// 본판 전장은 그대로 (뒤에서 계속 돌았음). 깼으면 그 칸의 소탕이 열림. 전멸, 나가기, 시간 끝 (재화 제외) 은 그때까지 얻은 것만
		/// </summary>
		public static void EndRun(IdleState state, IdleTuning tuning, bool cleared)
		{
			IdleDungeonRun run = state.Dungeon;
			if (run.Active == false || run.Finished)
			{
				return;
			}

			double spent = run.TimeLimitSeconds > 0d ? run.TimeLimitSeconds - run.SecondsLeft : 0d;
			state.LastDungeonResult = new IdleDungeonResult(run.Kind, run.Difficulty, run.Stage, cleared, spent,
				run.Kills, run.WavesCleared, run.Gold, run.Shards, run.Gear);
			state.DungeonResultSequence += 1L;

			if (cleared && run.Difficulty < DIFFICULTY_COUNT && run.Stage < STAGE_COUNT)
			{
				state.DungeonCleared |= 1L << BitOf(run.Kind, run.Difficulty, run.Stage);
			}

			run.Finished = true;
			run.Cleared = cleared;
			run.SecondsSinceFinish = 0d;
			run.Battle.Foes.Clear();
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

