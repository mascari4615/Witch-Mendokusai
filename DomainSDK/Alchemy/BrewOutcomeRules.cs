using System;

namespace WitchMendokusai.DomainSDK.Alchemy
{
    /// <summary>
    /// 제조 채점 규칙(수치 노출 룰 — 후속 SO 가 감싸 디자이너/모딩 표면으로 노출).
    /// SideEffectWeight = 누적 부작용 1 단위가 품질에서 깎는 양.
    /// FineThreshold / MasterworkThreshold = 품질(0~1) → 등급 경계.
    /// Default 수치는 placeholder(디자인 미확정) — 구조만 데이터 주도.
    /// </summary>
    [Serializable]
    public struct BrewOutcomeRules
    {
        public float SideEffectWeight;
        public float FineThreshold;
        public float MasterworkThreshold;

        public static BrewOutcomeRules Default
        {
            get
            {
                return new BrewOutcomeRules
                {
                    SideEffectWeight = 0.05f,
                    FineThreshold = 0.6f,
                    MasterworkThreshold = 0.85f,
                };
            }
        }
    }
}
