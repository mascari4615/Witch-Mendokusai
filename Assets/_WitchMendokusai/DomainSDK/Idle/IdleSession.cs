using WitchMendokusai.DomainSDK.Presentation;
using WitchMendokusai.DomainSDK.Upgrade;

namespace WitchMendokusai.DomainSDK.Idle
{
    /// <summary>
    /// 코어와 표현 사이의 <b>유일한 창구</b> (TASK-WM-406).
    ///
    /// 코어(<see cref="IdleModel"/>)는 static 순수 함수로 남겨 둔다 — 그래야 EditMode 에서 수천 판을 굴린다.
    /// 대신 상태를 들고 다니며 「의도를 받고 사진을 내주는」 얇은 층이 하나 필요하다. 그게 여기다.
    ///
    /// ★ 표현은 이 클래스만 안다 — <see cref="IdleState"/> 도 <see cref="IdleModel"/> 도 모른다.
    ///   그래서 표현을 3D 로 갈아도, 글자로 갈아도 코어에 손이 안 닿는다.
    /// ★ 이 클래스도 Unity 를 모른다 — 시간은 <see cref="Advance"/> 로 <b>밖에서</b> 흘려 준다.
    ///   에디터 창이 흘리든, 런타임 Update 가 흘리든, 시험이 8시간을 한 번에 흘리든 같다.
    /// </summary>
    public sealed class IdleSession : IIntentSink<IdleRaiseUpgradeIntent>
    {
        private readonly IdleState state;
        private readonly IdleTuning tuning;

        public IdleSession(IdleTuning tuning, IdleState state = null)
        {
            this.tuning = tuning ?? new IdleTuning();
            this.state = state ?? new IdleState();
        }

        /// <summary>저장·불러오기용 — 호스트가 직렬화할 때만 만진다.</summary>
        public IdleState State => state;

        /// <summary>시간을 흘린다. 얼마를 흘릴지는 부르는 쪽이 정한다.</summary>
        public void Advance(double seconds)
        {
            IdleModel.Step(state, tuning, seconds);
        }

        /// <summary>의도를 받는다 — 받아들여졌으면 true. 자원이 모자라거나 상한이면 아무 일도 없다.</summary>
        public bool Send(IdleRaiseUpgradeIntent intent)
        {
            return IdleModel.TryRaise(state, tuning, intent.Kind, out UpgradeRaiseFailure _);
        }

        /// <summary>지금 상태의 사진을 찍는다.</summary>
        public IdleSnapshot Capture()
        {
            return new IdleSnapshot(
                state.Resource,
                IdleModel.IncomePerSecond(state, tuning),
                state.Kills,
                RemainingHealthRatio(),
                ViewOf(IdleUpgradeKind.Damage, IdleModel.DamageOf(state, tuning)),
                ViewOf(IdleUpgradeKind.AttackSpeed, IdleModel.AttackSpeedOf(state, tuning)));
        }

        private double RemainingHealthRatio()
        {
            if (tuning.TargetHealth <= 0d)
            {
                return 0d;
            }

            double remaining = 1d - state.DamageDealtToTarget / tuning.TargetHealth;
            if (remaining < 0d)
            {
                return 0d;
            }

            return remaining > 1d ? 1d : remaining;
        }

        private IdleUpgradeView ViewOf(IdleUpgradeKind kind, double currentValue)
        {
            UpgradeLevel level = state.LevelOf(kind);
            bool hasNext = IdleModel.TryGetNextCost(state, tuning, kind, out double nextCost);

            return new IdleUpgradeView(
                kind,
                level.Level,
                currentValue,
                nextCost,
                hasNext == false,
                hasNext && state.Resource >= nextCost);
        }
    }
}
