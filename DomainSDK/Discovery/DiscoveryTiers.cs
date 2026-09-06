namespace WitchMendokusai.DomainSDK.Discovery
{
    /// <summary>
    /// 계단 보상. 점수를 문턱으로 나눈 만큼 계단, 계단당 배수
    ///
    /// 매끈하게 오르면 채운 순간이 안 느껴져서 계단으로. 첫 사용처는 방치형 인형 도감 (종류 + 별)
    /// 순수 셈. 점수를 무엇으로 세나는 갈래 몫
    /// </summary>
    public static class DiscoveryTiers
    {
        /// <summary>몇 계단 올랐나. 문턱이 0 이하면 계단 없음</summary>
        public static int StepsOf(int score, int stepScore)
        {
            if (stepScore <= 0 || score <= 0)
            {
                return 0;
            }

            return score / stepScore;
        }

        /// <summary>계단이 주는 배수. 계단 0 이면 1</summary>
        public static double MultiplierOf(int score, int stepScore, double stepBonus)
        {
            return 1d + StepsOf(score, stepScore) * stepBonus;
        }
    }
}
