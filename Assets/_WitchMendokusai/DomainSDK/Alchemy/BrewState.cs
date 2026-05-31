using System;

namespace WitchMendokusai.DomainSDK.Alchemy
{
    /// <summary>
    /// 제조 진행 중 솥 지도 위 마커의 누적 상태. 순수 데이터.
    /// Position = 지금까지 투입한 재료 벡터들의 누적 끝점.
    /// StepCount = 투입한 재료 수.
    /// AccruedSideEffect = 경로가 위험지대(HazardZone)를 통과하며 누적한 부작용(기본 0 = 위험지대 미사용 시 무해).
    /// </summary>
    [Serializable]
    public struct BrewState
    {
        public BrewVector Position;
        public int StepCount;
        public float AccruedSideEffect;

        public static BrewState Start
        {
            get { return new BrewState { Position = BrewVector.Zero, StepCount = 0, AccruedSideEffect = 0f }; }
        }
    }
}
