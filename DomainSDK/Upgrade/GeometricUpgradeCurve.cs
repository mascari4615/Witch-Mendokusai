using System;

namespace WitchMendokusai.DomainSDK.Upgrade
{
    /// <summary>
    /// 수식으로 값을 내는 곡선 — 값도 효과도 레벨마다 일정 배수로 곱해진다. 방치형의 표준 형태다.
    ///
    ///   값   = BaseCost  × CostRatio^level
    ///   효과 = BaseValue × (ValueRatio^level − 1) / (ValueRatio − 1)   (등비수열 합)
    ///
    /// ★ 왜 곱셈인가 — 방치형의 재미는 <b>값이 오르는 속도와 힘이 오르는 속도의 경주</b>다.
    ///   CostRatio 가 ValueRatio 보다 커야 후반이 완만해지고(성취감), 작으면 금세 무의미해진다.
    ///   그래서 이 두 수의 비가 사실상 이 장르의 난이도 손잡이다.
    ///
    /// ★ 표를 안 만드니 레벨 수가 작업량이 아니다 — 수천 레벨도 공짜다(<see cref="IUpgradeCurve"/> 주석 참조).
    /// </summary>
    [Serializable]
    public sealed class GeometricUpgradeCurve : IUpgradeCurve
    {
        /// <summary>0→1 값.</summary>
        public double BaseCost { get; set; } = 10d;

        /// <summary>레벨마다 값에 곱해지는 배수.</summary>
        public double CostRatio { get; set; } = 1.15d;

        /// <summary>레벨 하나가 주는 효과의 첫 값.</summary>
        public double BaseValue { get; set; } = 1d;

        /// <summary>레벨마다 효과에 곱해지는 배수. 1 이면 매 레벨 같은 양이 더해진다(선형).</summary>
        public double ValueRatio { get; set; } = 1d;

        /// <summary>상한. 기본은 무한.</summary>
        public int MaxLevel { get; set; } = UpgradeLevel.UNBOUNDED;

        int IUpgradeCurve.MaxLevel => MaxLevel;

        public double CostToRaiseFrom(int level)
        {
            return BaseCost * Math.Pow(CostRatio, level);
        }

        public double TotalValueAt(int level)
        {
            if (level <= 0)
            {
                return 0d;
            }

            // 배수가 1 이면 등비수열 합 공식이 0 나누기가 된다 — 그때는 선형이라 곱셈 하나로 끝난다.
            if (Math.Abs(ValueRatio - 1d) < 1e-12d)
            {
                return BaseValue * level;
            }

            return BaseValue * (Math.Pow(ValueRatio, level) - 1d) / (ValueRatio - 1d);
        }
    }
}
