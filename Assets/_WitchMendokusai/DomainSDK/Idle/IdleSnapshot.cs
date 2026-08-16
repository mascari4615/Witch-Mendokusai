using WitchMendokusai.DomainSDK.Contracts;

namespace WitchMendokusai.DomainSDK.Idle
{
    /// <summary>
    /// 한 업그레이드 축이 화면에 보이는 모습 — 레벨, 지금 효과, 다음 비용, 지금 살 수 있나.
    /// </summary>
    public readonly struct IdleUpgradeView
    {
        public IdleUpgradeKind Kind { get; }
        public int Level { get; }

        /// <summary>이 축이 지금 주고 있는 값(공격력이면 한 방, 공격속도면 초당 횟수).</summary>
        public double CurrentValue { get; }

        /// <summary>다음 레벨 비용. <see cref="IsMaxed"/> 면 뜻이 없다.</summary>
        public double NextCost { get; }

        /// <summary>더 못 올린다.</summary>
        public bool IsMaxed { get; }

        /// <summary>지금 가진 자원으로 살 수 있다.</summary>
        public bool CanAfford { get; }

        public IdleUpgradeView(IdleUpgradeKind kind, int level, double currentValue, double nextCost, bool isMaxed, bool canAfford)
        {
            Kind = kind;
            Level = level;
            CurrentValue = currentValue;
            NextCost = nextCost;
            IsMaxed = isMaxed;
            CanAfford = canAfford;
        }
    }

    /// <summary>
    /// 지금 판의 <b>읽기 전용 사진</b> — 코어가 표현에게 건네는 것 (TASK-WM-406).
    ///
    /// ★ 상태 자체를 안 넘긴다 — 넘기면 표현이 코어를 고칠 수 있게 되고,
    ///   그 순간 「코어만으로 게임이 돈다」가 거짓이 된다. 표현을 갈아끼울 때마다 판정이 달라진다.
    /// ★ 화면에 필요한 <b>계산된 값</b>까지 담는다 — 표현마다 같은 계산을 다시 하면 어긋난다.
    ///   3D 창과 글자 창이 다른 숫자를 보이면 그건 버그가 아니라 설계 실패다.
    /// </summary>
    public readonly struct IdleSnapshot : IGameSnapshot
    {
        /// <summary>모은 자원.</summary>
        public double Resource { get; }

        /// <summary>초당 들어오는 자원.</summary>
        public double IncomePerSecond { get; }

        /// <summary>지금까지 처치 수.</summary>
        public long Kills { get; }

        /// <summary>지금 대상의 남은 체력 비율(0~1) — 진행 막대에 쓴다.</summary>
        public double TargetHealthRatio { get; }

        /// <summary>지금 내려와 있는 단계.</summary>
        public int Stage { get; }

        /// <summary>이번 단계에서 처치한 수.</summary>
        public int KillsInStage { get; }

        /// <summary>이번 단계에 필요한 처치 수 — 「몇 남았나」는 표현이 뺄셈하지 말고 이걸 쓴다.</summary>
        public int KillsPerStage { get; }

        /// <summary>공격력 축.</summary>
        public IdleUpgradeView Damage { get; }

        /// <summary>공격속도 축.</summary>
        public IdleUpgradeView AttackSpeed { get; }

        public IdleSnapshot(double resource, double incomePerSecond, long kills, double targetHealthRatio,
            int stage, int killsInStage, int killsPerStage,
            IdleUpgradeView damage, IdleUpgradeView attackSpeed)
        {
            Resource = resource;
            IncomePerSecond = incomePerSecond;
            Kills = kills;
            TargetHealthRatio = targetHealthRatio;
            Stage = stage;
            KillsInStage = killsInStage;
            KillsPerStage = killsPerStage;
            Damage = damage;
            AttackSpeed = attackSpeed;
        }

        /// <summary>축 하나를 골라 본다 — 표현이 반복문으로 그릴 때 쓴다.</summary>
        public IdleUpgradeView ViewOf(IdleUpgradeKind kind)
        {
            return kind == IdleUpgradeKind.Damage ? Damage : AttackSpeed;
        }
    }
}
