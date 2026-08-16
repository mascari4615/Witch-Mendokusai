namespace WitchMendokusai.DomainSDK.Act
{
    /// <summary>
    /// 행동이 자원을 꺼내 쓰고 담는 자리 (TASK-WM-408) — 소지품이든 창고든 마을 금고든.
    ///
    /// ★ 왜 인터페이스인가: 원장은 「무엇이 얼마나 있나」만 물으면 된다. 그것이 배낭인지
    ///   상자인지 도시 경제 원장(CityEconomy)인지는 원장의 관심이 아니다 —
    ///   여기서 구체 저장소를 알기 시작하면 코어에 게임 종류가 스며든다.
    /// </summary>
    public interface IActResourcePool
    {
        /// <summary>지금 가진 수량 — 모르는 자원은 0.</summary>
        int AmountOf(ResourceId resource);

        /// <summary>수량을 더한다(음수면 뺀다). 지불 가능 판정은 원장이 미리 끝낸 뒤에만 부른다.</summary>
        void Add(ResourceId resource, int amount);
    }
}
