using System;

namespace WitchMendokusai.DomainSDK.Idle
{
    // IdleBattleSim.cs 의 Strike 조각. 같은 클래스의 partial. 상태(필드)는 원본 파일을 본다. 타격.
    public static partial class IdleBattleSim
    {
        private static void StrikeByDolls(IdleState state, IdleTuning tuning, double delta)
        {
            IdleBattle battle = state.Battle;

            for (int seat = 0; seat < IdleSquad.SEAT_COUNT; seat++)
            {
                if (IdleSquad.Standing(state, seat) == false)
                {
                    continue;
                }

                IdleFoe target = battle.Target[seat] >= 0L ? battle.FoeOf(battle.Target[seat]) : null;
                if (target == null || target.Health <= 0d)
                {
                    continue;
                }

                double range = battle.StatRange[seat];
                if (Distance(battle.X[seat], battle.Y[seat], target.X, target.Y) > range + EPSILON)
                {
                    continue;
                }

                double damage = battle.StatDamage[seat];
                double interval = battle.StatInterval[seat];

                battle.Cooldown[seat] -= delta;

                for (int guard = 0; guard < MAX_HITS_PER_TICK && battle.Cooldown[seat] <= EPSILON; guard++)
                {
                    if (double.IsInfinity(interval))
                    {
                        battle.Cooldown[seat] = 0d;
                        break;
                    }

                    battle.Cooldown[seat] += interval;
                    target.Health -= damage;
                    state.HitsOnTarget += 1L;
                    battle.Hits.Add(new IdleHit(seat, target.Index, damage, false));

                    if (target.Health <= 0d)
                    {
                        // 남은 쿨은 다음 목표로 이월. 이 틱에는 타격 중지 (목표는 다음 틱에 다시 선택)
                        break;
                    }
                }
            }
        }

        private static void StrikeByFoes(IdleState state, IdleTuning tuning, double delta)
        {
            IdleBattle battle = state.Battle;

            for (int at = 0; at < battle.Foes.Count; at++)
            {
                IdleFoe foe = battle.Foes[at];
                if (foe.Health <= 0d)
                {
                    continue;
                }

                foe.Cooldown -= delta;

                for (int guard = 0; guard < MAX_HITS_PER_TICK && foe.Cooldown <= EPSILON; guard++)
                {
                    int front = FrontSeat(state);
                    if (front < 0)
                    {
                        foe.Cooldown = 0d;
                        return;
                    }

                    if (Distance(foe.X, foe.Y, battle.X[front], battle.Y[front]) > foe.Range + EPSILON)
                    {
                        // 아직 사거리 밖. 쿨은 0 에서 대기
                        foe.Cooldown = 0d;
                        break;
                    }

                    foe.Cooldown += foe.AttackSeconds;
                    double received = IdleSquad.DamageTakenBySeat(state, tuning, front, foe.Damage);
                    state.SeatHealth[front] -= received;
                    battle.Hits.Add(new IdleHit(front, foe.Index, received, true));

                    if (state.SeatHealth[front] <= EPSILON)
                    {
                        state.SeatHealth[front] = 0d;
                        state.SeatReviveSeconds[front] = 0d;
                    }
                }
            }
        }

        /// <summary>
        /// 사거리 무시 즉시 <paramref name="seconds"/> 초치 타격 (일제 사격, 손 때리기).
        /// 시간 진행 없음. 목표는 가장 가까운 적
        /// </summary>
        public static void StrikeFor(IdleState state, IdleTuning tuning, double seconds)
        {
            IdleBattle battle = state.Battle;
            if (seconds <= 0d || battle.Ready == false)
            {
                return;
            }

            double damage = IdleModel.DamageOf(state, tuning);
            long hits = (long)(IdleModel.AttackSpeedOf(state, tuning) * seconds + EPSILON);

            for (int seat = 0; seat < IdleSquad.SEAT_COUNT && hits > 0L; seat++)
            {
                if (IdleSquad.Standing(state, seat) == false)
                {
                    continue;
                }

                for (long at = 0; at < hits; at++)
                {
                    IdleFoe target = NearestFoe(battle, battle.X[seat], battle.Y[seat]);
                    if (target == null)
                    {
                        break;
                    }

                    target.Health -= damage;
                    state.HitsOnTarget += 1L;
                    battle.Hits.Add(new IdleHit(seat, target.Index, damage, false));

                    if (target.Health <= 0d)
                    {
                        ClearDead(state, tuning);
                    }
                }

                // 한 자리만 (전원 각각이면 손 때리기 값이 자리 수 배)
                break;
            }

            if (battle.StageSeen != state.Stage)
            {
                Reset(state, tuning);
            }
        }

        public static bool StrikeForTarget(IdleState state, IdleTuning tuning, double seconds, long foeIndex)
        {
            IdleBattle battle = state.Battle;
            IdleFoe target = battle.FoeOf(foeIndex);
            if (seconds <= 0d || battle.Ready == false || target == null || target.Health <= 0d)
            {
                return false;
            }

            double damage = IdleModel.DamageOf(state, tuning);
            long hits = (long)(IdleModel.AttackSpeedOf(state, tuning) * seconds + EPSILON);
            int seat = FrontSeat(state);
            if (seat < 0 || hits <= 0L)
            {
                return false;
            }

            for (long at = 0; at < hits && target.Health > 0d; at++)
            {
                target.Health -= damage;
                state.HitsOnTarget += 1L;
                battle.Hits.Add(new IdleHit(seat, target.Index, damage, false));
            }

            if (target.Health <= 0d)
            {
                ClearDead(state, tuning);
            }

            if (battle.StageSeen != state.Stage)
            {
                Reset(state, tuning);
            }

            return true;
        }
    }
}

