namespace WitchMendokusai.DomainSDK.Idle
{
    /// <summary>
    /// 업그레이드 축 — MVP-0 은 둘뿐이다(세기 / 빠르기).
    /// ★ 이름을 일부러 추상으로 둔다: 컨셉(슬라임·사냥꾼·목장)이 바뀌어도 이 축은 그대로 산다.
    ///   구체 이름은 표시 계층(UI 문자열)에서만 붙인다.
    /// </summary>
    public enum IdleUpgradeKind
    {
        /// <summary>한 방의 세기 — 대상을 깎는 양.</summary>
        Power = 0,

        /// <summary>때리는 빠르기 — 초당 타격 횟수.</summary>
        Rate = 1,
    }
}
