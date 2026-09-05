namespace WitchMendokusai.DomainSDK.Life
{
    /// <summary>
    /// TASK-WM-168 Tomodachi 자율 삶 레이어 — 캐릭터가 시간에 따라 스스로 채워야 하는 욕구의 종류.
    /// 순수 enum (DomainSDK 격상순서 1단계). 충족도가 시간에 따라 줄고(<see cref="NeedModel.Step"/>),
    /// 임계 아래로 떨어지면 문제 상태 → 캐릭터가 알아서 해소하려 하거나(INC-2) 4호가 개입한다(INC-4).
    ///
    /// 비전 매핑: Hunger=먹기 / Energy=자기 / Mood=취미·즐거움(마도서 연구·정원) / Social=함께 어울림.
    /// "함께 있는 것이 요점"(WM 핵심) = Social 결핍이 캐릭터를 서로에게 끌어당기는 입력.
    /// 미래(INC-7) 데이터 주도 시 NeedSpec 만 SO 로 외부화 — enum 은 안정적 코어로 유지.
    /// </summary>
    public enum NeedKind
    {
        Hunger = 0,
        Energy = 1,
        Mood = 2,
        Social = 3,
    }
}
