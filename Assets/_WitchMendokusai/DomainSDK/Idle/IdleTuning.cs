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
        /// <summary>대상 하나의 내구 — 이만큼 깎으면 자원이 나온다.</summary>
        public double TargetDurability { get; set; } = 10d;

        /// <summary>대상 하나를 처치했을 때 들어오는 자원.</summary>
        public double YieldPerTarget { get; set; } = 1d;

        /// <summary>세기 0 레벨의 한 방.</summary>
        public double BasePower { get; set; } = 1d;

        /// <summary>빠르기 0 레벨의 초당 타격 횟수.</summary>
        public double BaseRate { get; set; } = 1d;

        /// <summary>세기 곡선.</summary>
        public IUpgradeCurve PowerCurve { get; set; } = new GeometricUpgradeCurve
        {
            BaseCost = 10d,
            CostRatio = 1.22d,
            BaseValue = 1d,
            ValueRatio = 1.15d,
        };

        /// <summary>빠르기 곡선.</summary>
        public IUpgradeCurve RateCurve { get; set; } = new GeometricUpgradeCurve
        {
            BaseCost = 25d,
            CostRatio = 1.28d,
            BaseValue = 0.5d,
            ValueRatio = 1.12d,
        };

        /// <summary>한 축의 곡선을 고른다.</summary>
        public IUpgradeCurve CurveOf(IdleUpgradeKind kind)
        {
            return kind == IdleUpgradeKind.Power ? PowerCurve : RateCurve;
        }
    }
}
