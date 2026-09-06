using WitchMendokusai.DomainSDK.Contracts;

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
    public sealed partial class IdleSession : IIntentSink<IdleRaiseUpgradeIntent>, IIntentSink<IdlePrestigeIntent>, IIntentSink<IdleAppraiseIntent>, IIntentSink<IdleHoldStageIntent>, IIntentSink<IdleGoToStageIntent>,
        IIntentSink<IdleBuyProducerIntent>, IIntentSink<IdleMergeIntent>, IIntentSink<IdleEquipIntent>,
        IIntentSink<IdleSalvageIntent>, IIntentSink<IdleLockItemIntent>, IIntentSink<IdleSortBagIntent>,
        IIntentSink<IdlePullBatchIntent>, IIntentSink<IdleOpenFreeBoxIntent>,
        IIntentSink<IdleEnterDungeonIntent>, IIntentSink<IdleSweepDungeonIntent>,
        IIntentSink<IdleCastCardIntent>, IIntentSink<IdleNextStageIntent>
    {
        private readonly IdleState state;
        private readonly IdleTuning tuning;

        public IdleSession(IdleTuning tuning, IdleState state = null)
        {
            this.tuning = tuning ?? new IdleTuning();
            this.state = state ?? new IdleState();

            // 새 판이든 불러온 판이든 전장에 하나 (C10 시작 인형)
            IdleHeroes.EnsureStarter(this.state);
            IdleCards.EnsureDeck(this.state);
        }

        /// <summary>저장·불러오기용 — 호스트가 직렬화할 때만 만진다.</summary>
        public IdleState State => state;

        /// <summary>테스트와 호스트 구성에서 현재 튜닝을 확인할 때.</summary>
        public IdleTuning Tuning => tuning;

        /// <summary>편성 칸의 인형. 범위 밖이거나 빈 칸이면 -1.</summary>
        public int HeroAtPartySlot(int slot)
        {
            return slot >= 0 && slot < state.Party.Length ? state.Party[slot] : -1;
        }

        /// <summary>현재 판에서 해당 구역으로 옮길 수 있나.</summary>
        public bool CanGoToStage(int stage)
        {
            return IdleModel.CanGoToStage(state, stage);
        }

        /// <summary>한 인형이 해당 부위에 낀 장비.</summary>
        public IdleItem WornOf(int heroId, int slot)
        {
            return IdleGear.WornOf(state, heroId, slot);
        }

        /// <summary>한 인형이 낀 장비를 호출자가 준 배열에 복사.</summary>
        public void CopyWornOf(int heroId, IdleItem[] destination)
        {
            IdleGear.CopyWornOf(state, heroId, destination);
        }

        /// <summary>장비 하나의 최종 효과 배수.</summary>
        public double GearMultiplierOf(IdleItem item)
        {
            return IdleGear.MultiplierOfItem(item, tuning);
        }

        /// <summary>시간을 흘린다 — <b>위험 없이</b>. 자리 비운 몫·시뮬이 쓰는 길이다.</summary>
        public void Advance(double seconds)
        {
            IdleModel.Step(state, tuning, seconds);
        }

        /// <summary>
        /// 보고 있는 동안의 한 프레임 — 적이 때리고 쓰러지고 일어난다 (V2 부대층).
        ///
        /// ★ 화면만 이걸 부른다. 자는 동안은 <see cref="Advance"/> — 전멸이 없다.
        /// </summary>
        public void AdvanceLive(double seconds)
        {
            // 시계는 실시각. 배속이 날을 앞당기면 입장권이 빨리 차는 구멍
            if (clockSeconds > 0d)
            {
                clockSeconds += seconds;
                IdleDungeons.Refill(state, tuning, Now());
            }

            // 배속은 보고 있는 동안만 (P1-6). 자리 비운 몫은 실측 초당 값이라 안 부풀려짐
            IdleModel.StepLive(state, tuning, seconds * SpeedNow);

            // 코스트가 찼으면 자동으로 한 장. 켜져 있을 때만
            IdleCards.AutoCastOne(state, tuning, out IdleCardResult _);
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
            return CatchUp(nowUnixSeconds, out IdleAwayReport _);
        }

        /// <summary>
        /// 자리 비운 몫을 쳐준다 — <b>무엇을 벌었고 얼마를 흘렸는지</b>까지 돌려준다.
        ///
        /// ★ 돌아온 순간이 방치형의 보상이다. 「N 동안 잡아 뒀다」만으로는 <b>얼마나</b>가 없어
        ///   보상이 안 느껴진다. 그리고 상한에 걸렸으면 그 사실을 말해야 한다 —
        ///   말 안 하면 사용자는 몇 시간을 흘린 줄도 모르고, 상한을 올릴 이유(환생)도 안 보인다.
        /// </summary>
        public double CatchUp(long nowUnixSeconds, out IdleAwayReport report)
        {
            report = default;

            double resourceBefore = state.Resource;
            long killsBefore = state.Kills;
            int stageBefore = state.Stage;
            int bagBefore = state.Bag.Count;
            double credited = CatchUpCore(nowUnixSeconds, out double asked, out double allowed);

            report = new IdleAwayReport(
                asked,
                credited,
                asked > credited,
                allowed,
                state.Resource - resourceBefore,
                state.Kills - killsBefore,
                state.Stage - stageBefore,
                state.Bag.Count - bagBefore);

            return credited;
        }

        private double CatchUpCore(long nowUnixSeconds, out double asked, out double allowed)
        {
            asked = 0d;
            allowed = IdleModel.MaxOfflineFor(state, tuning);

            // ★ 폭주는 <b>보고 있는 동안만</b>의 것이다. 안 지우면 자리 비운 내내 7배가 걸린다 —
            //   그러면 「켜 두고 나가기」가 최적 전략이 되어 봉우리의 뜻이 뒤집힌다.
            state.SurgeKind = (int)IdleSurgeKind.None;
            state.SurgeSecondsLeft = 0d;
            state.VisitorSecondsLeft = 0d;
            state.SinceVisitorSeconds = 0d;

            // 던전 입장권은 흐른 초가 아니라 날 경계로 찬다 (economy.md 4). 실시각을 아는 유일한 자리
            clockSeconds = nowUnixSeconds;
            IdleDungeons.Refill(state, tuning, nowUnixSeconds);

            long lastSeen = state.LastSeenUnixSeconds;
            state.LastSeenUnixSeconds = nowUnixSeconds;

            if (lastSeen <= 0L)
            {
                return 0d;
            }

            double away = nowUnixSeconds - lastSeen;
            asked = away;

            if (away <= 0d)
            {
                return 0d;
            }

            // 상한은 환생 횟수에 따라 는다 — 환생하면 「덜 매여도 되는 것」도 보상이다.
            if (away > allowed)
            {
                away = allowed;
            }

            IdleModel.StepAway(state, tuning, away);
            return away;
        }

        /// <summary>지금 시각을 찍어 둔다 — 저장 직전에 부른다. 이게 다음 <see cref="CatchUp"/> 의 기준점이다.</summary>
        public void MarkSeen(long nowUnixSeconds)
        {
            state.LastSeenUnixSeconds = nowUnixSeconds;
            clockSeconds = nowUnixSeconds;
        }

        /// <summary>
        /// 지금 고른 배속 (gap-2026-08-23 P1-6). 화면이 흐른 시간에 곱하는 값
        /// </summary>
        public double SpeedNow
        {
            get
            {
                double[] steps = tuning.SpeedSteps;

                if (steps == null || steps.Length == 0)
                {
                    return 1d;
                }

                int at = state.SpeedStep;
                return at >= 0 && at < steps.Length ? steps[at] : steps[0];
            }
        }

        /// <summary>
        /// 지금 몇 시인가 (Unix 초). 복귀 때 맞추고 흐른 만큼 더하는 값
        ///
        /// ★ <c>LastSeenUnixSeconds</c> 는 저장 직전에만 찍히는 값이라 실시각이 아님.
        ///   그것으로 입장권 카운트다운을 재던 동안 화면이 늘 0 시간을 적었다 (실측 2026-09-01)
        ///
        /// ★ 배속은 안 곱함. 판이 빨라져도 <b>날은 그대로</b>
        /// </summary>
        private double clockSeconds;

        /// <summary>
        /// 시계를 초로. <b>반올림</b>.
        ///
        /// ★ 자르면 0.1 초를 600번 더한 자리에서 부동소수 오차로 1초가 샌다 (실측 2026-09-01)
        /// </summary>
        private long Now()
        {
            return (long)System.Math.Round(clockSeconds);
        }
    }
}

