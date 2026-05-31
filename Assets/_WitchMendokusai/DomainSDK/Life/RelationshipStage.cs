namespace WitchMendokusai.DomainSDK.Life
{
    /// <summary>
    /// TASK-WM-168 INC-3 — 두 캐릭터 사이 관계의 단계 사다리. 순수 enum (DomainSDK).
    ///
    /// 핵심 invariant: Stranger~Housemate(동거 직전까지)는 친밀도로 *자율* 승급(<see cref="RelationshipModel.AddAffinity"/>).
    /// 그 위(Partner/Married = 연애·결혼)는 **4호(플레이어) 개입으로만**(<see cref="RelationshipModel.TryIntervene"/>)
    /// — 자연 발동 X. 욘·링·알리사의 느슨한 마녀-인형 공존 로어 보호 + 4호=관찰자=욘의 의지 대리(MDD).
    /// 자율 상한 = RelationshipParams.AutoCeiling(기본 Housemate).
    /// </summary>
    public enum RelationshipStage
    {
        Stranger = 0,
        Acquaintance = 1,
        Friend = 2,
        BestFriend = 3,
        Housemate = 4,
        Partner = 5,
        Married = 6,
    }
}
