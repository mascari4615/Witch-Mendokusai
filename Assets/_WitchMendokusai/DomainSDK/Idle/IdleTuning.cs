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
        public GeometricScale TargetHealthByStage { get; set; } = new GeometricScale(10d, 1.55d);

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

        /// <summary>공격속도 0 레벨의 초당 타격 횟수.</summary>
        public double BaseAttackSpeed { get; set; } = 1d;

        /// <summary>공격력 곡선.</summary>
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

        /// <summary>
        /// 아직 한 번도 안 접었을 때의 등급 상한.
        ///
        /// ★ 울티마 스쿼드의 <b>일반 모드 상한이 6등급</b>이다. 그 위(7~8)는 카오스에서만 나온다.
        /// </summary>
        public int BaseMaxTier { get; set; } = 6;

        /// <summary>
        /// 한 번 접을 때마다 열리는 등급 수.
        ///
        /// ★ 울티마 스쿼드가 일반 6 → 카오스 8 로 <b>상한 자체를 연다</b>(+2). 여기가 그 자리다.
        ///
        /// ★ 왜 이게 있어야 하나 — 실측(2026-08-16)에서 드러난 구멍이다.
        ///   등급이 5단계마다 하나씩 열리니 상한 8 은 36단계면 다 열리는데, 2시간이면 40단계다.
        ///   그 뒤로는 아무리 내려가도 등급이 안 열려 <b>「깊이가 관문」이 후반에 그냥 꺼졌다.</b>
        ///   접을 때마다 천장을 올리면 「내려간다 → 천장에 닿는다 → 접는다 → 천장이 오른다」가 돈다.
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
        ///   - <b>곱하기 1.55</b>(단계 난이도와 같은 값) → 인플레. 판마다 깊이가 5배씩 뛴다
        ///     (69 → 363 → …). 지나온 길을 공짜로 되찾고 그 위에 또 쌓이기 때문이다.
        ///   - <b>곱하기 1.10</b> → 판마다 <b>약 70단계씩 일정하게</b> 깊어진다:
        ///     69 → 133 → 201 → 273 → 350. 이게 「매번 조금 더 멀리」의 모양이다.
        ///
        /// ★ 그래서 규칙은 「난이도와 같게」가 아니라 <b>「난이도보다 한참 작게」</b>다 —
        ///   점수는 <b>되돌아가는 삯</b>이지 <b>앞으로 미는 힘</b>이 아니다. 미는 힘은 올리기가 낸다.
        /// </summary>
        public double PrestigeMultiplierPerPoint { get; set; } = 1.10d;

        /// <summary>
        /// 아직 한 번도 안 접었을 때, 자리를 비운 동안 쳐주는 시간의 상한(초). 기본 8시간.
        ///
        /// ★ 왜 상한이 있나 — 없으면 한 달 만에 돌아온 사람이 한 번에 다 받고 게임이 끝난다.
        ///   방치형에서 상한은 벌이 아니라 <b>돌아올 이유</b>다.
        /// </summary>
        public double BaseMaxOfflineSeconds { get; set; } = 8d * 3600d;

        /// <summary>
        /// 한 번 접을 때마다 늘어나는 상한(초). 기본 2시간.
        ///
        /// ★ 울티마 스쿼드가 <b>16시간 → 24시간</b>으로 이 값 자체를 늘려 준다. 그 자리다.
        ///   접으면 세 가지가 같이 오른다: 공격 배수 · 등급 천장 · <b>자리 비워도 되는 시간</b>.
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

        /// <summary>한 축의 곡선을 고른다.</summary>
        public IUpgradeCurve CurveOf(IdleUpgradeKind kind)
        {
            return kind == IdleUpgradeKind.Damage ? DamageCurve : AttackSpeedCurve;
        }
    }
}
