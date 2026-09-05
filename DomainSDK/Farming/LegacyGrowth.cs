using System;

namespace WitchMendokusai.DomainSDK.Farming
{
    /// <summary>
    /// 옛 밭(실시간 초로 자라던 것)을 지금의 성장 규칙으로 옮기는 다리 (TASK-WM-410). 순수 (DomainSDK).
    ///
    /// ★ 왜 다리가 필요한가: 옛 밭은 「심은 뒤 N초」로만 자랐다(단계도 생기도 없음).
    ///   그걸 지금 규칙으로 적으면 <b>단계 1개 · 절대 안 시듦(생기 소모 0)</b> 짜리 작물이다 —
    ///   규칙을 새로 만드는 게 아니라, 옛 규칙이 지금 규칙의 <b>가장 단순한 경우</b>임을 드러내는 것이다.
    ///
    /// ⚠ 눈에 보이는 차이 하나: 성장 시간이 <b>분 단위로 올림</b>된다(30초짜리는 1분이 된다).
    ///   지금의 성장은 분으로 세기 때문이고, 내림하면 0분 = 심자마자 수확이 되어 더 나쁘다.
    /// </summary>
    public static class LegacyGrowth
    {
        private const int SINGLE_STAGE = 1;
        private const float NEVER_WITHERS = 0f;
        private const float FULL_VITALITY = 100f;
        private const float NO_TEND_NEEDED = 0f;

        /// <summary>
        /// 「심은 뒤 N초면 다 자란다」를 성장 수치로 옮긴다.
        /// <paramref name="realSecondsPerGrowthMinute"/> = 현실 몇 초가 성장 1분인가(세계의 환산율).
        /// </summary>
        public static PlantGrowthParams FromSeconds(float growSeconds, float realSecondsPerGrowthMinute)
        {
            float secondsPerMinute = realSecondsPerGrowthMinute > 0f ? realSecondsPerGrowthMinute : 60f;
            int minutes = (int)Math.Ceiling(growSeconds / secondsPerMinute);

            if (minutes < 1)
            {
                minutes = 1; // 0분이면 심자마자 수확 = 옛 밭보다 더 이상하다.
            }

            return new PlantGrowthParams(minutes, SINGLE_STAGE, FULL_VITALITY, NEVER_WITHERS, NO_TEND_NEEDED);
        }
    }
}
