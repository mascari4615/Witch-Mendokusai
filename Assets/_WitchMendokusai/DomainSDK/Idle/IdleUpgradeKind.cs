namespace WitchMendokusai.DomainSDK.Idle
{
    /// <summary>
    /// 업그레이드 축 — MVP-0 은 둘뿐이다(공격력 / 공격속도).
    /// ★ 이름을 일부러 추상으로 둔다: 컨셉(슬라임·사냥꾼·목장)이 바뀌어도 이 축은 그대로 산다.
    ///   구체 이름은 표시 계층(UI 문자열)에서만 붙인다.
    /// </summary>
    public enum IdleUpgradeKind
    {
        /// <summary>한 방의 공격력 — 대상을 깎는 양.</summary>
        Damage = 0,

        /// <summary>때리는 공격속도 — 초당 타격 횟수.</summary>
        AttackSpeed = 1,
    }
}
