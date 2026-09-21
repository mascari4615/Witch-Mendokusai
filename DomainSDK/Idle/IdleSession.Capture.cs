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
                CaptureDolls(),
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
                IdleDolls.DiscoveryScoreOf(state),
                IdleDolls.DiscoveryMultiplierOf(state, tuning),
                ViewDollStat(IdleDolls.StarterId, IdleUpgradeKind.Damage, 1),
                ViewDollStat(IdleDolls.StarterId, IdleUpgradeKind.AttackSpeed, 1),
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
                state.ActiveArena.Battle.OriginX,
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
                (IdleDollGrade)tuning.PullBatchFloorGrade,
                PickupNow(),
                tuning.PickupWeight,
                IdleGacha.PickupSecondsLeft(tuning, Now()),
                IdleFreeBox.IsReady(state, tuning, Now()),
                IdleFreeBox.SecondsLeft(state, tuning, Now()),
                tuning.FreeBoxStones,
                tuning.TicketsPerDay,
                IdleDrops.MaxTierAt(state.Stage, state.Ascensions, tuning),
                CaptureDungeonCells(),
                CaptureDungeonRun(),
                state.LastDungeonResult,
                state.DungeonResultSequence,
                CaptureBattleEpoch());
        }

        /// <summary>보고 있는 전장의 재배치 번호. 던전 전장은 음수로 (본판 번호와 안 겹치게)</summary>
        private long CaptureBattleEpoch()
        {
            return state.Dungeon.Active ? -(state.Dungeon.Battle.Epoch + 1L) : state.Battle.Epoch;
        }

        private IdleDungeonCellView[] dungeonCellBuffer;

        /// <summary>던전 4 x 난이도 3 x 스테이지 5 칸 전부. 자리는 IdleDungeons.CellIndexOf</summary>
        private IdleDungeonCellView[] CaptureDungeonCells()
        {
            int total = IdleDungeons.COUNT * IdleDungeons.DIFFICULTY_COUNT * IdleDungeons.STAGE_COUNT;
            dungeonCellBuffer ??= new IdleDungeonCellView[total];
            for (int index = 0; index < total; index++)
            {
                IdleDungeonKind kind = (IdleDungeonKind)(index / (IdleDungeons.DIFFICULTY_COUNT * IdleDungeons.STAGE_COUNT));
                int difficulty = index / IdleDungeons.STAGE_COUNT % IdleDungeons.DIFFICULTY_COUNT;
                int stage = index % IdleDungeons.STAGE_COUNT;
                IdleDungeonStageSpec cell = IdleDungeons.CellOf(tuning, kind, difficulty, stage);
                dungeonCellBuffer[index] = cell == null
                    ? new IdleDungeonCellView(kind, difficulty, stage, false, false, false, 0, 0d, 0, 0d, 0L, 0L, 0)
                    : new IdleDungeonCellView(kind, difficulty, stage, true,
                        IdleDungeons.IsUnlocked(state, tuning, kind, difficulty, stage),
                        IdleDungeons.IsCleared(state, kind, difficulty, stage),
                        cell.Level, cell.TimeLimitSeconds, cell.Waves,
                        IdleDungeons.SweepGoldOf(state, tuning, kind, cell), cell.Shards, cell.GearCount,
                        IdleDungeons.GearTierOf(state, tuning, cell));
            }
            return dungeonCellBuffer;
        }

        private IdleDungeonRunView CaptureDungeonRun()
        {
            IdleDungeonRun run = state.Dungeon;
            return new IdleDungeonRunView(run.Active, run.Kind, run.Difficulty, run.Stage, run.SecondsLeft, run.TimeLimitSeconds,
                run.WavesCleared, run.Waves, run.Kills, run.Gold, run.Shards, run.Gear,
                run.Finished, run.Cleared, run.SecondsSinceFinish);
        }

        /// <summary>
        /// 자리 셋을 사진에. 체력, 부활을 화면이 다시 계산하지 않게
        ///
        /// ★ <b>판을 안 건드린다</b> — 세우는 일은 <see cref="IdleModel.Step"/> 만 한다.
        ///   묻기만 하는 자리가 판을 고치면 사진 한 장에 게임이 달라진다.
        /// </summary>
        private IdleSeatView[] CaptureSeats()
        {
            // 보고 있는 전장의 체력. 던전 판이 살아 있으면 던전 체력 (본판 체력은 뒤에서 제 길)
            IdleArena arena = state.ActiveArena;
            IdleSeatView[] made = Room(ref seatBuffer, IdleSquad.SEAT_COUNT);

            for (int seat = 0; seat < made.Length; seat++)
            {
                bool taken = IdleSquad.SeatTaken(state, seat);
                int id = taken ? state.Party[seat] : -1;
                IdleDollGrade grade = id >= 0 && IdleDolls.Knows(id)
                    ? IdleDolls.KindOf(id).Grade
                    : IdleDollGrade.Common;

                made[seat] = new IdleSeatView(
                    seat,
                    taken,
                    IdleSquad.Standing(state, arena, seat),
                    IdleSquad.HealthRatioOf(state, tuning, arena, seat),
                    IdleSquad.ReviveRatioOf(state, tuning, arena, seat),
                    id,
                    grade);
            }

            return made;
        }

        private IdleFighterView[] CaptureFighters()
        {
            IdleBattle battle = state.ActiveArena.Battle;
            IdleFighterView[] made = Room(ref fighterBuffer, IdleSquad.SEAT_COUNT);

            for (int seat = 0; seat < made.Length; seat++)
            {
                made[seat] = new IdleFighterView(
                    seat,
                    battle.Ready ? battle.X[seat] : 0d,
                    battle.Ready ? battle.Y[seat] : IdleBattleSim.LaneOf(tuning, seat),
                    IdleDolls.RangeOf(state, tuning, seat),
                    battle.Ready && battle.Moving[seat],
                    battle.Ready ? battle.Target[seat] : -1L);
            }

            return made;
        }

        private IdleFoeView[] CaptureFoes()
        {
            IdleBattle battle = state.ActiveArena.Battle;
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
            IdleBattle battle = state.ActiveArena.Battle;
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
                int owner = IdleCards.OwnerAt(state, index);
                IdleCardKind kind = IdleCards.HandAt(state, index);
                made[index] = new IdleCardView(kind,
                    IdleCards.CostOf(kind, tuning),
                    owner >= 0 && IdleCards.CanCast(state, tuning, kind),
                    owner);
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
        private IdleDollView[] CaptureDolls()
        {
            IdleDollView[] made = Room(ref dollBuffer, state.Dolls.Count);

            for (int index = 0; index < state.Dolls.Count; index++)
            {
                IdleDollOwned owned = state.Dolls[index];
                IdleDollKind kind = IdleDolls.KindOf(owned.Id);

                bool inParty = false;
                for (int slot = 0; slot < state.Party.Length; slot++)
                {
                    if (state.Party[slot] == owned.Id)
                    {
                        inParty = true;
                        break;
                    }
                }

                made[index] = new IdleDollView(
                    owned.Id,
                    kind.Name,
                    kind.Grade,
                    kind.Axis,
                    kind.Sides,
                    owned.Stars,
                    owned.Copies,
                    IdleGacha.CopiesForNextStar(owned.Stars, tuning),
                    inParty,
                    IdleDolls.OwnedShareOf(owned, tuning),
                    owned.Level,
                    IdleDolls.LevelCostOf(owned, tuning),
                    state.Resource >= IdleDolls.LevelCostOf(owned, tuning),
                    CanRaiseAnyStat(owned.Id));
            }

            return made;
        }

        // ── 사진에 쓰는 판들 ─────────────────────────────────────────────────
        //
        // ★ <b>왜 돌려 쓰나</b> — 사진은 <b>매 프레임</b> 찍힌다. 전에는 찍을 때마다 배열 다섯을
        //   새로 만들었고, 실측 <b>한 번에 2472 바이트</b>였다(가방 40칸·인형 16 기준).
        //   60프레임 x 8시간이면 <b>4 GB</b>어치 쓰레기다 — 방치형은 밤새 켜 두는 게 기본값이라
        //   그게 그대로 쌓인다. 추측이 아니라 재고 고쳤다
        //   (GC.GetAllocatedBytesForCurrentThread 로 엔진 밖에서 잰 값).
        //
        // ⚠ 그래서 <b>이 사진은 다음 사진을 찍을 때까지만 살아 있다</b>. 들고 있다가 나중에
        //   보면 그때는 다른 판이다. 지금 쓰는 자리는 전부 <b>찍자마자 쓴다</b>(화면 한 프레임,
        //   시험 한 줄). 들고 있어야 하면 그때는 <b>복사해서</b> 들어라.
        private IdleProducerView[] producerBuffer;

        private IdleDollView[] dollBuffer;

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

                for (int seat = 0; seat < IdleDolls.MAIN_SLOTS && seat < state.Party.Length; seat++)
                {
                    int dollId = state.Party[seat];
                    if (dollId < 0)
                    {
                        continue;
                    }

                    IdleItem one = IdleGear.WornOf(state, dollId, slot);
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
            // 라이브 전장이 있으면 맨 앞 적 (x 최소) 의 남은 체력. 보고 있는 전장 기준
            IdleBattle battle = state.ActiveArena.Battle;
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

