namespace WitchMendokusai.DomainSDK.Farming
{
    /// <summary>
    /// 이 작물이 <b>무슨 시계로</b> 자라는가 (TASK-WM-410). 순수 enum (DomainSDK).
    ///
    /// ★ 왜 작물이 고르나: 세계에 시계가 둘 있다 — 세계의 하늘(WorldClock)과 바깥 현실.
    ///   시계를 <b>밭</b>이 고르면 같은 밭에 심었다는 이유로 작물의 성질이 바뀐다.
    ///   시계를 <b>코어</b>가 하나로 강제하면 한쪽 감각(자면 자라는 맛 / 꺼 놔도 자라는 맛)이 죽는다.
    ///   그래서 「나는 어느 시계를 탄다」는 작물의 선언이다.
    ///
    /// 코어는 이 선언을 <b>읽기만</b> 한다 — 분을 누가 주느냐만 갈릴 뿐, 자라고 시드는 규칙은 하나다.
    /// </summary>
    public enum PlantClock
    {
        /// <summary>세계의 하늘 — 자면 밤이 지나고 그만큼 자란다(돌봄·시듦이 사는 쪽).</summary>
        World = 0,

        /// <summary>바깥 현실 — 게임을 꺼 둔 동안에도 자란다(방치 수확).</summary>
        Real = 1,
    }
}
