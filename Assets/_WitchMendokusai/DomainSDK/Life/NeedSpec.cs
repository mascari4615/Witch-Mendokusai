namespace WitchMendokusai.DomainSDK.Life
{
    /// <summary>
    /// 한 욕구의 튜닝값 — 분당 감소 속도 / 문제로 보는 임계 / 충족 상한. 순수 값 타입 (DomainSDK).
    /// 하드코딩 금지(수치노출 룰) — 미래(INC-7) 에 LifeProfileSO 가 ToSpec() 으로 제공.
    /// (패턴: Farming/PlantGrowthParams — readonly struct + 생성자 주입)
    /// </summary>
    public readonly struct NeedSpec
    {
        /// <summary>분당 충족도 감소량. 클수록 빨리 배고파짐/지침.</summary>
        public readonly float DecayPerMinute;

        /// <summary>이 값 미만이면 문제 상태(<see cref="NeedModel.IsInNeed"/>).</summary>
        public readonly float LowThreshold;

        /// <summary>충족도 상한 (채워질 수 있는 최대). 정규화 비교의 분모이기도 함.</summary>
        public readonly float Max;

        public NeedSpec(float decayPerMinute, float lowThreshold, float max)
        {
            DecayPerMinute = decayPerMinute;
            LowThreshold = lowThreshold;
            Max = max;
        }
    }
}
