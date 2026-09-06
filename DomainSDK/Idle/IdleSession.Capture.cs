using WitchMendokusai.DomainSDK.Contracts;

namespace WitchMendokusai.DomainSDK.Idle
{
    // IdleSession.cs 의 Capture 조각. 같은 클래스의 partial. 상태(필드)는 원본 파일을 본다. 사진(스냅샷) 찍기와 재사용 버퍼.
    public sealed partial class IdleSession
    {
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
                state.Battle.OriginX,
                CaptureFighters(),
                CaptureFoes(),
                CaptureHits(),
                CaptureQueued(),
                CaptureTickets(),
                IdleDungeons.SecondsUntilRefill(state, tuning, Now()),
                SpeedNow,
                state.AutoCast,
                IdleShop.BagUpgradeCost(state, tuning),
                IdleShop.CanBuyBag(state, tuning),
                tuning.PullBatchCount,
                IdleGacha.BatchCostOf(state, tuning),
                IdleGacha.BatchStoneCostOf(tuning),
                IdleGacha.CanPullBatch(state, tuning),
                (IdleHeroGrade)tuning.PullBatchFloorGrade,
                PickupNow(),
                tuning.PickupWeight,
                IdleGacha.PickupSecondsLeft(tuning, Now()),
                IdleFreeBox.IsReady(state, tuning, Now()),
                IdleFreeBox.SecondsLeft(state, tuning, Now()),
                tuning.FreeBoxStones,
                tuning.TicketsPerDay,
                IdleDrops.MaxTierAt(state.Stage, state.Ascensions, tuning),
                IdleModel.IncomePerSecond(state, tuning) * tuning.DungeonGoldSeconds,
                tuning.DungeonBossShards,
                tuning.DungeonBossGear,
                tuning.DungeonGearCount);
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
    }
}

