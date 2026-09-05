using System;

namespace WitchMendokusai.DomainSDK.Alchemy
{
    /// <summary>
    /// 마도서 페이지가 요구하는 목표 효과 좌표 + 도달 허용 반경.
    /// 효과 기능 축(회복/보호/저주해소/성장/변이/봉인 등)이 솥 지도 위 좌표점으로 배치된다.
    /// Radius = 목표 도달로 인정하는 허용 오차(난이도 = 반경 축소).
    /// </summary>
    [Serializable]
    public struct EffectTarget
    {
        public BrewVector Position;
        public float Radius;
    }
}
