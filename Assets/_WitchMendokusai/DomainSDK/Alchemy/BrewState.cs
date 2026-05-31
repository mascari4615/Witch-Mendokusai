using System;

namespace WitchMendokusai.DomainSDK.Alchemy
{
    /// <summary>
    /// 제조 진행 중 솥 지도 위 마커의 누적 상태. 순수 데이터.
    /// Position = 지금까지 투입한 재료 벡터들의 누적 끝점.
    /// StepCount = 투입한 재료 수(부작용/난이도 산정용 후속 훅).
    /// </summary>
    [Serializable]
    public struct BrewState
    {
        public BrewVector Position;
        public int StepCount;

        public static BrewState Start
        {
            get { return new BrewState { Position = BrewVector.Zero, StepCount = 0 }; }
        }
    }
}
