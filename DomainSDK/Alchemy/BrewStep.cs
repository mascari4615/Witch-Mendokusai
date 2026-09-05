using System;

namespace WitchMendokusai.DomainSDK.Alchemy
{
    /// <summary>
    /// 재료 한 번 투입 = 솥 지도 위 한 번의 이동.
    /// Direction = 재료가 가진 방향(SO 디폴트가 제공할 단위 방향, dual 구조 caller override 여지).
    /// Grind = 갈기 정도 = 이동 거리(플레이어 막자질 입력).
    /// Phase 0 은 Direction 정규화를 caller 책임으로 가정(엔진은 순수 합성만).
    /// </summary>
    [Serializable]
    public struct BrewStep
    {
        public BrewVector Direction;
        public float Grind;
    }
}
