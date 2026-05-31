namespace WitchMendokusai.DomainSDK.Life
{
    /// <summary>
    /// TASK-WM-168 INC-4 — 4호(플레이어)가 세계에 손대는 개입의 종류. 순수 enum (DomainSDK).
    ///
    /// 욕구 해소 4종(Feed→Hunger / Rest→Energy / Cheer→Mood / Socialize→Social): 아이템·돌봄으로 채움.
    /// Mediate: 다툰 둘의 친밀도 회복(중재). Bond: 관계 단계 도약(연애·결혼 게이트 통과) — 핵심.
    /// 4호 = 관찰자 인형 = 욘의 의지 대리. 큰 인연(Bond)은 세계가 아니라 4호가 정한다(INC-3 invariant).
    /// </summary>
    public enum InterventionKind
    {
        Feed = 0,
        Rest = 1,
        Cheer = 2,
        Socialize = 3,
        Mediate = 4,
        Bond = 5,
    }
}
