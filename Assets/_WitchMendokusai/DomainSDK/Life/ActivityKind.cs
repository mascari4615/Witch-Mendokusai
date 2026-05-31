namespace WitchMendokusai.DomainSDK.Life
{
    /// <summary>
    /// TASK-WM-168 INC-2 — 캐릭터가 자율로 고르는 일상 활동의 종류. 순수 enum (DomainSDK).
    /// 각 활동은 특정 욕구를 채운다(<see cref="ActivitySelector.ActivityForNeed"/>):
    /// Eat→Hunger / Sleep→Energy / Hobby→Mood(마도서 연구·정원 돌보기) / Socialize→Social(서로 어울림).
    /// Idle = 급한 욕구 없을 때 기본(배회·쉼). 관계 도약(고백·연애)은 활동이 아니라 4호 개입(INC-4).
    /// </summary>
    public enum ActivityKind
    {
        Idle = 0,
        Eat = 1,
        Sleep = 2,
        Hobby = 3,
        Socialize = 4,
    }
}
