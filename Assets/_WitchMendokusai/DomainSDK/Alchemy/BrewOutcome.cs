using System;

namespace WitchMendokusai.DomainSDK.Alchemy
{
    /// <summary>
    /// 제조 한 판의 채점 결과. BrewEngine.Evaluate 가 마커 상태(BrewState)+목표(EffectTarget)로 산출.
    /// 순수 데이터 — 후속 PotionBrewed 이벤트/인벤토리 보상이 이 값을 읽는다.
    /// Reached = 목표 도달 여부 / Potency = 효과 강도(중심 근접 0~1) / SideEffect = 누적 부작용 /
    /// Quality = 강도 − 부작용 페널티(0~1 clamp) / Grade = 품질 등급.
    /// </summary>
    [Serializable]
    public struct BrewOutcome
    {
        public bool Reached;
        public float Potency;
        public float SideEffect;
        public float Quality;
        public BrewGrade Grade;
    }
}
