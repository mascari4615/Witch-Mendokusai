using System;

namespace WitchMendokusai.DomainSDK.Idle
{
    /// <summary>
    /// 사거리 전투 시뮬레이션 (combat.md). 라이브 층 전용
    ///
    /// ★ 2D 평면. 인형은 가장 가까운 적을 향해 사거리 안까지 걷기, 적은 맨 앞 인형을 향해 걷기.
    ///   앞줄은 사거리의 결과 (R2). 적도 사거리 보유 (R3)
    /// ★ 틱 고정 <see cref="IdleTuning.BattleTickSeconds"/>. 프레임 길이는 이월. 60s 한 번과 0.1s 600번이 동일
    /// ★ 엔진 API 없음. <c>double</c> 과 <c>IdleRandom</c> 만. 같은 판, 같은 시간이면 같은 결과
    /// ★ 처치, 드롭, 회복, 구역 이동은 지금 코어 함수 재사용. 여기는 위치와 타격만
    /// ★ 전장은 <see cref="IdleArena"/> 로 받음 (changes/idle-dungeon-run v2). 본판과 던전이 같은 시뮬을 따로 돎.
    ///   구역 진행, 실측, 전멸 후퇴는 본판만. 던전은 칸 규칙 (IdleDungeons) 이 처치와 끝을 정함
    /// </summary>
    public static partial class IdleBattleSim
    {
        private const double EPSILON = 1e-9d;

        /// <summary>한 틱에 한 자리가 넣을 수 있는 타격 상한. 공격 속도가 아주 높아도 멎지 않게</summary>
        private const int MAX_HITS_PER_TICK = 256;

        /// <summary>시간 진행. 본판 전장 먼저, 던전 판이 살아 있으면 그 전장도 같은 시간만큼</summary>
        public static void Advance(IdleState state, IdleTuning tuning, double seconds)
        {
            if (seconds <= 0d || double.IsNaN(seconds))
            {
                state.Battle.Hits.Clear();
                state.Dungeon.Battle.Hits.Clear();
                return;
            }

            IdleHeroes.EnsureStarter(state);
            state.EnsureSeatRoom(tuning);

            AdvanceArena(state, tuning, state.MainArena, seconds);

            if (state.Dungeon.Active)
            {
                AdvanceArena(state, tuning, state.Dungeon.Arena, seconds);
            }
        }

        /// <summary>전장 하나의 시간 진행. 이월을 더해 틱 수만큼. 타격 목록은 부를 때마다 새로</summary>
        private static void AdvanceArena(IdleState state, IdleTuning tuning, in IdleArena arena, double seconds)
        {
            IdleBattle battle = arena.Battle;
            battle.Hits.Clear();

            if (arena.Dungeon && state.Dungeon.Finished)
            {
                // 끝난 판. 전장은 멈춘 채 결과까지의 초만 (Reset 이 적을 다시 세우면 안 됨)
                IdleDungeons.TickRun(state, tuning, seconds);
                return;
            }

            if (battle.Ready == false || (arena.Dungeon == false && battle.StageSeen != state.Stage))
            {
                Reset(state, tuning, arena);
            }

            double tick = tuning.BattleTickSeconds > 0d ? tuning.BattleTickSeconds : 0.1d;
            battle.Carry += seconds;
            long ticks = (long)(battle.Carry / tick + EPSILON);

            if (ticks > tuning.BattleTicksPerCall)
            {
                // 라이브 전용 층. 넘친 시간은 폐기 (프레임 드랍 보호)
                ticks = tuning.BattleTicksPerCall;
                battle.Carry = 0d;
            }
            else
            {
                battle.Carry -= ticks * tick;
                if (battle.Carry < 0d)
                {
                    battle.Carry = 0d;
                }
            }

            CacheSeatStats(state, tuning, arena);

            for (long at = 0; at < ticks; at++)
            {
                if (Tick(state, tuning, arena, tick) > 0L)
                {
                    // 처치는 드롭, 회복, 구역 이동을 부른다. 스탯이 바뀔 수 있는 유일한 자리
                    CacheSeatStats(state, tuning, arena);
                }

                if (arena.Dungeon && (state.Dungeon.Active == false || state.Dungeon.Finished))
                {
                    // 판이 끝남. 남은 틱은 본판만의 것 (끝난 판의 결과 대기 초는 TickRun 이 셈)
                    IdleDungeons.TickRun(state, tuning, (ticks - at - 1) * tick);
                    break;
                }
            }
        }

        /// <summary>
        /// 자리별 피해, 공격 간격, 사거리를 한 번에 셈
        ///
        /// ★ 틱마다 셈하면 한 시간 시뮬 116ms 중 113ms 가 여기 (실측 2026-09-05, 108k 호출).
        ///   Advance 안에서 스탯이 바뀌는 길은 처치뿐. 그 뒤에만 다시 셈
        /// </summary>
        private static void CacheSeatStats(IdleState state, IdleTuning tuning, in IdleArena arena)
        {
            IdleBattle battle = arena.Battle;

            for (int seat = 0; seat < IdleSquad.SEAT_COUNT; seat++)
            {
                if (IdleSquad.SeatTaken(state, seat) == false)
                {
                    battle.StatDamage[seat] = 0d;
                    battle.StatInterval[seat] = double.PositiveInfinity;
                    battle.StatRange[seat] = 0d;
                    continue;
                }

                int heroId = state.Party[seat];
                double perSecond = IdleModel.AttackSpeedOfHero(state, tuning, heroId);
                battle.StatDamage[seat] = IdleModel.DamageOfHero(state, tuning, heroId);
                battle.StatInterval[seat] = perSecond > 0d ? 1d / perSecond : double.PositiveInfinity;
                battle.StatRange[seat] = IdleHeroes.RangeOf(state, tuning, seat);
            }
        }

        /// <summary>본판 전장 새로 세우기</summary>
        public static void Reset(IdleState state, IdleTuning tuning)
        {
            Reset(state, tuning, state.MainArena);
        }

        /// <summary>전장 새로 세우기. 인형은 뒤에 줄지어, 첫 웨이브는 앞에</summary>
        public static void Reset(IdleState state, IdleTuning tuning, in IdleArena arena)
        {
            IdleBattle battle = arena.Battle;

            // 다시 세우기 전에 지금 서 있던 자리를 원점에 접음. 안 그러면 화면이 뒤로 튐
            double standing = double.PositiveInfinity;
            for (int seat = 0; seat < IdleSquad.SEAT_COUNT; seat++)
            {
                if (IdleSquad.SeatTaken(state, seat) && battle.X[seat] < standing)
                {
                    standing = battle.X[seat];
                }
            }

            if (double.IsInfinity(standing) == false)
            {
                battle.OriginX += standing;
            }

            battle.Foes.Clear();
            battle.Wave = 0;
            battle.Carry = 0d;
            battle.Ready = true;
            battle.StageSeen = state.Stage;
            battle.Epoch += 1L;

            for (int seat = 0; seat < IdleSquad.SEAT_COUNT; seat++)
            {
                battle.X[seat] = -seat * tuning.SeatBackStep;
                battle.Y[seat] = LaneOf(tuning, seat);
                battle.Cooldown[seat] = 0d;
                battle.Target[seat] = -1L;
                battle.Moving[seat] = false;
            }

            SpawnWave(state, tuning, arena);
        }

        /// <summary>자리의 줄 (y). 튜닝 표가 짧으면 0</summary>
        public static double LaneOf(IdleTuning tuning, int seat)
        {
            double[] lanes = tuning.LaneY;
            return lanes != null && seat < lanes.Length ? lanes[seat] : 0d;
        }

        /// <summary>틱 하나. 반환은 처치 수</summary>
        private static long Tick(IdleState state, IdleTuning tuning, in IdleArena arena, double delta)
        {
            IdleBattle battle = arena.Battle;

            // 던전 판은 시간이 정함. 끝나면 이 전장은 더 안 돎 (본판은 제 전장에서 계속)
            if (arena.Dungeon && IdleDungeons.TickRun(state, tuning, delta))
            {
                return 0L;
            }

            if (battle.Foes.Count == 0)
            {
                SpawnWave(state, tuning, arena);
            }

            MoveDolls(state, tuning, arena, delta);

            int front = FrontSeat(state, arena);
            if (front >= 0)
            {
                MoveFoes(tuning, arena, delta, front);
            }

            StrikeByDolls(state, tuning, arena, delta);
            StrikeByFoes(state, tuning, arena, delta);

            long kills = ClearDead(state, tuning, arena);

            if (arena.Dungeon && state.Dungeon.Finished)
            {
                // 던전 판이 처치로 끝남 (보스, 마지막 웨이브). 전장은 멈추고 결과 대기
                return kills;
            }

            if (FrontSeat(state, arena) < 0)
            {
                if (arena.Dungeon)
                {
                    // 던전 전멸. 얻은 것만 들고 나감 (사용자 2026-09-08). 본판은 무관
                    IdleDungeons.EndRun(state, tuning, false);
                    return kills;
                }

                // 전멸. 지금 코어의 실패 규칙 그대로 물러나 반복
                IdleSquad.FallBack(state, tuning);
                Reset(state, tuning, arena);
                return kills;
            }

            Revive(state, tuning, arena, delta);

            if (arena.Dungeon == false)
            {
                Measure(state, tuning, arena, delta, kills);

                if (battle.StageSeen != state.Stage)
                {
                    Reset(state, tuning, arena);
                }
            }

            return kills;
        }

        /// <summary>본판에서 서 있는 자리 중 x 최대. 적이 노리는 자리. 없으면 -1</summary>
        public static int FrontSeat(IdleState state)
        {
            return FrontSeat(state, state.MainArena);
        }

        public static int FrontSeat(IdleState state, in IdleArena arena)
        {
            IdleBattle battle = arena.Battle;
            int front = -1;

            for (int seat = 0; seat < IdleSquad.SEAT_COUNT; seat++)
            {
                if (IdleSquad.Standing(state, arena, seat) == false)
                {
                    continue;
                }

                if (front < 0 || battle.X[seat] > battle.X[front])
                {
                    front = seat;
                }
            }

            return front;
        }
    }
}
