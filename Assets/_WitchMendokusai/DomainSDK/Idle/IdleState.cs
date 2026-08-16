using WitchMendokusai.DomainSDK.Upgrade;

namespace WitchMendokusai.DomainSDK.Idle
{
    /// <summary>
    /// 지금 판의 상태 — 모은 자원, 축별 레벨, 그리고 아직 안 끝난 타격의 잔여.
    ///
    /// ★ double 을 쓴다 — 방치형은 곧 float 정밀도를 넘긴다(1e7 부터 1 단위가 사라진다).
    /// ★ 잔여 피해까지 상태다 — 이걸 안 들고 있으면 짧은 스텝을 여러 번 밟을 때 피해가 매번 버려져
    ///   같은 시간을 재도 스텝 크기에 따라 결과가 달라진다. 저장·오프라인 보상이 그 위에 서므로 치명적이다.
    /// </summary>
    public sealed class IdleState
    {
        /// <summary>모은 자원.</summary>
        public double Resource { get; set; }

        /// <summary>지금까지 처치한 대상 수 — 진행감·통계용.</summary>
        public long TargetsFelled { get; set; }

        /// <summary>지금 대상에게 이미 넣어 둔 피해.</summary>
        public double DamageOnTarget { get; set; }

        /// <summary>세기 레벨.</summary>
        public UpgradeLevel Power { get; } = new UpgradeLevel();

        /// <summary>빠르기 레벨.</summary>
        public UpgradeLevel Rate { get; } = new UpgradeLevel();

        /// <summary>한 축의 레벨 상태를 고른다.</summary>
        public UpgradeLevel LevelOf(IdleUpgradeKind kind)
        {
            return kind == IdleUpgradeKind.Power ? Power : Rate;
        }
    }
}
