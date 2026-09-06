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
    public sealed partial class IdleTuning
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
        /// ★ 대열 방치 전투 계열이 「지역 3 × 스테이지 10」인 이유와 같다 — <b>끝이 보이는 토막</b>이 있어야
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
        ///   생산자 클리커 계열은 클릭 한 번에 즉시 하나가 는다. 방치형이라도 <b>보이는 빈도</b>는 그만큼 필요하다.
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
        /// 고를 수 있는 배속 (gap-2026-08-23 P1-6).
        ///
        /// ★ <b>보고 있는 동안만</b>. 폭주와 같은 결이고, 자리를 비운 몫은 실측 초당 값으로
        ///   계산하므로 배속이 오프라인 보상을 안 부풀림
        /// </summary>
        public double[] SpeedSteps { get; set; } = { 1d, 2d, 3d };

        // ── 지나가는 것 (변동성) ────────────────────────────────────────────
        //
        // ★ 조사 1순위 (`refs/cookie-clicker.md`) — 방치형은 기대값이 평탄해서
        //   「지금 이 화면을 볼 이유」가 없다. 봉우리를 만드는 자리다.

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

        public IUpgradeCurve MaxHealthCurve { get; set; } = new GeometricUpgradeCurve
        {
            BaseCost = 20d,
            CostRatio = 1.22d,
            BaseValue = 0.08d,
            ValueRatio = 1.06d,
        };

        public IUpgradeCurve DefenseCurve { get; set; } = new GeometricUpgradeCurve
        {
            BaseCost = 20d,
            CostRatio = 1.24d,
            BaseValue = 0.05d,
            ValueRatio = 1.05d,
        };

        public IUpgradeCurve CriticalChanceCurve { get; set; } = new GeometricUpgradeCurve
        {
            BaseCost = 40d,
            CostRatio = 1.3d,
            BaseValue = 0.01d,
            ValueRatio = 1.03d,
        };

        public IUpgradeCurve CriticalDamageCurve { get; set; } = new GeometricUpgradeCurve
        {
            BaseCost = 50d,
            CostRatio = 1.3d,
            BaseValue = 0.05d,
            ValueRatio = 1.04d,
        };

        public IUpgradeCurve RecoveryCurve { get; set; } = new GeometricUpgradeCurve
        {
            BaseCost = 30d,
            CostRatio = 1.25d,
            BaseValue = 0.02d,
            ValueRatio = 1.04d,
        };

        public double BaseCriticalChance { get; set; } = 0.05d;

        public double BaseCriticalDamage { get; set; } = 1.5d;

        public double MaxCriticalChance { get; set; } = 0.75d;

        // ── 기지 (클리커 층) ───────────────────────────────────────────────

        /// <summary>생산자 종류 수.</summary>
        public int ProducerCount { get; set; } = 8;

        /// <summary>
        /// 같은 생산자를 살수록 값이 이만큼씩 오른다.
        ///
        /// ★ <b>생산자 클리커 계열의 실제 값(1.15)</b>이다. 이 하나가
        ///   「싼 것을 여럿 살까, 비싼 것을 하나 살까」를 매번 묻는다.
        /// </summary>
        public double ProducerCostRatio { get; set; } = 1.15d;

        /// <summary>생산자 종류마다의 첫 값 — 위 번호일수록 비싸다.</summary>
        public GeometricScale ProducerCostByKind { get; set; } = new GeometricScale(15d, 10d);

        /// <summary>생산자 하나가 내는 초당 자원 — 위 번호일수록 많이 낸다.</summary>
        public GeometricScale ProducerOutputByKind { get; set; } = new GeometricScale(0.5d, 8d);

        // ── 장비 (모험이 가져오는 것) ──────────────────────────────────────

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
        /// ★ 왜 <b>단계에 선형</b>인가 — 생산자 클리커 계열은 구운 쿠키의 <b>세제곱근</b>,
        ///   깊이 밀기 계열 2층은 <b>로그</b>로 준다. 둘 다 「지수로 커지는 노력」을
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
        /// ★ 대열 방치 전투 계열이 <b>16시간 → 24시간</b>으로 이 값 자체를 늘려 준다. 그 자리다.
        ///   환생하면 세 가지가 같이 오른다: 공격 배수 · 등급 천장 · <b>자리 비워도 되는 시간</b>.
        ///   셋째가 특히 방치형답다 — 세지는 게 아니라 <b>덜 매여도 되는 것</b>이 보상이다.
        /// </summary>
        public double OfflineSecondsPerAscension { get; set; } = 2d * 3600d;

        /// <summary>
        /// 아무리 늘어도 여기까지(초). 기본 24시간.
        ///
        /// ★ 끝이 있어야 한다 — 무한히 늘면 「하루에 한 번 켠다」가 「한 달에 한 번 켠다」가 되고,
        ///   그 순간 게임이 아니라 알림이 된다. 대열 방치 전투 계열의 확장 상한도 24시간이다.
        /// </summary>
        public double MaxOfflineCapSeconds { get; set; } = 24d * 3600d;

        // ── 카드 · 코스트 (V2, concept-v2 — 자동전투+카드 개입 계열 문법) ──────────────────────
        //
        // ★ 개입의 전부를 이 층으로 모은다. 코스트는 시간이 채우고(방치 정합),
        //   자리를 비우면 가득 찬 채로 맞이한다 — 카드 시전이 곧 복귀 보상.

        // ── 부대 — 맞고 쓰러지고 일어난다 (V2, 사용자 방향 2026-08-23) ────────

        // 사거리 전투 (combat.md). 단위 m, s

        /// <summary>한 축의 곡선을 고른다.</summary>
        public IUpgradeCurve CurveOf(IdleUpgradeKind kind)
        {
            switch (kind)
            {
                case IdleUpgradeKind.Damage: return DamageCurve;
                case IdleUpgradeKind.AttackSpeed: return AttackSpeedCurve;
                case IdleUpgradeKind.MaxHealth: return MaxHealthCurve;
                case IdleUpgradeKind.Defense: return DefenseCurve;
                case IdleUpgradeKind.CriticalChance: return CriticalChanceCurve;
                case IdleUpgradeKind.CriticalDamage: return CriticalDamageCurve;
                default: return RecoveryCurve;
            }
        }
    }
}

