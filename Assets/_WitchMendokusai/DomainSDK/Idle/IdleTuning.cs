using WitchMendokusai.DomainSDK.Upgrade;

namespace WitchMendokusai.DomainSDK.Idle
{
    /// <summary>
    /// 방치 판의 손잡이 한 묶음 — 이 게임의 재미는 전부 이 숫자들에 들어 있다.
    /// Unity 의존 0 이라 EditMode 에서 수천 판을 돌려 곡선을 먼저 검증할 수 있다(방치형은 그게 순서다).
    /// 배선 계층(Domain/Idle)의 SO 가 이 값을 채운다 — 수치 하드코딩 금지 룰의 DomainSDK 쪽 형태.
    ///
    /// ★ 값·성장은 여기 없다 — <see cref="IUpgradeCurve"/> 가 정본(SSOT)이다.
    ///   컨셉 이름(슬라임·사냥꾼)도 안 박는다. 「깎아서 얻는다」 구조는 컨셉이 바뀌어도 남는다.
    /// </summary>
    public sealed class IdleTuning
    {
        /// <summary>
        /// 단계마다의 대상 체력 — 이만큼 깎으면 자원이 나온다.
        ///
        /// ★ 배수가 보상 배수보다 <b>커야</b> 벽이 생긴다. 그 벽이 「더 못 내려간다 → 올려야 한다」를
        ///   만드는 유일한 장치다. 두 배수가 같으면 아무 데서나 무한히 내려가고 올릴 이유가 사라진다.
        /// </summary>
        public GeometricScale TargetHealthByStage { get; set; } = new GeometricScale(3d, 1.55d);

        /// <summary>단계마다 대상 하나를 처치했을 때 들어오는 자원.</summary>
        public GeometricScale RewardByStage { get; set; } = new GeometricScale(1d, 1.35d);

        /// <summary>
        /// 한 단계에서 몇을 처치해야 다음으로 내려가나.
        ///
        /// ★ 울티마 스쿼드가 「지역 3 × 스테이지 10」인 이유와 같다 — <b>끝이 보이는 토막</b>이 있어야
        ///   「하나만 더」가 생긴다. 끝없이 이어지는 막대는 아무 데서나 끄게 된다.
        /// </summary>
        public int KillsPerStage { get; set; } = 10;

        /// <summary>공격력 0 레벨의 한 방.</summary>
        public double BaseDamage { get; set; } = 1d;

        /// <summary>
        /// 공격속도 0 레벨의 초당 타격 횟수.
        ///
        /// ★ <b>초반에 매초 뭔가 일어나야 한다</b> (사용자 실측 2026-08-16: 「전혀 클리커 같지 않다」).
        ///   그때 4단계에서 한 마리에 19초였다 — 화면이 멎은 것처럼 보인다.
        ///   쿠키 클리커는 클릭 한 번에 즉시 하나가 는다. 방치형이라도 <b>보이는 빈도</b>는 그만큼 필요하다.
        /// </summary>
        public double BaseAttackSpeed { get; set; } = 3d;

        /// <summary>
        /// 손으로 한 대가 <b>자동 공격 몇 초치</b>인가 (<see cref="IdleModel.Tap"/>).
        ///
        /// ★ 비율로 둔 이유 — 고정값이면 초반엔 과하고 후반엔 무의미해진다.
        ///   비율이면 손은 늘 같은 몫을 한다: 초당 다섯 번 두드리면 대략 <b>공격속도 배</b>가 된다.
        /// ★ 안 두드려도 손해는 없다 — 방치형이라 손은 <b>더 얹는 것</b>이지 <b>내야 하는 것</b>이 아니다.
        /// </summary>
        public double TapSecondsOfAttack { get; set; } = 0.2d;

        // ── 영웅 뽑기 (TASK-WM-406) ─────────────────────────────────────────
        //
        // ★ 사용자가 정한 것은 <b>인심</b>이다 (2026-08-17: 「관대 — 많이 뽑는 맛」).
        //   아래 숫자는 그 결정을 인디 관측 범위 안에서 옮긴 것이다:
        //   최상위 1~2% · 천장 60~80회 (`refs/korean-idle-gacha.md` § 손잡이).

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
        ///   생산자와 같은 꼴(쿠키 클리커 1.15)이라 배우기도 쉽다.
        /// </summary>
        public double PullCostRatio { get; set; } = 1.15d;

        /// <summary>
        /// 한 번 뽑는 데 드는 <b>환생석</b> — 자원과 둘 다 낸다.
        ///
        /// ★ 자원은 「지금 판에서 얼마나 벌었나」, 환생석은 「몇 판을 지나왔나」를 묻는다.
        ///   둘을 같이 걸면 <b>한쪽만 몰아서는 못 뽑는다</b> — 방치도 환생도 건너뛸 수 없다.
        /// </summary>
        public long PullStoneCost { get; set; } = 1L;

        /// <summary>
        /// 한 번에 몰아 사는 최대 개수.
        ///
        /// ★ 상한이 없으면 자원이 아주 많을 때 한 번 누르는 데 몇 초가 걸린다 —
        ///   그건 편해진 게 아니라 <b>멈춘 것</b>으로 느껴진다.
        /// </summary>
        public int BulkBuyMost { get; set; } = 50;

        // ── 지나가는 것 (변동성) ────────────────────────────────────────────
        //
        // ★ 조사 1순위 (`refs/cookie-clicker.md`) — 방치형은 기대값이 평탄해서
        //   「지금 이 화면을 볼 이유」가 없다. 봉우리를 만드는 자리다.

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

        /// <summary>손 때리기가 폭증하는 배수 (쿠키 클리커 777 자리, 우리 규모에 맞춰 낮춘다).</summary>
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

        /// <summary>★ 상한. 여기 닿아도 중복은 조각으로 남는다(꽝이 되면 안 된다).</summary>
        public int MaxStars { get; set; } = 5;

        /// <summary>★ 한 단계에 필요한 중복 수의 기본값 — 위 ★ 일수록 배수로 는다.</summary>
        public int CopiesPerStar { get; set; } = 2;

        /// <summary>★ 한 단계가 더해 주는 몫 (업계 관측 약 10%).</summary>
        public double HeroStarStep { get; set; } = 0.10d;

        /// <summary>
        /// <b>들고만 있어도</b> 붙는 몫 (일반 등급 기준, 등급 무게가 곱해진다).
        ///
        /// ★ 절대 크기는 어떤 상용작도 공개하지 않는다 — 우리 시뮬로 정한다.
        ///   시작값은 「일반 하나 = 3%」. 같은 갈래끼리 더해지므로 열 마리면 +30%.
        /// </summary>
        public double HeroOwnedShareByGrade { get; set; } = 0.03d;

        /// <summary>내보냈을 때 <b>더</b> 붙는 몫 — 보유보다 커야 「내보낸다」가 뜻을 가진다.</summary>
        public double HeroPartyShareByGrade { get; set; } = 0.12d;

        /// <summary>도감이 한 계단 오르는 데 필요한 점수(모은 종류 + 올린 ★).</summary>
        public int CodexStepScore { get; set; } = 5;

        /// <summary>도감 한 계단이 판 전체에 더해 주는 몫.</summary>
        public double CodexStepBonus { get; set; } = 0.15d;

        /// <summary>
        /// 공격력 곡선.
        ///
        /// ★ <b>효과 배수 1.337 은 임의의 값이 아니다</b> — 관계식에서 나온다(실측·유도 2026-08-16).
        ///   자원 R 로 살 수 있는 레벨은 <c>log R / log 비용배수</c> 이므로
        ///   공격력은 <c>R^(ln 효과 / ln 비용)</c> 로 큰다. 자원은 단계마다 보상배수로 크니
        ///   <b>공격력 ∝ 보상^(ln효과/ln비용) 의 단계승</b>이다. 체력은 체력배수의 단계승이다.
        ///
        ///   <c>보상^(ln효과/ln비용)</c> vs <c>체력</c> — 이 하나가 후반을 결정한다.
        ///   지금 값이면 좌변 <b>1.235</b>, 체력 <b>1.55</b> — 업그레이드만으로는 못 따라간다.
        ///
        /// ★ <b>일부러 못 따라가게 둔다.</b> 등식을 맞춰 보니(1.337) 판 하나가 2시간에 312단계까지
        ///   가는 폭주가 났다. 그리고 더 근본적으로 <b>둘을 동시에 가질 수 없다</b>:
        ///   벽은 <c>보상 &lt; 체력</c> 이라야 서고, 진행은 <c>보상^0.7 ≥ 체력</c> 이라야 되는데
        ///   <c>보상^0.7 &lt; 보상</c> 이므로 두 조건이 동시에 참일 수 없다.
        ///   <b>그 간극을 메우는 것이 프레스티지다</b> — 그래서 이 축은 모자라게 두고,
        ///   모자란 만큼을 <see cref="PrestigeMultiplierPerPoint"/> 가 낸다.
        /// </summary>
        public IUpgradeCurve DamageCurve { get; set; } = new GeometricUpgradeCurve
        {
            BaseCost = 10d,
            CostRatio = 1.22d,
            BaseValue = 1d,
            ValueRatio = 1.15d,
        };

        /// <summary>공격속도 곡선.</summary>
        public IUpgradeCurve AttackSpeedCurve { get; set; } = new GeometricUpgradeCurve
        {
            BaseCost = 25d,
            CostRatio = 1.28d,
            BaseValue = 0.5d,
            ValueRatio = 1.12d,
        };

        // ── 기지 (클리커 층) ───────────────────────────────────────────────

        /// <summary>생산자 종류 수.</summary>
        public int ProducerCount { get; set; } = 8;

        /// <summary>
        /// 같은 생산자를 살수록 값이 이만큼씩 오른다.
        ///
        /// ★ <b>쿠키 클리커의 실제 값(1.15)</b>이다. 이 하나가
        ///   「싼 것을 여럿 살까, 비싼 것을 하나 살까」를 매번 묻는다.
        /// </summary>
        public double ProducerCostRatio { get; set; } = 1.15d;

        /// <summary>생산자 종류마다의 첫 값 — 위 번호일수록 비싸다.</summary>
        public GeometricScale ProducerCostByKind { get; set; } = new GeometricScale(15d, 10d);

        /// <summary>생산자 하나가 내는 초당 자원 — 위 번호일수록 많이 낸다.</summary>
        public GeometricScale ProducerOutputByKind { get; set; } = new GeometricScale(0.5d, 8d);

        // ── 장비 (모험이 가져오는 것) ──────────────────────────────────────

        /// <summary>
        /// 가방 칸 수.
        ///
        /// ★ 차는 것 자체가 결정이다 — 「무엇을 합치고 무엇을 버릴까」.
        ///   울티마 스쿼드에도 「장비 꽉참」 알림이 있다.
        /// </summary>
        public int BagCapacity { get; set; } = 40;

        /// <summary>몇 개를 합쳐야 한 단계 위가 되나.</summary>
        public int MergeCount { get; set; } = 3;

        /// <summary>찬 장비의 등급 하나가 주는 배수 — 잠재가 없어도 차는 뜻이 있게.</summary>
        public double GearTierBonus { get; set; } = 0.15d;

        /// <summary>
        /// 감정 한 번에 드는 자원 (등급 1 기준). 등급마다 <see cref="AppraiseCostRatio"/> 배.
        ///
        /// ★ 공짜면 「올릴까 감정할까」가 결정이 아니다 — 두 축이 <b>같은 저울</b>에 올라가야
        ///   기지와 모험이 서로 물린다. 사용자 지적(「안 녹아든다」)의 핵심이 이것이었다.
        /// </summary>
        public double AppraiseBaseCost { get; set; } = 50d;

        /// <summary>등급 하나 위마다 감정 값이 이만큼 곱해진다.</summary>
        public double AppraiseCostRatio { get; set; } = 4d;

        /// <summary>합치기 한 번에 드는 자원 — 감정의 절반으로 둔다(합치기가 더 흔한 행동이라).</summary>
        public double MergeCostFactor { get; set; } = 0.5d;

        /// <summary>
        /// 아직 한 번도 안 환생했을 때의 등급 상한.
        ///
        /// ★ 울티마 스쿼드의 <b>일반 모드 상한이 6등급</b>이다. 그 위(7~8)는 카오스에서만 나온다.
        /// </summary>
        public int BaseMaxTier { get; set; } = 6;

        /// <summary>
        /// 한 번 환생할 때마다 열리는 등급 수.
        ///
        /// ★ 울티마 스쿼드가 일반 6 → 카오스 8 로 <b>상한 자체를 연다</b>(+2). 여기가 그 자리다.
        ///
        /// ★ 왜 이게 있어야 하나 — 실측(2026-08-16)에서 드러난 구멍이다.
        ///   등급이 5단계마다 하나씩 열리니 상한 8 은 36단계면 다 열리는데, 2시간이면 40단계다.
        ///   그 뒤로는 아무리 내려가도 등급이 안 열려 <b>「깊이가 관문」이 후반에 그냥 꺼졌다.</b>
        ///   환생할 때마다 천장을 올리면 「내려간다 → 천장에 닿는다 → 환생한다 → 천장이 오른다」가 돈다.
        ///
        /// ★ 절대 상한을 안 둔다. 대신 <b>매 판마다 천장이 보인다</b> —
        ///   「끝이 보이는 토막」을 한 층 위에 다시 만든 것이다(단계 10개가 한 토막인 것과 같은 이치).
        /// </summary>
        public int TiersPerAscension { get; set; } = 2;

        /// <summary>
        /// 몇 단계를 내려가야 등급 상한이 하나 열리나.
        ///
        /// ★ 근거는 울티마 스쿼드의 실제 표다 — 「1지역(10스테이지) 1~2등급 · 2지역 3~4 · 3지역 5~6」.
        ///   10스테이지에 2등급이니 <b>5스테이지에 1등급</b>이다. 이 비율이 「깊이가 곧 관문」의 몸통이다.
        /// </summary>
        public int StagesPerTier { get; set; } = 5;

        /// <summary>처치 하나가 떨구는 기대 개수.</summary>
        public double DropsPerKill { get; set; } = 0.25d;

        /// <summary>
        /// 한 등급 위로 갈 때 곱해지는 흔함 — 작을수록 높은 등급이 귀하다.
        ///
        /// ★ 이 값이 <b>상한의 값어치</b>를 정한다. 1 에 가까우면 등급이 다 흔해서
        ///   상한이 열려도 감흥이 없고, 너무 작으면 열린 상한이 장식이 된다.
        /// </summary>
        public double TierRarity { get; set; } = 0.4d;

        /// <summary>
        /// 잠재 등급마다의 <b>가장 낮은 값</b>. 레어 = 2%, 한 등급 위마다 2.2배.
        ///
        /// ★ 등급 사이가 겹치지 않아야 <b>등급 자체가 뜻을 갖는다</b>.
        ///   퍼짐이 2 인데 등급 간격이 2.2 라 「레어 최고값 &lt; 에픽 최저값」이 항상 성립한다 —
        ///   즉 아무리 운이 좋아도 <b>아래 등급이 위 등급을 못 이긴다.</b>
        ///   이게 「좋은 잠재를 원하면 내려가는 수밖에 없다」의 실제 근거다.
        /// </summary>
        public GeometricScale PotentialByGrade { get; set; } = new GeometricScale(0.02d, 2.2d);

        /// <summary>한 등급 안에서 가장 높은 값 ÷ 가장 낮은 값. 이만큼이 <b>운</b>의 몫이다.</summary>
        public double PotentialSpread { get; set; } = 2d;

        /// <summary>
        /// 이 단계까지는 내려가 봐야 리셋할 수 있다.
        ///
        /// ★ <b>벽을 느껴 본 뒤에만</b> 리셋이 보상이 된다. 벽에 닿기 전에 리셋을 열어 주면
        ///   그건 그냥 「처음부터 다시」라는 벌이다. 리셋의 값어치는 벽이 만든다.
        /// </summary>
        public int PrestigeMinStage { get; set; } = 10;

        /// <summary>
        /// 단계 하나당 받는 리셋 점수.
        ///
        /// ★ 왜 <b>단계에 선형</b>인가 — 쿠키 클리커는 구운 쿠키의 <b>세제곱근</b>,
        ///   클리커 히어로즈 2층은 <b>로그</b>로 준다. 둘 다 「지수로 커지는 노력」을
        ///   「선형으로 커지는 보상」으로 바꾸는 꼴이다. 우리는 단계 난이도 자체가 지수라
        ///   <b>단계 번호가 이미 노력의 로그</b>다 — 그래서 여기서는 단계에 선형이 그 자리다.
        ///   여기에 또 제곱근을 씌우면 두 번 눌러 리셋이 영영 시시해진다.
        /// </summary>
        public double PrestigePointsPerStage { get; set; } = 1d;

        /// <summary>
        /// 점수 하나가 <b>곱하는</b> 배수. 더하지 않고 곱한다.
        ///
        /// ★ 이 손잡이 하나로 게임이 <b>정체</b>도 되고 <b>인플레</b>도 된다. 셋 다 실측으로 걸렀다
        ///   (2026-08-16, 이레~보름짜리 시뮬레이션):
        ///
        ///   - <b>더하기(점수당 +10%)</b> → 정체. 판 소요가 매 판 1.8배씩 늘어 11판째 42시간.
        ///     요구는 지수인데 보상이 선형이라 못 따라간다.
        ///   - <b>곱하기 1.10</b> → 판마다 +64 → +68 → +68 → <b>+28</b> 로 감속하다 멎는다.
        ///   - <b>곱하기 1.55</b> → 첫 판 뒤 판마다 <b>+21 로 고르게</b> 나아간다.
        ///
        /// ★ <b>기본값이 체력 배수와 같다(1.55)</b> — 우연이 아니다.
        ///   점수는 대략 단계 수만큼 쌓이므로, 점수 하나가 단계 하나만큼의 어려움을 갚으면
        ///   <b>환생할 때마다 그 판이 늘린 어려움을 정확히 상쇄</b>한다.
        ///
        /// ⚠ 한때 이 값을 1.10 으로 내렸었다. 1.55 에서 폭주가 났기 때문인데,
        ///   진짜 원인은 이 손잡이가 아니라 <b>모델의 고장</b>이었다 —
        ///   그때는 한 번 때려 여러 마리가 죽어서 공격력이 곧 처치 속도였다(3a0e0885 에서 고침).
        ///   넘치는 피해를 버리게 하자 유도했던 값이 실제로 맞았다.
        ///   <b>손잡이를 의심하기 전에 모델을 의심할 것.</b>
        /// </summary>
        public double PrestigeMultiplierPerPoint { get; set; } = 1.55d;

        /// <summary>
        /// 아직 한 번도 안 환생했을 때, 자리를 비운 동안 쳐주는 시간의 상한(초). 기본 8시간.
        ///
        /// ★ 왜 상한이 있나 — 없으면 한 달 만에 돌아온 사람이 한 번에 다 받고 게임이 끝난다.
        ///   방치형에서 상한은 벌이 아니라 <b>돌아올 이유</b>다.
        /// </summary>
        public double BaseMaxOfflineSeconds { get; set; } = 8d * 3600d;

        /// <summary>
        /// 한 번 환생할 때마다 늘어나는 상한(초). 기본 2시간.
        ///
        /// ★ 울티마 스쿼드가 <b>16시간 → 24시간</b>으로 이 값 자체를 늘려 준다. 그 자리다.
        ///   환생하면 세 가지가 같이 오른다: 공격 배수 · 등급 천장 · <b>자리 비워도 되는 시간</b>.
        ///   셋째가 특히 방치형답다 — 세지는 게 아니라 <b>덜 매여도 되는 것</b>이 보상이다.
        /// </summary>
        public double OfflineSecondsPerAscension { get; set; } = 2d * 3600d;

        /// <summary>
        /// 아무리 늘어도 여기까지(초). 기본 24시간.
        ///
        /// ★ 끝이 있어야 한다 — 무한히 늘면 「하루에 한 번 켠다」가 「한 달에 한 번 켠다」가 되고,
        ///   그 순간 게임이 아니라 알림이 된다. 울티마 스쿼드의 확장 상한도 24시간이다.
        /// </summary>
        public double MaxOfflineCapSeconds { get; set; } = 24d * 3600d;

        // ── 카드 · 코스트 (V2, concept-v2 — 블아 문법) ──────────────────────
        //
        // ★ 개입의 전부를 이 층으로 모은다. 코스트는 시간이 채우고(방치 정합),
        //   자리를 비우면 가득 찬 채로 맞이한다 — 카드 시전이 곧 복귀 보상.

        /// <summary>코스트 게이지의 상한 (블아 = 10칸).</summary>
        public double CostMax { get; set; } = 10d;

        /// <summary>초당 차는 코스트 — 0.1 이면 빈 게이지가 100초에 가득.</summary>
        public double CostPerSecond { get; set; } = 0.1d;

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

        // ── 부대 — 맞고 쓰러지고 일어난다 (V2, 사용자 방향 2026-08-23) ────────

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

        /// <summary>한 축의 곡선을 고른다.</summary>
        public IUpgradeCurve CurveOf(IdleUpgradeKind kind)
        {
            return kind == IdleUpgradeKind.Damage ? DamageCurve : AttackSpeedCurve;
        }
    }
}
