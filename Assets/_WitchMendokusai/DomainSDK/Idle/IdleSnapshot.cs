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

        /// <summary>여태 모은 리셋 점수.</summary>
        public long PrestigePoints { get; }

        /// <summary>지금 접으면 받는 점수 — 0 이면 아직 못 접는다.</summary>
        public long PrestigeAward { get; }

        /// <summary>리셋 점수가 지금 주고 있는 배수.</summary>
        public double PrestigeMultiplier { get; }

        /// <summary>등급별로 여태 떨어진 개수 (0번째 = 1등급).</summary>
        public long[] DroppedByTier { get; }

        /// <summary>지금 단계에서 나올 수 있는 가장 높은 등급 — 「더 내려가야 하는 이유」를 그대로 보여주는 값.</summary>
        public int MaxTierNow { get; }

        /// <summary>
        /// 이번 판의 천장 — 아무리 내려가도 여기까지다.
        /// <see cref="MaxTierNow"/> 가 여기 닿았으면 <b>더 내려가도 등급은 안 열린다</b> = 접을 때다.
        /// </summary>
        public int TierCeiling { get; }

        /// <summary>여태 뽑은 가장 좋은 잠재 값(비율).</summary>
        public double BestPotentialValue { get; }

        /// <summary>그 잠재의 등급.</summary>
        public PotentialGrade BestPotentialGrade { get; }

        /// <summary>지금 자리를 비워도 되는 시간(초) — 접을수록 는다.</summary>
        public double MaxOfflineSeconds { get; }

        /// <summary>여기 머무는 중인가 — 사람이 고른 것.</summary>
        public bool HoldingStage { get; }

        /// <summary>여태 가장 깊이 간 단계 — 물러났다가 여기로 돌아올 수 있다.</summary>
        public int BestStage { get; }

        /// <summary>가장 잘 벌리는 자리 — 막혔을 때 물러날 곳.</summary>
        public int BestFarmingStage { get; }

        /// <summary>공격력 축.</summary>
        public IdleUpgradeView Damage { get; }

        /// <summary>공격속도 축.</summary>
        public IdleUpgradeView AttackSpeed { get; }

        public IdleSnapshot(double resource, double incomePerSecond, long kills, double targetHealthRatio,
            int stage, int killsInStage, int killsPerStage,
            long prestigePoints, long prestigeAward, double prestigeMultiplier,
            long[] droppedByTier, int maxTierNow, int tierCeiling,
            double bestPotentialValue, PotentialGrade bestPotentialGrade, double maxOfflineSeconds, bool holdingStage, int bestStage, int bestFarmingStage,
            IdleUpgradeView damage, IdleUpgradeView attackSpeed)
        {
            Resource = resource;
            IncomePerSecond = incomePerSecond;
            Kills = kills;
            TargetHealthRatio = targetHealthRatio;
            Stage = stage;
            KillsInStage = killsInStage;
            KillsPerStage = killsPerStage;
            PrestigePoints = prestigePoints;
            PrestigeAward = prestigeAward;
            PrestigeMultiplier = prestigeMultiplier;
            DroppedByTier = droppedByTier;
            MaxTierNow = maxTierNow;
            TierCeiling = tierCeiling;
            BestPotentialValue = bestPotentialValue;
            BestPotentialGrade = bestPotentialGrade;
            MaxOfflineSeconds = maxOfflineSeconds;
            HoldingStage = holdingStage;
            BestStage = bestStage;
            BestFarmingStage = bestFarmingStage;
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
