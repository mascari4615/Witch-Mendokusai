namespace WitchMendokusai.DomainSDK.Idle
{
    /// <summary>
    /// 던전 하나의 규칙 (알파 9번, changes/idle-dungeon-run v2). 값은 SO 가 준다 (사용자 2026-09-08: 하드코딩 금지)
    ///
    /// 종류가 규칙을 정하고 (재화는 시간 안 처치, 보스는 하나 잡기, 장비는 웨이브 밀기),
    /// 난이도 x 스테이지 칸이 세기와 보상을 정함. 칸은 난이도 3 x 스테이지 5 (사용자 2026-09-08)
    /// </summary>
    public sealed class IdleDungeonSpec
    {
        public IdleDungeonKind Kind { get; set; }

        /// <summary>닫힌 던전은 입장도 소탕도 못 함 (스킬 던전은 스킬 재료가 아직 없음)</summary>
        public bool Open { get; set; } = true;

        /// <summary>난이도 순. 난이도마다 스테이지 배열</summary>
        public IdleDungeonTier[] Tiers { get; set; } = new IdleDungeonTier[0];

        /// <summary>그 난이도의 스테이지 수. 없으면 0</summary>
        public int StagesOf(int difficulty)
        {
            if (Tiers == null || difficulty < 0 || difficulty >= Tiers.Length || Tiers[difficulty]?.Stages == null)
            {
                return 0;
            }

            return Tiers[difficulty].Stages.Length;
        }

        /// <summary>그 칸의 규칙. 없으면 null</summary>
        public IdleDungeonStageSpec CellOf(int difficulty, int stage)
        {
            return stage >= 0 && stage < StagesOf(difficulty) ? Tiers[difficulty].Stages[stage] : null;
        }

        /// <summary>코드 기본값. 실제 값은 SO (DungeonCatalogSO). 시험과 SO 가 비었을 때만</summary>
        public static IdleDungeonSpec[] Defaults()
        {
            return new[]
            {
                Build(IdleDungeonKind.Gold, true, 90d, 0, 3, 30d, 900d, 0L, 0L),
                Build(IdleDungeonKind.Boss, true, 120d, 0, 1, 0d, 0d, 3L, 2L),
                Build(IdleDungeonKind.Gear, true, 180d, 5, 3, 0d, 0d, 0L, 1L),
                new IdleDungeonSpec { Kind = IdleDungeonKind.Skill, Open = false },
            };
        }

        /// <summary>난이도 3 x 스테이지 5 를 한 규칙으로 채움. 세기는 본판 구역 환산 (난이도마다 20, 스테이지마다 4)</summary>
        private static IdleDungeonSpec Build(IdleDungeonKind kind, bool open, double timeLimit, int waves, int waveSize,
            double goldSecondsPerKill, double sweepGoldSeconds, long shards, long gearCount)
        {
            IdleDungeonTier[] tiers = new IdleDungeonTier[IdleDungeons.DIFFICULTY_COUNT];
            for (int difficulty = 0; difficulty < tiers.Length; difficulty++)
            {
                IdleDungeonStageSpec[] stages = new IdleDungeonStageSpec[IdleDungeons.STAGE_COUNT];
                for (int stage = 0; stage < stages.Length; stage++)
                {
                    stages[stage] = new IdleDungeonStageSpec
                    {
                        Level = 1 + difficulty * 20 + stage * 4,
                        HealthMultiplier = 1d,
                        DamageMultiplier = 1d,
                        TimeLimitSeconds = timeLimit,
                        Waves = waves,
                        WaveSize = waveSize,
                        GoldSecondsPerKill = goldSecondsPerKill,
                        SweepGoldSeconds = sweepGoldSeconds,
                        Shards = shards > 0L ? shards + difficulty : 0L,
                        GearCount = gearCount,
                        GearTier = 0,
                    };
                }

                tiers[difficulty] = new IdleDungeonTier { Stages = stages };
            }

            return new IdleDungeonSpec { Kind = kind, Open = open, Tiers = tiers };
        }
    }

    /// <summary>난이도 하나. 스테이지 배열</summary>
    public sealed class IdleDungeonTier
    {
        public IdleDungeonStageSpec[] Stages { get; set; } = new IdleDungeonStageSpec[0];
    }

    /// <summary>
    /// 난이도-스테이지 칸 하나의 세기와 보상. 세기는 본판 구역과 무관한 절대값 (사용자 2026-09-08)
    ///
    /// ★ 절대값이라도 곡선은 본판 것을 빌림. <see cref="Level"/> 이 본판 몇 구역짜리 적인가.
    ///   그래야 표를 손으로 채울 수 있고 (구역 번호 하나) 본판 곡선을 고치면 던전도 따라감
    /// </summary>
    public sealed class IdleDungeonStageSpec
    {
        /// <summary>적 세기의 본판 구역 환산. 체력과 초당 피해 곡선을 이 구역 것으로</summary>
        public int Level { get; set; } = 1;

        public double HealthMultiplier { get; set; } = 1d;

        public double DamageMultiplier { get; set; } = 1d;

        /// <summary>시간 제한 (초). 0 이면 없음. 끝나면 판이 끝남 (재화는 그때가 클리어)</summary>
        public double TimeLimitSeconds { get; set; }

        /// <summary>장비 던전의 웨이브 수. 다 밀면 클리어. 0 이면 웨이브 조건 없음</summary>
        public int Waves { get; set; }

        /// <summary>한 웨이브 잡몹 수. 0 이면 본판 WaveSize</summary>
        public int WaveSize { get; set; }

        /// <summary>재화 던전. 처치 하나가 주는 골드를 초당 수입 몇 초치로 셈</summary>
        public double GoldSecondsPerKill { get; set; }

        /// <summary>재화 던전 소탕 한 판이 주는 골드 (초당 수입 몇 초치)</summary>
        public double SweepGoldSeconds { get; set; }

        /// <summary>보스 던전. 보스를 잡으면 환생 조각</summary>
        public long Shards { get; set; }

        /// <summary>보스 던전은 잡았을 때, 장비 던전은 웨이브마다 주는 장비 수</summary>
        public long GearCount { get; set; }

        /// <summary>주는 장비 등급. 0 이면 Level 구역의 최고 등급</summary>
        public int GearTier { get; set; }
    }

    /// <summary>던전 한 판이 살아 있는 동안의 상태. 저장 안 함 (얻는 즉시 상태에 들어가므로 되돌릴 것이 없음)</summary>
    public sealed class IdleDungeonRun
    {
        public bool Active { get; set; }

        /// <summary>
        /// 판이 끝났으나 아직 던전 안. 결과는 던전 안에서 보이고 사람이 나가기를 눌러야 본판 (사용자 2026-09-20)
        ///
        /// ★ Finished 동안 전장과 시계는 멈춤. 던전 배경과 쓰러진 자리는 그대로
        /// </summary>
        public bool Finished { get; set; }

        /// <summary>끝난 판이 클리어였나. Finished 일 때만 뜻이 있음</summary>
        public bool Cleared { get; set; }

        /// <summary>끝난 뒤 흐른 초. 결과는 이만큼 지난 뒤 보임 (보스 한 방이라도 쓰러지는 것을 보게)</summary>
        public double SecondsSinceFinish { get; set; }

        public IdleDungeonKind Kind { get; set; }

        public int Difficulty { get; set; }

        public int Stage { get; set; }

        public double TimeLimitSeconds { get; set; }

        public double SecondsLeft { get; set; }

        public int Waves { get; set; }

        public int WavesCleared { get; set; }

        public long Kills { get; set; }

        public double Gold { get; set; }

        public long Shards { get; set; }

        public int Gear { get; set; }

        /// <summary>던전 전장. 본판 전장과 따로 (사용자 2026-09-08: 본판은 뒤에서 계속)</summary>
        public IdleBattle Battle { get; } = new IdleBattle();

        /// <summary>던전 안 인형 체력. 입장 때 성장한 최대치까지 가득 (사용자 2026-09-20), 본판 체력과 무관</summary>
        public double[] SeatHealth { get; } = new double[IdleSquad.SEAT_COUNT];

        public double[] SeatReviveSeconds { get; } = new double[IdleSquad.SEAT_COUNT];

        public IdleArena Arena => new IdleArena(Battle, SeatHealth, SeatReviveSeconds, true);

        public void Clear()
        {
            Active = false;
            Finished = false;
            Cleared = false;
            SecondsSinceFinish = 0d;
            Difficulty = 0;
            Stage = 0;
            TimeLimitSeconds = 0d;
            SecondsLeft = 0d;
            Waves = 0;
            WavesCleared = 0;
            Kills = 0L;
            Gold = 0d;
            Shards = 0L;
            Gear = 0;
            Battle.Ready = false;
            Battle.Foes.Clear();
            Battle.Hits.Clear();
        }
    }

    /// <summary>끝난 던전 한 판의 결과. 화면이 팝업으로 적는다</summary>
    public readonly struct IdleDungeonResult
    {
        public IdleDungeonResult(IdleDungeonKind kind, int difficulty, int stage, bool cleared, double secondsSpent,
            long kills, int wavesCleared, double gold, long shards, int gear)
        {
            Kind = kind;
            Difficulty = difficulty;
            Stage = stage;
            Cleared = cleared;
            SecondsSpent = secondsSpent;
            Kills = kills;
            WavesCleared = wavesCleared;
            Gold = gold;
            Shards = shards;
            Gear = gear;
        }

        public IdleDungeonKind Kind { get; }

        public int Difficulty { get; }

        public int Stage { get; }

        /// <summary>끝까지 깼나. 재화는 시간을 버팀, 보스는 잡음, 장비는 웨이브 다 밈. 깬 칸만 소탕이 열림</summary>
        public bool Cleared { get; }

        public double SecondsSpent { get; }

        public long Kills { get; }

        public int WavesCleared { get; }

        public double Gold { get; }

        public long Shards { get; }

        /// <summary>가방에 실제로 들어간 장비 수</summary>
        public int Gear { get; }
    }

    /// <summary>사진에 실리는 판 상태</summary>
    public readonly struct IdleDungeonRunView
    {
        public IdleDungeonRunView(bool active, IdleDungeonKind kind, int difficulty, int stage, double secondsLeft,
            double timeLimitSeconds, int wavesCleared, int waves, long kills, double gold, long shards, int gear,
            bool finished = false, bool cleared = false, double secondsSinceFinish = 0d)
        {
            Active = active;
            Finished = finished;
            Cleared = cleared;
            SecondsSinceFinish = secondsSinceFinish;
            Kind = kind;
            Difficulty = difficulty;
            Stage = stage;
            SecondsLeft = secondsLeft;
            TimeLimitSeconds = timeLimitSeconds;
            WavesCleared = wavesCleared;
            Waves = waves;
            Kills = kills;
            Gold = gold;
            Shards = shards;
            Gear = gear;
        }

        public bool Active { get; }

        /// <summary>판은 끝났고 던전 안에서 결과를 기다리는 중</summary>
        public bool Finished { get; }

        public bool Cleared { get; }

        public double SecondsSinceFinish { get; }

        /// <summary>결과를 보일 때. 끝난 뒤 RESULT_DELAY_SECONDS 가 지났나</summary>
        public bool ResultReady => Finished && SecondsSinceFinish >= IdleDungeons.RESULT_DELAY_SECONDS;

        public IdleDungeonKind Kind { get; }

        public int Difficulty { get; }

        public int Stage { get; }

        public double SecondsLeft { get; }

        public double TimeLimitSeconds { get; }

        public int WavesCleared { get; }

        public int Waves { get; }

        public long Kills { get; }

        public double Gold { get; }

        public long Shards { get; }

        public int Gear { get; }
    }

    /// <summary>사진에 실리는 칸 하나 (던전 x 난이도 x 스테이지). 화면 격자가 그대로 적는다</summary>
    public readonly struct IdleDungeonCellView
    {
        public IdleDungeonCellView(IdleDungeonKind kind, int difficulty, int stage, bool open, bool unlocked, bool cleared,
            int level, double timeLimitSeconds, int waves, double sweepGold, long shards, long gearCount, int gearTier)
        {
            Kind = kind;
            Difficulty = difficulty;
            Stage = stage;
            Open = open;
            Unlocked = unlocked;
            Cleared = cleared;
            Level = level;
            TimeLimitSeconds = timeLimitSeconds;
            Waves = waves;
            SweepGold = sweepGold;
            Shards = shards;
            GearCount = gearCount;
            GearTier = gearTier;
        }

        public IdleDungeonKind Kind { get; }

        public int Difficulty { get; }

        public int Stage { get; }

        /// <summary>칸이 있고 던전이 열려 있나 (스킬 던전은 닫힘)</summary>
        public bool Open { get; }

        /// <summary>앞 칸을 깨서 들어갈 수 있나</summary>
        public bool Unlocked { get; }

        /// <summary>한 번이라도 깼나. 소탕이 열리는 조건</summary>
        public bool Cleared { get; }

        public int Level { get; }

        public double TimeLimitSeconds { get; }

        public int Waves { get; }

        /// <summary>재화 던전 소탕 한 판의 골드 (지금 수입 기준)</summary>
        public double SweepGold { get; }

        public long Shards { get; }

        public long GearCount { get; }

        public int GearTier { get; }
    }
}
