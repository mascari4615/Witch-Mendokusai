using WitchMendokusai.DomainSDK.Contracts;
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
    public sealed class IdleSession : IIntentSink<IdleRaiseUpgradeIntent>, IIntentSink<IdlePrestigeIntent>, IIntentSink<IdleAppraiseIntent>, IIntentSink<IdleHoldStageIntent>
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

        /// <summary>
        /// 자리를 비운 동안을 쳐준다 — 방치형이 방치형이 되는 지점.
        ///
        /// ★ 그냥 「지난 시간만큼 Advance」로 끝난다. 별도 계산식이 없다.
        ///   코어가 「60초를 한 번에 흘리든 0.1초씩 600번 흘리든 결과가 같다」를 보장하기 때문이다.
        ///   그 성질이 없었다면 여기서 온라인과 다른 수식을 따로 만들어야 했고,
        ///   그 순간 「자는 동안 손해」 같은 버그가 영영 따라붙는다.
        ///
        /// ★ 시계는 사람이 앞뒤로 돌릴 수 있다 — 음수는 0으로 본다(되감아도 이득이 없다).
        /// ★ 처음 시작(마지막 시각 0)은 안 쳐준다 — 1970년부터의 시간을 줄 수는 없다.
        ///
        /// <returns>실제로 쳐준 시간(초). 화면에 「자리를 비운 사이 …」로 보여줄 재료다.</returns>
        /// </summary>
        public double CatchUp(long nowUnixSeconds)
        {
            long lastSeen = state.LastSeenUnixSeconds;
            state.LastSeenUnixSeconds = nowUnixSeconds;

            if (lastSeen <= 0L)
            {
                return 0d;
            }

            double away = nowUnixSeconds - lastSeen;
            if (away <= 0d)
            {
                return 0d;
            }

            // 상한은 접은 횟수에 따라 는다 — 접으면 「덜 매여도 되는 것」도 보상이다.
            double allowed = IdleModel.MaxOfflineFor(state, tuning);
            if (away > allowed)
            {
                away = allowed;
            }

            IdleModel.Step(state, tuning, away);
            return away;
        }

        /// <summary>지금 시각을 찍어 둔다 — 저장 직전에 부른다. 이게 다음 <see cref="CatchUp"/> 의 기준점이다.</summary>
        public void MarkSeen(long nowUnixSeconds)
        {
            state.LastSeenUnixSeconds = nowUnixSeconds;
        }

        /// <summary>의도를 받는다 — 받아들여졌으면 true. 자원이 모자라거나 상한이면 아무 일도 없다.</summary>
        public bool Send(IdleRaiseUpgradeIntent intent)
        {
            return IdleModel.TryRaise(state, tuning, intent.Kind, out UpgradeRaiseFailure _);
        }

        /// <summary>여기 머물지 정한다. 언제든 뒤집을 수 있다 — 되돌릴 수 없는 선택이면 아무도 안 누른다.</summary>
        public bool Send(IdleHoldStageIntent intent)
        {
            state.HoldingStage = intent.Hold;
            return true;
        }

        /// <summary>떨어진 것 하나를 감정한다. 그 등급이 없으면 아무 일도 안 일어난다.</summary>
        public bool Send(IdleAppraiseIntent intent)
        {
            return IdlePotentials.TryAppraise(state, tuning, intent.Tier, out PotentialRoll _);
        }

        /// <summary>
        /// 감정하고 <b>무엇이 나왔는지</b>까지 돌려준다 — 화면이 결과를 보여줘야 도박이 도박이 된다.
        /// </summary>
        public bool TryAppraise(int tier, out PotentialRoll roll)
        {
            return IdlePotentials.TryAppraise(state, tuning, tier, out roll);
        }

        /// <summary>판을 접고 점수로 바꾼다. 아직 못 접으면 아무 일도 안 일어난다.</summary>
        public bool Send(IdlePrestigeIntent intent)
        {
            return IdleModel.TryPrestige(state, tuning, out long _);
        }

        /// <summary>지금 상태의 사진을 찍는다.</summary>
        public IdleSnapshot Capture()
        {
            return new IdleSnapshot(
                state.Resource,
                IdleModel.IncomePerSecond(state, tuning),
                state.Kills,
                RemainingHealthRatio(),
                state.Stage,
                state.KillsInStage,
                tuning.KillsPerStage,
                state.PrestigePoints,
                IdleModel.PrestigeAwardFor(state, tuning),
                IdleModel.PrestigeMultiplier(state, tuning),
                state.DroppedByTier,
                IdleDrops.MaxTierAt(state.Stage, state.Ascensions, tuning),
                IdleDrops.CeilingFor(state.Ascensions, tuning),
                state.BestPotentialValue,
                (PotentialGrade)state.BestPotentialGrade,
                IdleModel.MaxOfflineFor(state, tuning),
                state.HoldingStage,
                ViewOf(IdleUpgradeKind.Damage, IdleModel.DamageOf(state, tuning)),
                ViewOf(IdleUpgradeKind.AttackSpeed, IdleModel.AttackSpeedOf(state, tuning)));
        }

        private double RemainingHealthRatio()
        {
            double durability = IdleModel.TargetHealthOf(state, tuning);
            if (durability <= 0d)
            {
                return 0d;
            }

            long hitsNeeded = IdleModel.HitsToFell(state, tuning);
            if (hitsNeeded == long.MaxValue || hitsNeeded <= 0L)
            {
                return 1d;
            }

            double remaining = 1d - (double)state.HitsOnTarget / hitsNeeded;
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
