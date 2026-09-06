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

        /// <summary>
        /// <b>사고 나면</b> 이 축이 주는 값 — 「사면 얼마나 좋아지나」를 화면이 지어내지 않게.
        ///
        /// ★ 조사에서 「이해 지원(legibility)」으로 꼽힌 자리다
        ///   (<c>refs/pattern-catalogue.md</c>): 방치형에서 이게 없으면 <b>다른 시스템의 재미가
        ///   체감 자체가 안 된다</b>. 값만 보이면 누르는 게 도박이 된다.
        /// </summary>
        public double NextValue { get; }

        /// <summary>
        /// 지금 벌이로 <b>몇 초 뒤에</b> 살 수 있나. 이미 살 수 있으면 0, 영영 못 벌면 무한.
        ///
        /// ★ 「언제 살 수 있나」를 알아야 <b>기다릴지 다른 걸 할지</b>가 결정이 된다.
        /// </summary>
        public double SecondsToAfford { get; }

        public IdleUpgradeView(IdleUpgradeKind kind, int level, double currentValue, double nextCost,
            bool isMaxed, bool canAfford, double nextValue, double secondsToAfford)
        {
            Kind = kind;
            Level = level;
            CurrentValue = currentValue;
            NextCost = nextCost;
            IsMaxed = isMaxed;
            CanAfford = canAfford;
            NextValue = nextValue;
            SecondsToAfford = secondsToAfford;
        }
    }
}

