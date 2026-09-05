namespace WitchMendokusai.DomainSDK.Life
{
    /// <summary>
    /// 마을 노동이 생산하는 자원의 well-known <see cref="ResourceId"/> 상수.
    /// ResourceId 는 데이터주도(enum 아님) — city 시뮬(CityPaintManager)이 0=원자재·1=재화를 쓰므로,
    /// 노동 자원은 100 대역으로 격리해 같은 <see cref="CityEconomy"/> 원장에서 키 충돌을 피한다.
    /// 표시명·스프라이트는 추후 Domain ResourceSO 카탈로그가 부여(스킨 deferred).
    /// </summary>
    public static class KnownResources
    {
        // city 시뮬 저대역(0~)과 격리 — 노동/삶 자원은 100 대역.
        private const int LIFE_BASE = 100;

        public static readonly ResourceId Acorn = new ResourceId(LIFE_BASE + 0);         // 도토리
        public static readonly ResourceId Herb = new ResourceId(LIFE_BASE + 1);          // 약초·식물 재료
        public static readonly ResourceId Mineral = new ResourceId(LIFE_BASE + 2);       // 광물·마나 결정
        public static readonly ResourceId Food = new ResourceId(LIFE_BASE + 3);          // 식량
        public static readonly ResourceId CraftMaterial = new ResourceId(LIFE_BASE + 4); // 제조 재료
    }
}
