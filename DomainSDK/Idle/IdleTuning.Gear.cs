using WitchMendokusai.DomainSDK.Upgrade;

namespace WitchMendokusai.DomainSDK.Idle
{
    // IdleTuning.cs 의 Gear 조각. 같은 클래스의 partial. 상태(필드)는 원본 파일을 본다. 가방, 장비, 합성, 감정, 분해, 단계.
    public sealed partial class IdleTuning
    {
        /// <summary>가방 확장 한 묶음이 늘려 주는 칸 수 (상점, 2026-09-01)</summary>
        public int BagUpgradeStep { get; set; } = 10;

        /// <summary>가방 확장을 몇 묶음까지 사나. 무한이면 합성을 안 하게 된다</summary>
        public int BagUpgradeMost { get; set; } = 8;

        /// <summary>첫 가방 확장에 드는 골드</summary>
        public double BagUpgradeCostBase { get; set; } = 200d;

        /// <summary>살수록 값이 오르는 비율</summary>
        public double BagUpgradeCostRatio { get; set; } = 1.6d;

        /// <summary>
        /// 가방 칸 수.
        ///
        /// ★ 차는 것 자체가 결정이다 — 「무엇을 합치고 무엇을 버릴까」.
        ///   대열 방치 전투 계열에도 「장비 꽉참」 알림이 있다.
        /// </summary>
        public int BagCapacity { get; set; } = 40;

        /// <summary>몇 개를 합쳐야 한 단계 위가 되나.</summary>
        public int MergeCount { get; set; } = 9;

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
        public double MergeCostFactor { get; set; } = 0d;

        /// <summary>분해 한 개가 주는 골드 (등급 1 기준). 등급마다 <see cref="SalvageGoldRatio"/> 배 (사용자 2026-09-05)</summary>
        public double SalvageGoldBase { get; set; } = 20d;

        /// <summary>등급 하나 위마다 분해 골드가 이만큼 곱해진다.</summary>
        public double SalvageGoldRatio { get; set; } = 3d;

        /// <summary>
        /// 아직 한 번도 안 환생했을 때의 등급 상한.
        ///
        /// ★ 대열 방치 전투 계열의 <b>일반 모드 상한이 6등급</b>이다. 그 위(7~8)는 카오스에서만 나온다.
        /// </summary>
        public int BaseMaxTier { get; set; } = 6;

        /// <summary>
        /// 한 번 환생할 때마다 열리는 등급 수.
        ///
        /// ★ 대열 방치 전투 계열이 일반 6 → 카오스 8 로 <b>상한 자체를 연다</b>(+2). 여기가 그 자리다.
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
        /// ★ 근거는 대열 방치 전투 계열의 실제 표다 — 「1지역(10스테이지) 1~2등급 · 2지역 3~4 · 3지역 5~6」.
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
    }
}

