using WitchMendokusai.DomainSDK.Upgrade;

namespace WitchMendokusai.DomainSDK.Idle
{
    // IdleTuning.cs 의 Gacha 조각. 같은 클래스의 partial. 상태(필드)는 원본 파일을 본다. 뽑기, 재화, 별똥, 영웅 성장.
    public sealed partial class IdleTuning
    {
        /// <summary>
        /// 첫 뽑기의 값 (<b>자원</b>).
        ///
        /// ★ 사용자 결정 2026-08-17 — 뽑기 값은 <b>환생석이 아니라 자원</b>이다.
        ///   환생석은 배수의 재료(지수)라, 뽑기에 쓰면 뽑을수록 <b>손해</b>였다
        ///   (실측 이레: 안 뽑음 1619단계 vs 다 뽑음 104단계).
        ///   한 재화가 지수 성장과 일회성 소비를 겸하면 소비 쪽은 늘 진다.
        /// </summary>
        public double PullCostBase { get; set; } = 250d;

        /// <summary>
        /// 뽑을 때마다 값이 이만큼 오른다.
        ///
        /// ★ 사용자가 짚은 단점을 막는 자리다 — 「후반엔 자원이 남아돌아 뽑기가 무한이 된다」.
        ///   값이 <b>뽑은 횟수</b>를 따라 오르면, 자원이 아무리 많아도 뽑기 수는 로그로 눌린다.
        ///   생산자와 같은 꼴(생산자 클리커 계열 1.15)이라 배우기도 쉽다.
        /// </summary>
        public double PullCostRatio { get; set; } = 1.15d;

        /// <summary>
        /// 한 번 뽑는 데 드는 <b>환생석</b> — 자원과 둘 다 낸다.
        ///
        /// ★ 자원은 「지금 판에서 얼마나 벌었나」, 환생석은 「몇 판을 지나왔나」를 묻는다.
        ///   둘을 같이 걸면 <b>한쪽만 몰아서는 못 뽑는다</b> — 방치도 환생도 건너뛸 수 없다.
        /// </summary>
        /// <summary>
        /// 구역을 <b>처음</b> 깰 때 주는 뽑기 재화 (economy.md 표 2).
        ///
        /// ★ 없으면 뽑기를 얻는 길이 환생뿐. 첫 환생 전까지 상점이 잠김.
        ///   수집형에서 초반 몇 시간 동안 아무도 못 뽑는 것은 그 자체로 이탈 지점
        /// </summary>
        public long StonesPerFirstClear { get; set; } = 1L;

        /// <summary>
        /// 보스 하나가 떨구는 환생 조각 (economy.md 표 2). 0 이면 드롭 없음
        ///
        /// ★ 기본 0 (2026-09-01). 1 로 두고 재 보니 성장이 과하게 밀려 두 시간 만에 1619 상한에 닿고
        ///   뽑는 판과 안 뽑는 판이 같아졌다 (PullingGetsYouDeeper 실패). 구조만 두고 수치는 사용자 판정
        /// </summary>
        public long ShardsPerBoss { get; set; }

        /// <summary>처치 하나가 뽑기 재화를 떨굴 확률 (economy.md 표 2, 낮은 확률)</summary>
        public double StoneDropChance { get; set; } = 0.002d;

        public long PullStoneCost { get; set; } = 1L;

        /// <summary>이 시간 전에는 절대 안 뜬다(초).</summary>
        public double VisitorEarliestSeconds { get; set; } = 90d;

        /// <summary>이 시간이 지나면 반드시 뜬다(초).</summary>
        public double VisitorLatestSeconds { get; set; } = 300d;

        /// <summary>떠 있는 동안(초) — 기다려 주지 않아야 누르는 것이 사건이 된다.</summary>
        public double VisitorStaySeconds { get; set; } = 13d;

        /// <summary>잡으면 폭주가 이만큼 간다(초).</summary>
        public double SurgeSeconds { get; set; } = 30d;

        /// <summary>판 전체가 빨라지는 배수.</summary>
        public double FrenzyMultiplier { get; set; } = 7d;

        /// <summary>손 폭주가 걸릴 확률 — 드물어야 「대박」이 된다.</summary>
        public double HandFrenzyChance { get; set; } = 0.2d;

        /// <summary>손 때리기가 폭증하는 배수 (생산자 클리커 계열 777 자리, 우리 규모에 맞춰 낮춘다).</summary>
        public double HandFrenzyMultiplier { get; set; } = 50d;

        /// <summary>최고 등급이 나올 확률. 관대한 판이라 위쪽(2%)을 쓴다.</summary>
        public double LegendChance { get; set; } = 0.02d;

        public double EpicChance { get; set; } = 0.10d;

        public double RareChance { get; set; } = 0.28d;

        /// <summary>
        /// 이만큼 뽑는 동안 최고 등급이 없으면 <b>다음 판에 준다</b> (천장).
        ///
        /// ★ 없으면 불운 한 번이 곧 이탈이다. 확률이 옳아도 사람은 자기 표본만 본다.
        /// </summary>
        public int PityPulls { get; set; } = 60;

        /// <summary>묶음 뽑기 수 (사용자 2026-09-05: 10회). 값은 1회의 이만큼 배, 할인 없음</summary>
        public int PullBatchCount { get; set; } = 10;

        /// <summary>묶음 뽑기가 보장하는 최저 등급 (<see cref="IdleHeroGrade"/> 값. 1 이 레어). 묶음 안에 이 등급 이상이 없으면 마지막 하나를 여기로</summary>
        public int PullBatchFloorGrade { get; set; } = 1;

        /// <summary>픽업 인형이 같은 등급 안에서 뽑히는 무게. 다른 인형은 1 (사용자 2026-09-05: 2배)</summary>
        public double PickupWeight { get; set; } = 2d;

        /// <summary>픽업 인형이 바뀌는 주기 (날). 7 이면 주마다</summary>
        public long PickupDays { get; set; } = 7L;

        /// <summary>무료 상자가 주는 뽑기 재화. 하루 1회 (economy.md 표 2 무료 상자 줄)</summary>
        public long FreeBoxStones { get; set; } = 1L;

        /// <summary>★ 상한. 여기 닿아도 중복은 조각으로 남는다(꽝이 되면 안 된다).</summary>
        public int MaxStars { get; set; } = 5;

        /// <summary>★ 한 단계에 필요한 중복 수의 기본값 — 위 ★ 일수록 배수로 는다.</summary>
        public int CopiesPerStar { get; set; } = 2;

        /// <summary>★ 한 단계가 더해 주는 몫 (업계 관측 약 10%).</summary>
        public double HeroStarStep { get; set; } = 0.10d;

        /// <summary>
        /// 인형 레벨 한 칸이 더해 주는 몫 (economy.md 표 3).
        ///
        /// ★ ★ 의 1/10. 레벨은 수백까지 올리는 것이고 ★ 은 다섯이 끝이라, 레벨 열 칸이
        ///   ★ 하나와 맞먹게. 그래야 둘 다 올릴 이유가 남음
        /// </summary>
        public double HeroLevelStep { get; set; } = 0.01d;

        /// <summary>인형 레벨 0 에서 1 로 올리는 골드</summary>
        public double HeroLevelCostBase { get; set; } = 20d;

        /// <summary>
        /// 레벨마다 값이 오르는 비율.
        ///
        /// ★ 뽑기(1.15)보다 완만하게. 레벨은 자주 누르는 것이라 같은 비율이면 금세 벽
        /// </summary>
        public double HeroLevelCostRatio { get; set; } = 1.09d;

        /// <summary>
        /// <b>들고만 있어도</b> 붙는 몫 (일반 등급 기준, 등급 무게가 곱해진다).
        ///
        /// ★ 절대 크기는 어떤 상용작도 공개하지 않는다 — 우리 시뮬로 정한다.
        ///   시작값은 「일반 하나 = 3%」. 같은 갈래끼리 더해지므로 열 마리면 +30%.
        /// </summary>
        public double HeroOwnedShareByGrade { get; set; } = 0.03d;

        /// <summary>메인 칸에 내보냈을 때 <b>더</b> 붙는 몫. 보유보다 커야 내보낸다가 뜻을 가진다.</summary>
        public double HeroPartyShareByGrade { get; set; } = 0.12d;

        /// <summary>
        /// 보조 칸에 넣었을 때 붙는 몫. 시작값은 메인의 절반.
        ///
        /// ★ 메인보다 작게: 보조는 전장에 안 서서 안 맞으므로, 몫이 같으면 늘 보조가 정답.
        ///   보유(<see cref="HeroOwnedShareByGrade"/>)보다는 크게: 그래야 보조 칸에 넣는다가 결정.
        /// </summary>
        public double HeroSupportShareByGrade { get; set; } = 0.06d;

        /// <summary>도감이 한 계단 오르는 데 필요한 점수(모은 종류 + 올린 ★).</summary>
        public int DiscoveryStepScore { get; set; } = 5;

        /// <summary>도감 한 계단이 판 전체에 더해 주는 몫.</summary>
        public double DiscoveryStepBonus { get; set; } = 0.15d;
    }
}

