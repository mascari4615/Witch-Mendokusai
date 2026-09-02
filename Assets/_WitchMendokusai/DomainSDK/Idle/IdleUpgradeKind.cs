namespace WitchMendokusai.DomainSDK.Idle
{
    /// <summary>
    /// 영웅별로 올리는 전투 수치.
    /// 전투 판정 이름. 표시 이름은 표현 계층 소유.
    /// </summary>
    public enum IdleUpgradeKind
    {
        /// <summary>한 방의 공격력 — 대상을 깎는 양.</summary>
        Damage = 0,

        /// <summary>때리는 공격속도 — 초당 타격 횟수.</summary>
        AttackSpeed = 1,

        /// <summary>맞고 버티는 최대 체력.</summary>
        MaxHealth = 2,

        /// <summary>받는 피해를 줄이는 방어력.</summary>
        Defense = 3,

        /// <summary>치명타가 날 확률.</summary>
        CriticalChance = 4,

        /// <summary>치명타 한 번의 피해 배수.</summary>
        CriticalDamage = 5,

        /// <summary>처치할 때 되찾는 최대 체력의 몫.</summary>
        Recovery = 6,
    }
}
