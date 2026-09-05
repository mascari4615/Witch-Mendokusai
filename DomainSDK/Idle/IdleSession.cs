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
    public sealed class IdleSession : IIntentSink<IdleRaiseUpgradeIntent>, IIntentSink<IdlePrestigeIntent>, IIntentSink<IdleAppraiseIntent>, IIntentSink<IdleHoldStageIntent>, IIntentSink<IdleGoToStageIntent>,
        IIntentSink<IdleBuyProducerIntent>, IIntentSink<IdleMergeIntent>, IIntentSink<IdleEquipIntent>,
        IIntentSink<IdleSalvageIntent>, IIntentSink<IdleLockItemIntent>, IIntentSink<IdleSortBagIntent>,
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

        /// <summary>감정 버튼 한 줄에 필요한 값과 차단 사유.</summary>
        public IdleAppraiseView ViewAppraisal(int tier)
        {
            return new IdleAppraiseView(
                IdleGear.AppraiseCost(tier, tuning),
                IdlePotentials.WhyNot(state, tuning, tier));
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

        /// <summary>의도를 받는다 — 받아들여졌으면 true. 자원이 모자라거나 상한이면 아무 일도 없다.</summary>
        public bool Send(IdleRaiseUpgradeIntent intent)
        {
            return IdleModel.TryRaise(state, tuning, intent.HeroId, intent.Kind, intent.Amount);
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

        /// <summary>배속을 다음 자리로. 끝에서 처음으로</summary>
        public void CycleSpeed()
        {
            double[] steps = tuning.SpeedSteps;
            int count = steps == null || steps.Length == 0 ? 1 : steps.Length;
            state.SpeedStep = (state.SpeedStep + 1) % count;
        }

        /// <summary>설정 화면에서 배속 단계를 직접 고른다.</summary>
        public void SetSpeedStep(int step)
        {
            double[] steps = tuning.SpeedSteps;
            int count = steps == null || steps.Length == 0 ? 1 : steps.Length;
            state.SpeedStep = step < 0 ? 0 : step >= count ? count - 1 : step;
        }

        /// <summary>자동 시전 켜고 끄기</summary>
        public void ToggleAutoCast()
        {
            state.AutoCast = state.AutoCast == false;
        }

        /// <summary>가방을 한 묶음 넓힌다 (상점). 골드가 모자라거나 상한이면 아무 일도 없다</summary>
        public bool BuyBagUpgrade()
        {
            return IdleShop.TryBuyBag(state, tuning);
        }

        /// <summary>인형 레벨을 한 칸 올린다 (economy.md 표 3). 골드가 모자라면 아무 일도 없다</summary>
        public bool RaiseHeroLevel(int heroId)
        {
            return IdleHeroes.TryRaiseLevel(state, tuning, heroId);
        }

        /// <summary>
        /// 손으로 한 대. <b>늘 받아들여진다</b> — 모을 것이 필요 없는 유일한 행동이다.
        /// </summary>
        public bool Send(IdleTapIntent intent)
        {
            IdleModel.Tap(state, tuning);
            return true;
        }

        /// <summary>영웅을 한 번 뽑는다. 자원이 모자라면 아무 일도 안 일어난다.</summary>
        public bool TryPull(out IdleHeroPull pull)
        {
            return IdleGacha.TryPull(state, tuning, out pull);
        }

        /// <summary>영웅을 한 번 뽑는다 (결과가 필요 없을 때).</summary>
        public bool Send(IdlePullHeroIntent intent)
        {
            return IdleGacha.TryPull(state, tuning, out IdleHeroPull _);
        }

        /// <summary>
        /// 자리에 영웅을 앉힌다. 그 영웅이 이미 <b>다른 자리</b>에 있으면 둘을 맞바꾼다 —
        /// 같은 얼굴이 두 자리를 먹으면 셋을 고르는 뜻이 사라진다.
        /// </summary>
        public bool Send(IdleSetPartyIntent intent)
        {
            if (intent.Slot < 0 || intent.Slot >= state.Party.Length)
            {
                return false;
            }

            if (intent.HeroId >= 0 && state.IndexOfHero(intent.HeroId) < 0)
            {
                return false;
            }

            // ⚠ 빈 자리는 -1 로 적힌다. 그래서 <b>빼는</b> 요청(-1)에 이 맞바꿈을 그대로 태우면
            //   다른 빈 자리들이 전부 「같은 영웅」으로 잡혀 <b>빼려던 영웅이 두 자리에 복제</b>된다
            //   ([5,-1,-1] 에서 0번을 비우면 [-1,5,5]). 맞바꿈은 <b>진짜 영웅일 때만</b>이다.
            if (intent.HeroId >= 0)
            {
                for (int slot = 0; slot < state.Party.Length; slot++)
                {
                    if (slot != intent.Slot && state.Party[slot] == intent.HeroId)
                    {
                        state.Party[slot] = state.Party[intent.Slot];
                    }
                }
            }

            state.Party[intent.Slot] = intent.HeroId;
            return true;
        }

        /// <summary>생산자를 하나 산다. 자원이 모자라면 아무 일도 안 일어난다.</summary>
        public bool Send(IdleBuyProducerIntent intent)
        {
            return IdleBase.TryBuy(state, tuning, intent.Kind);
        }

        /// <summary>같은 부위·같은 등급 셋을 합친다.</summary>
        public bool Send(IdleMergeIntent intent)
        {
            return IdleGear.TryMerge(state, tuning, intent.Tier, intent.Slot, out IdleItem _);
        }

        public bool Send(IdleSalvageIntent intent)
        {
            return IdleGear.TrySalvage(state, tuning, intent.Tier, intent.Count, out int _, out double _);
        }

        /// <summary>분해 결과를 돌려주는 길. 화면이 「n개 분해, 골드 +g」를 말한다</summary>
        public bool TrySalvage(int tier, int count, out int salvaged, out double gold)
        {
            return IdleGear.TrySalvage(state, tuning, tier, count, out salvaged, out gold);
        }

        /// <summary>분해 미리보기. 몇 개가 되고 골드가 얼마인지</summary>
        public void ViewSalvage(int tier, int count, out int available, out double gold)
        {
            available = IdleGear.CountSalvageable(state, tier);
            int would = count > 0 && count < available ? count : available;
            gold = IdleGear.SalvageGold(tier, tuning) * would;
        }

        public bool Send(IdleLockItemIntent intent)
        {
            return IdleGear.TrySetLocked(state, intent.BagIndex, intent.Locked);
        }

        public bool Send(IdleSortBagIntent intent)
        {
            IdleGear.SortBag(state);
            return true;
        }

        /// <summary>가방의 것을 찬다.</summary>
        public bool Send(IdleEquipIntent intent)
        {
            return IdleGear.TryEquip(state, intent.HeroId, intent.BagIndex);
        }

        /// <summary>
        /// 지나온 자리로 옮긴다. 앞질러 가려 하면 아무 일도 안 일어난다.
        ///
        /// ★ 옮기면 <b>부대가 회복한다</b> — 물러나는 것이 재정비가 아니면 물러날 이유가 없다.
        /// </summary>
        public bool Send(IdleGoToStageIntent intent)
        {
            if (IdleModel.TryGoToStage(state, intent.Stage) == false)
            {
                return false;
            }

            IdleSquad.HealAll(state, tuning);
            return true;
        }

        /// <summary>반복을 끝내고 다음 구역에 다시 도전한다 (V2 방향 6).</summary>
        public bool Send(IdleNextStageIntent intent)
        {
            return IdleSquad.TryAdvanceStage(state, tuning);
        }

        /// <summary>여기 머물지 정한다. 언제든 뒤집을 수 있다 — 되돌릴 수 없는 선택이면 아무도 안 누른다.</summary>
        public bool Send(IdleHoldStageIntent intent)
        {
            state.HoldingStage = intent.Hold;
            return true;
        }

        /// <summary>카드 한 장을 낸다. 코스트가 모자라면 아무 일도 안 일어난다 (V2).</summary>
        public bool Send(IdleCastCardIntent intent)
        {
            return IdleCards.TryCastHand(state, tuning, intent.HandIndex, out IdleCardResult _);
        }

        /// <summary>카드를 내고 <b>무슨 일이 났는지</b>까지 돌려준다 — 감정 카드의 굴림을 화면이 보여주게.</summary>
        public bool TryCastCard(int handIndex, out IdleCardResult result)
        {
            return IdleCards.TryCastHand(state, tuning, handIndex, out result);
        }

        public bool TryCastCardAt(int handIndex, long foeIndex, out IdleCardResult result)
        {
            return IdleCards.TryCastHandAt(state, tuning, handIndex, foeIndex, out result);
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

        /// <summary>판을 환생하고 점수로 바꾼다. 아직 못 환생하면 아무 일도 안 일어난다.</summary>
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
                IdleModel.PrestigeNextPayingStage(state, tuning),
                IdleModel.PrestigeMultiplier(state, tuning),
                state.DroppedByTier,
                IdleDrops.MaxTierAt(state.Stage, state.Ascensions, tuning),
                IdleDrops.CeilingFor(state.Ascensions, tuning),
                CaptureProducers(),
                CaptureBag(),
                CaptureWorn(),
                IdleShop.BagCapacityOf(state, tuning),
                tuning.MergeCount,
                state.BestPotentialValue,
                (PotentialGrade)state.BestPotentialGrade,
                IdleModel.MaxOfflineFor(state, tuning),
                state.HoldingStage,
                state.BestStage,
                IdleModel.BestFarmingStage(state, tuning),
                CaptureHeroes(),
                CaptureParty(),
                IdleGacha.CostOf(state, tuning),
                IdleGacha.StoneCostOf(tuning),
                state.Stones,
                state.VisitorSecondsLeft,
                (IdleSurgeKind)state.SurgeKind,
                state.SurgeSecondsLeft,
                IdleSurge.MultiplierOfKind(state, tuning),
                IdleGacha.CanPull(state, tuning),
                tuning.PityPulls - state.PullsSincePity,
                tuning.LegendChance,
                tuning.EpicChance,
                tuning.RareChance,
                IdleHeroes.CodexScoreOf(state),
                IdleHeroes.CodexMultiplierOf(state, tuning),
                ViewHeroStat(IdleHeroes.STARTER_ID, IdleUpgradeKind.Damage, 1),
                ViewHeroStat(IdleHeroes.STARTER_ID, IdleUpgradeKind.AttackSpeed, 1),
                IdleModel.AttackSpeedOf(state, tuning),
                state.Cost,
                tuning.CostMax,
                state.SupplySecondsLeft,
                CaptureCards(),
                CaptureSeats(),
                state.Repeating,
                state.ClearedStage,
                IdleSquad.EnemyDamagePerSecond(state, tuning),
                state.HitsOnTarget,
                CaptureFighters(),
                CaptureFoes(),
                CaptureHits(),
                CaptureQueued(),
                CaptureTickets(),
                IdleDungeons.SecondsUntilRefill(state, tuning, Now()),
                SpeedNow,
                state.AutoCast,
                IdleShop.BagUpgradeCost(state, tuning),
                IdleShop.CanBuyBag(state, tuning));
        }

        /// <summary>
        /// 자리 셋을 사진에. 체력, 부활을 화면이 다시 계산하지 않게
        ///
        /// ★ <b>판을 안 건드린다</b> — 세우는 일은 <see cref="IdleModel.Step"/> 만 한다.
        ///   묻기만 하는 자리가 판을 고치면 사진 한 장에 게임이 달라진다.
        /// </summary>
        private IdleSeatView[] CaptureSeats()
        {
            IdleSeatView[] made = Room(ref seatBuffer, IdleSquad.SEAT_COUNT);

            for (int seat = 0; seat < made.Length; seat++)
            {
                bool taken = IdleSquad.SeatTaken(state, seat);
                int id = taken ? state.Party[seat] : -1;
                IdleHeroGrade grade = id >= 0 && IdleHeroes.Knows(id)
                    ? IdleHeroes.KindOf(id).Grade
                    : IdleHeroGrade.Common;

                made[seat] = new IdleSeatView(
                    seat,
                    taken,
                    IdleSquad.Standing(state, seat),
                    IdleSquad.HealthRatioOf(state, tuning, seat),
                    IdleSquad.ReviveRatioOf(state, tuning, seat),
                    id,
                    grade);
            }

            return made;
        }

        private IdleFighterView[] CaptureFighters()
        {
            IdleBattle battle = state.Battle;
            IdleFighterView[] made = Room(ref fighterBuffer, IdleSquad.SEAT_COUNT);

            for (int seat = 0; seat < made.Length; seat++)
            {
                made[seat] = new IdleFighterView(
                    seat,
                    battle.Ready ? battle.X[seat] : 0d,
                    battle.Ready ? battle.Y[seat] : IdleBattleSim.LaneOf(tuning, seat),
                    IdleHeroes.RangeOf(state, tuning, seat),
                    battle.Ready && battle.Moving[seat],
                    battle.Ready ? battle.Target[seat] : -1L);
            }

            return made;
        }

        private IdleFoeView[] CaptureFoes()
        {
            IdleBattle battle = state.Battle;
            IdleFoeView[] made = Room(ref foeBuffer, battle.Ready ? battle.Foes.Count : 0);

            for (int at = 0; at < made.Length; at++)
            {
                IdleFoe foe = battle.Foes[at];
                made[at] = new IdleFoeView(foe.Index, foe.Kind, foe.Boss, foe.X, foe.Y, foe.HealthRatio, foe.Range);
            }

            return made;
        }

        private IdleHit[] CaptureHits()
        {
            IdleBattle battle = state.Battle;
            IdleHit[] made = Room(ref hitBuffer, battle.Hits.Count);

            for (int at = 0; at < made.Length; at++)
            {
                made[at] = battle.Hits[at];
            }

            return made;
        }

        /// <summary>손패를 사진에 담는다 — 값·가능 여부를 화면이 다시 계산하지 않게.</summary>
        private IdleCardView[] CaptureCards()
        {
            IdleCardView[] made = Room(ref cardBuffer, IdleCards.CARD_COUNT);

            for (int index = 0; index < made.Length; index++)
            {
                IdleCardKind kind = IdleCards.HandAt(state, index);
                made[index] = new IdleCardView(kind,
                    IdleCards.CostOf(kind, tuning),
                    IdleCards.CanCast(state, tuning, kind));
            }

            return made;
        }

        /// <summary>입장권을 사진에 담는다 (economy.md 4)</summary>
        private long[] CaptureTickets()
        {
            state.EnsureTicketRoom();
            return state.Tickets;
        }

        /// <summary>줄 선 카드를 사진에 담는다 — 순환이 눈에 보이게 (gap-2026-08-23 P1)</summary>
        private IdleCardKind[] CaptureQueued()
        {
            if (queuedBuffer == null || queuedBuffer.Length != IdleCards.QUEUE_SIZE)
            {
                queuedBuffer = new IdleCardKind[IdleCards.QUEUE_SIZE];
            }

            for (int index = 0; index < queuedBuffer.Length; index++)
            {
                queuedBuffer[index] = IdleCards.QueuedAt(state, index);
            }

            return queuedBuffer;
        }

        /// <summary>시간을 흘린다 — <b>보고 있는 동안만</b> 도는 층(지나가는 것·폭주).</summary>
        public void AdvanceSurge(double seconds)
        {
            IdleSurge.Advance(state, tuning, seconds);
        }

        /// <summary>지나가는 것을 잡는다.</summary>
        public bool TryCatchVisitor(out IdleSurgeKind caught)
        {
            return IdleSurge.TryCatch(state, tuning, out caught);
        }

        /// <summary>도감을 사진에 담는다 — 화면이 등급표·별 셈을 다시 하지 않게.</summary>
        private IdleHeroView[] CaptureHeroes()
        {
            IdleHeroView[] made = Room(ref heroBuffer, state.Heroes.Count);

            for (int index = 0; index < state.Heroes.Count; index++)
            {
                IdleHeroOwned owned = state.Heroes[index];
                IdleHeroKind kind = IdleHeroes.KindOf(owned.Id);

                bool inParty = false;
                for (int slot = 0; slot < state.Party.Length; slot++)
                {
                    if (state.Party[slot] == owned.Id)
                    {
                        inParty = true;
                        break;
                    }
                }

                made[index] = new IdleHeroView(
                    owned.Id,
                    kind.Name,
                    kind.Grade,
                    kind.Axis,
                    kind.Sides,
                    owned.Stars,
                    owned.Copies,
                    IdleGacha.CopiesForNextStar(owned.Stars, tuning),
                    inParty,
                    IdleHeroes.OwnedShareOf(owned, tuning),
                    owned.Level,
                    IdleHeroes.LevelCostOf(owned, tuning),
                    state.Resource >= IdleHeroes.LevelCostOf(owned, tuning),
                    CanRaiseAnyStat(owned.Id));
            }

            return made;
        }

        private bool CanRaiseAnyStat(int heroId)
        {
            for (int stat = 0; stat <= (int)IdleUpgradeKind.Recovery; stat++)
            {
                if (IdleModel.TryGetCost(state, tuning, heroId, (IdleUpgradeKind)stat, 1, out double cost)
                    && state.Resource >= cost)
                {
                    return true;
                }
            }

            return false;
        }

        // ── 사진에 쓰는 판들 ─────────────────────────────────────────────────
        //
        // ★ <b>왜 돌려 쓰나</b> — 사진은 <b>매 프레임</b> 찍힌다. 전에는 찍을 때마다 배열 다섯을
        //   새로 만들었고, 실측 <b>한 번에 2472 바이트</b>였다(가방 40칸·영웅 16 기준).
        //   60프레임 x 8시간이면 <b>4 GB</b>어치 쓰레기다 — 방치형은 밤새 켜 두는 게 기본값이라
        //   그게 그대로 쌓인다. 추측이 아니라 재고 고쳤다
        //   (GC.GetAllocatedBytesForCurrentThread 로 엔진 밖에서 잰 값).
        //
        // ⚠ 그래서 <b>이 사진은 다음 사진을 찍을 때까지만 살아 있다</b>. 들고 있다가 나중에
        //   보면 그때는 다른 판이다. 지금 쓰는 자리는 전부 <b>찍자마자 쓴다</b>(화면 한 프레임,
        //   시험 한 줄). 들고 있어야 하면 그때는 <b>복사해서</b> 들어라.
        private IdleProducerView[] producerBuffer;
        private IdleHeroView[] heroBuffer;
        private IdleItem[] bagBuffer;
        private IdleItem[] wornBuffer;
        private int[] partyBuffer;
        private IdleCardView[] cardBuffer;
        private IdleCardKind[] queuedBuffer;

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
        private IdleSeatView[] seatBuffer;
        private IdleFighterView[] fighterBuffer;
        private IdleFoeView[] foeBuffer;
        private IdleHit[] hitBuffer;

        /// <summary>자리를 맞춰 준다 — 수가 그대로면 쓰던 판을 그대로 쓴다.</summary>
        private static T[] Room<T>(ref T[] buffer, int count)
        {
            if (buffer == null || buffer.Length != count)
            {
                buffer = new T[count];
            }

            return buffer;
        }

        private IdleItem[] CaptureBag()
        {
            IdleItem[] made = Room(ref bagBuffer, state.Bag.Count);

            for (int index = 0; index < made.Length; index++)
            {
                made[index] = state.Bag[index];
            }

            return made;
        }

        private IdleItem[] CaptureWorn()
        {
            // 사진은 <b>전장에 선 인형들</b>의 장비를 부위마다 하나로 요약.
            // 화면이 한 인형 것을 보려면 IdleGear.CopyWornOf 를 쓴다 (인형별, 2026-08-31)
            IdleItem[] made = Room(ref wornBuffer, IdleGear.SLOT_COUNT);

            for (int slot = 0; slot < made.Length; slot++)
            {
                made[slot] = default;

                for (int seat = 0; seat < IdleHeroes.MAIN_SLOTS && seat < state.Party.Length; seat++)
                {
                    int heroId = state.Party[seat];
                    if (heroId < 0)
                    {
                        continue;
                    }

                    IdleItem one = IdleGear.WornOf(state, heroId, slot);
                    if (one.IsEmpty == false)
                    {
                        made[slot] = one;
                        break;
                    }
                }
            }
            return made;
        }

        private int[] CaptureParty()
        {
            int[] made = Room(ref partyBuffer, state.Party.Length);
            System.Array.Copy(state.Party, made, made.Length);
            return made;
        }

        /// <summary>기지를 사진에 담는다 — 화면이 값·산출을 다시 계산하지 않게.</summary>
        private IdleProducerView[] CaptureProducers()
        {
            state.EnsureProducerRoom(tuning.ProducerCount);

            IdleProducerView[] made = Room(ref producerBuffer, tuning.ProducerCount);

            for (int kind = 0; kind < tuning.ProducerCount; kind++)
            {
                long owned = state.Owned[kind];
                double cost = IdleBase.CostOf(kind, owned, tuning);
                double each = IdleBase.OutputOf(kind, tuning);

                made[kind] = new IdleProducerView(
                    kind,
                    owned,
                    cost,
                    owned * each,
                    state.Resource >= cost,
                    IdleBase.IsHidden(kind, state),
                    IncomeGainOf(kind),
                    SecondsToAfford(cost, true));
            }

            return made;
        }

        private double RemainingHealthRatio()
        {
            // 라이브 전장이 있으면 맨 앞 적 (x 최소) 의 남은 체력
            IdleBattle battle = state.Battle;
            if (battle.Ready && battle.Foes.Count > 0)
            {
                IdleFoe nearest = null;
                for (int at = 0; at < battle.Foes.Count; at++)
                {
                    if (nearest == null || battle.Foes[at].X < nearest.X)
                    {
                        nearest = battle.Foes[at];
                    }
                }

                return nearest.HealthRatio;
            }

            double durability = IdleModel.TargetHealthOf(state, tuning);
            if (durability <= 0d)
            {
                return 0d;
            }

            double hitsNeeded = IdleModel.HitsToFell(state, tuning);
            if (double.IsInfinity(hitsNeeded) || hitsNeeded <= 0d)
            {
                return 1d;
            }

            double remaining = 1d - state.HitsOnTarget / hitsNeeded;
            if (remaining < 0d)
            {
                return 0d;
            }

            return remaining > 1d ? 1d : remaining;
        }

        public IdleUpgradeView ViewHeroStat(int heroId, IdleUpgradeKind kind, int amount)
        {
            int index = state.IndexOfHero(heroId);
            if (index < 0)
            {
                return new IdleUpgradeView(kind, 0, 0d, 0d, true, false, 0d, 0d);
            }

            IdleHeroOwned owned = state.Heroes[index];
            int level = owned.StatLevel(kind);
            bool hasNext = IdleModel.TryGetCost(state, tuning, heroId, kind, amount, out double nextCost);

            return new IdleUpgradeView(
                kind,
                level,
                HeroStatValue(heroId, kind),
                nextCost,
                hasNext == false,
                hasNext && state.Resource >= nextCost,
                ValueAfterRaising(heroId, kind, amount),
                SecondsToAfford(nextCost, hasNext));
        }

        /// <summary>
        /// 한 단계 올린 <b>뒤의</b> 값 — 실제로 올려 보고 되돌린다.
        ///
        /// ★ 공식을 화면이나 여기서 다시 쓰지 않는다. 두 번 쓰면 언젠가 갈리고,
        ///   그러면 <b>버튼이 거짓말</b>을 한다(사면 다른 값이 나온다).
        /// </summary>
        private double ValueAfterRaising(int heroId, IdleUpgradeKind kind, int amount)
        {
            int index = state.IndexOfHero(heroId);
            if (index < 0)
            {
                return 0d;
            }

            IdleHeroOwned before = state.Heroes[index];
            IdleHeroOwned afterOwned = before;
            afterOwned.SetStatLevel(kind, before.StatLevel(kind) + amount);
            state.Heroes[index] = afterOwned;
            double after = HeroStatValue(heroId, kind);
            state.Heroes[index] = before;

            return after;
        }

        private double HeroStatValue(int heroId, IdleUpgradeKind kind)
        {
            switch (kind)
            {
                case IdleUpgradeKind.Damage:
                    return IdleModel.DamageOfHero(state, tuning, heroId);
                case IdleUpgradeKind.AttackSpeed:
                    return IdleModel.AttackSpeedOfHero(state, tuning, heroId);
                case IdleUpgradeKind.MaxHealth:
                    return IdleSquad.MaxHealthOfHero(state, tuning, heroId);
                case IdleUpgradeKind.Defense:
                    double defense = IdleHeroes.DefenseOf(state, tuning, heroId);
                    return 1d - 1d / (1d + defense);
                case IdleUpgradeKind.CriticalChance:
                    return IdleHeroes.CriticalChanceOf(state, tuning, heroId);
                case IdleUpgradeKind.CriticalDamage:
                    return IdleHeroes.CriticalDamageOf(state, tuning, heroId);
                default:
                    return IdleHeroes.HealPerKillShareOf(state, tuning, heroId);
            }
        }

        /// <summary>
        /// 이 생산자를 하나 더 사면 <b>초당 수입이 몇 배</b>가 되나.
        ///
        /// ★ 공식을 화면이 다시 쓰지 않게. 두 번 쓰면 언젠가 갈리고 버튼이 거짓말을 한다.
        ///
        /// ★ 배수(장비·영웅·도감·폭주)는 사도 안 사도 <b>똑같이</b> 곱해져 비율에서 지워진다 —
        ///   그래서 바닥(<see cref="IdleBase.RawOutputPerSecond"/>)만으로 잰다. 값은 전과 같다.
        ///
        /// ⚠ 전에는 <b>생산자를 하나 얹었다 되돌리며</b> 쟀다. 조회하는 자리가 판을 건드리면,
        ///   그 사이에 무슨 일이 나는 순간 공짜 생산자가 남는다 — 그런 자리는 안 만드는 게 낫다.
        ///   덤으로 훑기가 두 번에서 한 번이 된다(화면이 매 프레임 생산자마다 부른다).
        /// </summary>
        private double IncomeGainOf(int kind)
        {
            double before = IdleBase.RawOutputPerSecond(state, tuning);

            if (before <= 0d)
            {
                return double.PositiveInfinity;
            }

            return (before + IdleBase.OutputOf(kind, tuning)) / before;
        }

        /// <summary>지금 벌이로 이 값을 모으는 데 걸리는 시간(초).</summary>
        private double SecondsToAfford(double cost, bool hasNext)
        {
            if (hasNext == false || state.Resource >= cost)
            {
                return 0d;
            }

            double perSecond = IdleModel.IncomePerSecond(state, tuning);
            if (perSecond <= 0d)
            {
                return double.PositiveInfinity;
            }

            return (cost - state.Resource) / perSecond;
        }
    }
}
