using WitchMendokusai.DomainSDK.Upgrade;

namespace WitchMendokusai.DomainSDK.Idle
{
    // IdleTuning.cs 의 Battle 조각. 같은 클래스의 partial. 상태(필드)는 원본 파일을 본다. 코스트, 카드, 자리, 적, 웨이브, 틱, 던전.
    public sealed partial class IdleTuning
    {
        /// <summary>던전 하나에 하루 몇 번 들어가나 (economy.md 4). 수치는 판정 대기</summary>
        public long TicketsPerDay { get; set; } = 3L;

        /// <summary>재화 던전 한 판이 주는 골드 = 지금 초당 수입의 이만큼 초 (수치는 판정 대기)</summary>
        public double DungeonGoldSeconds { get; set; } = 900d;

        /// <summary>보스 던전 한 판이 주는 환생 조각</summary>
        public long DungeonBossShards { get; set; } = 3L;

        /// <summary>보스 던전 한 판이 주는 장비 수 (지금 갈 수 있는 최고 등급)</summary>
        public long DungeonBossGear { get; set; } = 2L;

        /// <summary>장비 던전 한 판이 주는 장비 수</summary>
        public long DungeonGearCount { get; set; } = 5L;

        /// <summary>
        /// 날 경계를 UTC 자정에서 얼마나 미나 (초).
        ///
        /// ★ 기본값 20시간은 KST 05:00. 자정에 끊으면 아직 노는 사람이 하루를 두 번 겪는 꼴
        /// </summary>
        public long DayResetOffsetSeconds { get; set; } = 20L * 3600L;

        /// <summary>코스트 게이지의 상한 (자동전투+카드 개입 계열 = 10칸).</summary>
        public double CostMax { get; set; } = 10d;

        /// <summary>
        /// 초당 차는 코스트.
        ///
        /// ★ 실조사로 고친 값 (2026-08-23, `refs/blue-archive.md`): 자동전투+카드 개입 계열은 <b>1칸 ≈ 2.4초</b>다
        ///   (6인 파티 회복력 합 4,200 ÷ 1칸 10,000). 우리는 0.1(1칸 10초)로 뒀었는데 <b>네 배 느렸다</b> —
        ///   그러면 카드가 「가끔 오는 사건」이 아니라 「하루에 몇 번」이 되어 개입의 리듬이 죽는다.
        /// </summary>
        public double CostPerSecond { get; set; } = 0.4d;

        /// <summary>일제 사격 값 — 코스트.</summary>
        public double VolleyCost { get; set; } = 3d;

        /// <summary>
        /// 일제 사격이 즉시 몰아치는 <b>자동 공격 몇 초치</b>인가.
        ///
        /// ★ 손 때리기(<see cref="TapSecondsOfAttack"/>)와 같은 비율 문법 —
        ///   고정 피해면 초반엔 과하고 후반엔 아무것도 아니게 된다.
        /// </summary>
        public double VolleySecondsOfAttack { get; set; } = 20d;

        /// <summary>긴급 보급 값 — 코스트.</summary>
        public double SupplyCost { get; set; } = 2d;

        /// <summary>긴급 보급이 걸려 있는 시간(초). 겹치지 않고 새로 채운다.</summary>
        public double SupplySeconds { get; set; } = 30d;

        /// <summary>걸려 있는 동안 기지 수입에 곱하는 배수.</summary>
        public double SupplyMultiplier { get; set; } = 3d;

        /// <summary>비밀 감정 값 — 코스트. 자원 대신 코스트로 한 번 굴린다.</summary>
        public double AppraiseCardCost { get; set; } = 5d;

        /// <summary>
        /// 한 자리의 <b>기본</b> 체력 — 장비·환생·영웅 등급이 여기에 곱해진다.
        ///
        /// ★ 적 피해(단계 지수)와 맞물려 「몇 구역까지 버티나」를 정하는 자리다.
        /// </summary>
        /// ★ 초반은 <b>안 죽어야 한다</b> (실측 2026-08-23): 60 으로 뒀더니 1구역에서 50초 만에
        ///   전멸했다 — 사람이 아무것도 배우기 전에 실패부터 만난다. 벽은 <b>깊이</b>가 만들지
        ///   시계가 만들면 안 된다.
        public double SeatBaseHealth { get; set; } = 400d;

        /// <summary>
        /// 단계별 적이 <b>초당</b> 넣는 피해 — 깊이의 지수.
        ///
        /// ★ 체력 곡선(1.55)보다 <b>완만하게</b> 둔다. 같으면 아무리 키워도 같은 구역에서 죽고,
        ///   더 가파르면 성장이 무의미해진다. 1.35 는 보상 배수와 같은 결이라 배우기도 쉽다.
        /// </summary>
        /// ★ 배수는 <b>완만해야 한다</b> (실측 2026-08-23): 1.35(보상 배수와 같은 결)로 뒀더니
        ///   깊이 20 언저리에서 진행이 통째로 멎었다 — 하루를 켜 둬도 첫 천장에 못 닿는다.
        ///   적 피해는 <b>벽을 늦추는</b> 것이지 벽을 만드는 것이 아니다. 벽은 체력 곡선이 만든다.
        public GeometricScale EnemyDamageByStage { get; set; } = new GeometricScale(0.35d, 1.18d);

        /// <summary>쓰러진 자리가 다시 일어나는 데 걸리는 시간(초). 하나라도 서 있어야 돈다.</summary>
        public double ReviveSeconds { get; set; } = 12d;

        /// <summary>
        /// 하나 잡을 때마다 <b>최대 체력의 몇 할</b>을 회복하나.
        ///
        /// ★ 이 값이 <b>벽의 위치</b>를 정한다 — 잘 잡으면 안 죽고, 못 잡으면 죽는다.
        ///   0 으로 두면 시간이 곧 죽음이 되어 「머물러 파밍」이 불가능해진다.
        /// </summary>
        public double HealPerKillShare { get; set; } = 0.08d;

        /// <summary>영웅 등급 한 계단이 체력에 더해 주는 몫 — 뽑기의 값어치가 생존으로도 보이게.</summary>
        public double HeroGradeHealthStep { get; set; } = 0.35d;

        /// <summary>인형 사거리. 축 번호(<see cref="IdleHeroAxis"/>) 순. Damage 근, Speed 중, Base/Drop 원</summary>
        public double[] HeroRangeByAxis { get; set; } = new double[] { 2d, 5d, 8d, 8d };

        /// <summary>인형 걷는 속도 (m/s)</summary>
        public double DollMoveSpeed { get; set; } = 2.5d;

        /// <summary>자리별 줄 (y)</summary>
        public double[] LaneY { get; set; } = new double[] { 0d, -1.2d, 1.2d };

        /// <summary>처음 설 때 자리 사이 x 간격</summary>
        public double SeatBackStep { get; set; } = 0.6d;

        /// <summary>몸이 겹치지 않는 최소 거리</summary>
        public double BodyGap { get; set; } = 0.5d;

        public double FoeMeleeRange { get; set; } = 1.5d;

        public double FoeRangedRange { get; set; } = 6d;

        public double FoeMoveSpeed { get; set; } = 2d;

        public double BossMoveSpeed { get; set; } = 1.5d;

        public double FoeAttackSeconds { get; set; } = 1d;

        public double BossAttackSeconds { get; set; } = 1.5d;

        public double BossHealthMultiplier { get; set; } = 3d;

        /// <summary>한 웨이브 잡몹 수</summary>
        public int WaveSize { get; set; } = 3;

        /// <summary>웨이브가 서는 곳. 부대 맨 앞에서 이만큼 앞</summary>
        public double WaveSpawnDistance { get; set; } = 10d;

        public double WaveGapX { get; set; } = 1.5d;

        public double WaveGapY { get; set; } = 1d;

        /// <summary>원거리 적이 섞이기 시작하는 구역. 그 전은 근접만</summary>
        public int RangedFoeFromStage { get; set; } = 4;

        public double RangedFoeChance { get; set; } = 0.4d;

        public double BattleTickSeconds { get; set; } = 0.1d;

        /// <summary>한 번 부름에 도는 틱 상한. 넘친 시간은 폐기</summary>
        public int BattleTicksPerCall { get; set; } = 600;

        /// <summary>실측 창 (s). 같은 구역에서 이만큼 싸우면 초당 처치를 확정</summary>
        public double MeasureSeconds { get; set; } = 60d;

        /// <summary>오프라인 처치는 실측의 이 몫 (라이브의 50% 계약)</summary>
        public double OfflineKillShare { get; set; } = 0.5d;
    }
}

