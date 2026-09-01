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
    /// </summary>
    public static class IdleBattleSim
    {
        private const double EPSILON = 1e-9d;

        /// <summary>한 틱에 한 자리가 넣을 수 있는 타격 상한. 공격 속도가 아주 높아도 멎지 않게</summary>
        private const int MAX_HITS_PER_TICK = 256;

        /// <summary>시간 진행. 이월을 더해 틱 수만큼. 타격 목록은 부를 때마다 새로</summary>
        public static void Advance(IdleState state, IdleTuning tuning, double seconds)
        {
            IdleBattle battle = state.Battle;
            battle.Hits.Clear();

            if (seconds <= 0d || double.IsNaN(seconds))
            {
                return;
            }

            IdleHeroes.EnsureStarter(state);
            state.EnsureSeatRoom(tuning);

            if (battle.Ready == false || battle.StageSeen != state.Stage)
            {
                Reset(state, tuning);
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

            for (long at = 0; at < ticks; at++)
            {
                Tick(state, tuning, tick);
            }
        }

        /// <summary>전장 새로 세우기. 인형은 뒤에 줄지어, 첫 웨이브는 앞에</summary>
        public static void Reset(IdleState state, IdleTuning tuning)
        {
            IdleBattle battle = state.Battle;
            battle.Foes.Clear();
            battle.Wave = 0;
            battle.Carry = 0d;
            battle.Ready = true;
            battle.StageSeen = state.Stage;

            for (int seat = 0; seat < IdleSquad.SEAT_COUNT; seat++)
            {
                battle.X[seat] = -seat * tuning.SeatBackStep;
                battle.Y[seat] = LaneOf(tuning, seat);
                battle.Cooldown[seat] = 0d;
                battle.Target[seat] = -1L;
                battle.Moving[seat] = false;
            }

            SpawnWave(state, tuning);
        }

        /// <summary>자리의 줄 (y). 튜닝 표가 짧으면 0</summary>
        public static double LaneOf(IdleTuning tuning, int seat)
        {
            double[] lanes = tuning.LaneY;
            return lanes != null && seat < lanes.Length ? lanes[seat] : 0d;
        }

        private static void Tick(IdleState state, IdleTuning tuning, double delta)
        {
            IdleBattle battle = state.Battle;

            if (battle.Foes.Count == 0)
            {
                SpawnWave(state, tuning);
            }

            MoveDolls(state, tuning, delta);

            int front = FrontSeat(state);
            if (front >= 0)
            {
                MoveFoes(state, tuning, delta, front);
            }

            StrikeByDolls(state, tuning, delta);
            StrikeByFoes(state, tuning, delta);

            long kills = ClearDead(state, tuning);

            if (FrontSeat(state) < 0)
            {
                // 전멸. 지금 코어의 실패 규칙 그대로 물러나 반복
                IdleSquad.FallBack(state, tuning);
                Reset(state, tuning);
                return;
            }

            Revive(state, tuning, delta);
            Measure(state, tuning, delta, kills);

            if (battle.StageSeen != state.Stage)
            {
                Reset(state, tuning);
            }
        }

        /// <summary>서 있는 자리 중 x 최대. 적이 노리는 자리. 없으면 -1</summary>
        public static int FrontSeat(IdleState state)
        {
            IdleBattle battle = state.Battle;
            int front = -1;

            for (int seat = 0; seat < IdleSquad.SEAT_COUNT; seat++)
            {
                if (IdleSquad.Standing(state, seat) == false)
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

        private static IdleFoe NearestFoe(IdleBattle battle, double x, double y)
        {
            IdleFoe nearest = null;
            double best = double.PositiveInfinity;

            for (int at = 0; at < battle.Foes.Count; at++)
            {
                IdleFoe foe = battle.Foes[at];
                if (foe.Health <= 0d)
                {
                    continue;
                }

                double distance = Distance(x, y, foe.X, foe.Y);
                if (distance < best)
                {
                    best = distance;
                    nearest = foe;
                }
            }

            return nearest;
        }

        private static double Distance(double x, double y, double otherX, double otherY)
        {
            double dx = otherX - x;
            double dy = otherY - y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private static void MoveDolls(IdleState state, IdleTuning tuning, double delta)
        {
            IdleBattle battle = state.Battle;

            for (int seat = 0; seat < IdleSquad.SEAT_COUNT; seat++)
            {
                battle.Moving[seat] = false;

                if (IdleSquad.Standing(state, seat) == false)
                {
                    battle.Target[seat] = -1L;
                    continue;
                }

                IdleFoe target = NearestFoe(battle, battle.X[seat], battle.Y[seat]);
                if (target == null)
                {
                    battle.Target[seat] = -1L;
                    continue;
                }

                battle.Target[seat] = target.Index;
                double range = IdleHeroes.RangeOf(state, tuning, seat);
                double distance = Distance(battle.X[seat], battle.Y[seat], target.X, target.Y);

                if (distance <= range + EPSILON)
                {
                    continue;
                }

                // 사거리 밖. x 로만 걷기. 적 통과 금지
                double step = tuning.DollMoveSpeed * delta;
                double wanted = battle.X[seat] + step;
                double stop = target.X - tuning.BodyGap;
                if (wanted > stop)
                {
                    wanted = stop;
                }

                if (wanted > battle.X[seat])
                {
                    battle.X[seat] = wanted;
                    battle.Moving[seat] = true;
                }
            }
        }

        private static void MoveFoes(IdleState state, IdleTuning tuning, double delta, int front)
        {
            IdleBattle battle = state.Battle;
            double frontX = battle.X[front];
            double frontY = battle.Y[front];

            for (int at = 0; at < battle.Foes.Count; at++)
            {
                IdleFoe foe = battle.Foes[at];
                double distance = Distance(foe.X, foe.Y, frontX, frontY);

                if (distance <= foe.Range + EPSILON)
                {
                    continue;
                }

                double wanted = foe.X - foe.Speed * delta;
                double stop = frontX + tuning.BodyGap;
                if (wanted < stop)
                {
                    wanted = stop;
                }

                if (wanted < foe.X)
                {
                    foe.X = wanted;
                }
            }
        }

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

                double range = IdleHeroes.RangeOf(state, tuning, seat);
                if (Distance(battle.X[seat], battle.Y[seat], target.X, target.Y) > range + EPSILON)
                {
                    continue;
                }

                int heroId = state.Party[seat];
                double damage = IdleModel.DamageOfHero(state, tuning, heroId);
                double perSecond = IdleModel.AttackSpeedOfHero(state, tuning, heroId);
                double interval = perSecond > 0d ? 1d / perSecond : double.PositiveInfinity;

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

        /// <summary>죽은 적 정리와 처치 반영. 반환은 처치 수</summary>
        private static long ClearDead(IdleState state, IdleTuning tuning)
        {
            IdleBattle battle = state.Battle;
            long kills = 0L;

            for (int at = battle.Foes.Count - 1; at >= 0; at--)
            {
                if (battle.Foes[at].Health > 0d)
                {
                    continue;
                }

                battle.Foes.RemoveAt(at);
                kills++;
                OnKill(state, tuning);

                if (battle.StageSeen != state.Stage)
                {
                    // 구역 클리어. 남은 적은 무의미. Tick 끝에서 Reset
                    battle.Foes.Clear();
                    break;
                }
            }

            return kills;
        }

        /// <summary>처치 하나. 구역 판정은 지금 코어 <c>IdleModel.Resolve</c> 와 동일</summary>
        private static void OnKill(IdleState state, IdleTuning tuning)
        {
            state.Kills += 1L;
            IdleDrops.Accrue(state, tuning, 1L, state.Stage);
            IdleModel.RollStoneDrop(state, tuning, 1L);
            IdleSquad.HealOnKills(state, tuning, 1L);

            bool canClear = tuning.KillsPerStage > 0 && state.HoldingStage == false && state.Repeating == false;

            if (canClear && state.KillsInStage + 1 >= tuning.KillsPerStage)
            {
                // 구역의 마지막 하나가 보스 (combat.md 4.3). 잡으면 환생 조각 드롭
                IdleModel.DropPrestigeShard(state, tuning);

                state.ClearedStage = state.Stage;
                IdleSquad.HealAll(state, tuning);
                state.Stage += 1;
                state.KillsInStage = 0;
                state.HitsOnTarget = 0L;

                IdleModel.RewardNewDepth(state, tuning);

                return;
            }

            state.KillsInStage += 1;
            if (tuning.KillsPerStage > 0 && state.KillsInStage > tuning.KillsPerStage)
            {
                state.KillsInStage = tuning.KillsPerStage;
            }
        }

        private static void Revive(IdleState state, IdleTuning tuning, double delta)
        {
            IdleBattle battle = state.Battle;

            double rear = double.PositiveInfinity;
            for (int seat = 0; seat < IdleSquad.SEAT_COUNT; seat++)
            {
                if (IdleSquad.Standing(state, seat) && battle.X[seat] < rear)
                {
                    rear = battle.X[seat];
                }
            }

            for (int seat = 0; seat < IdleSquad.SEAT_COUNT; seat++)
            {
                if (IdleSquad.SeatTaken(state, seat) == false || state.SeatHealth[seat] > 0d)
                {
                    continue;
                }

                state.SeatReviveSeconds[seat] += delta;

                if (state.SeatReviveSeconds[seat] + EPSILON >= tuning.ReviveSeconds)
                {
                    state.SeatHealth[seat] = IdleSquad.MaxHealthOf(state, tuning, seat);
                    state.SeatReviveSeconds[seat] = 0d;
                    battle.Cooldown[seat] = 0d;
                    // 자기 줄 맨 뒤로 복귀
                    battle.X[seat] = double.IsInfinity(rear) ? 0d : rear - tuning.SeatBackStep;
                }
            }
        }

        /// <summary>
        /// 구역별 실측 (combat.md 6). 같은 구역에서 <see cref="IdleTuning.MeasureSeconds"/> 를 채우면
        /// 초당 처치 확정. 실측이 있는 가장 깊은 구역 것만 보존
        /// </summary>
        private static void Measure(IdleState state, IdleTuning tuning, double delta, long kills)
        {
            IdleBattle battle = state.Battle;

            if (battle.MeasureStage != state.Stage)
            {
                battle.MeasureStage = state.Stage;
                battle.MeasureSeconds = 0d;
                battle.MeasureKills = 0L;
            }

            battle.MeasureSeconds += delta;
            battle.MeasureKills += kills;

            if (battle.MeasureSeconds + EPSILON < tuning.MeasureSeconds)
            {
                return;
            }

            if (state.Stage >= state.MeasuredStage)
            {
                state.MeasuredStage = state.Stage;
                state.MeasuredKillsPerSecond = battle.MeasureKills / battle.MeasureSeconds;
            }

            battle.MeasureSeconds = 0d;
            battle.MeasureKills = 0L;
        }

        /// <summary>
        /// 다음 웨이브를 부대 앞에. 구역의 마지막 하나는 보스 혼자.
        /// 좌표는 부대 맨 뒤를 0 으로 재설정 (계속 커지지 않게)
        /// </summary>
        private static void SpawnWave(IdleState state, IdleTuning tuning)
        {
            IdleBattle battle = state.Battle;

            double rear = double.PositiveInfinity;
            double front = double.NegativeInfinity;
            for (int seat = 0; seat < IdleSquad.SEAT_COUNT; seat++)
            {
                if (IdleSquad.SeatTaken(state, seat) == false)
                {
                    continue;
                }

                if (battle.X[seat] < rear)
                {
                    rear = battle.X[seat];
                }

                if (battle.X[seat] > front)
                {
                    front = battle.X[seat];
                }
            }

            if (double.IsInfinity(rear))
            {
                rear = 0d;
                front = 0d;
            }

            for (int seat = 0; seat < IdleSquad.SEAT_COUNT; seat++)
            {
                battle.X[seat] -= rear;
            }

            front -= rear;

            int mobs = tuning.KillsPerStage - 1;
            int left = mobs - state.KillsInStage;
            bool bossWave = left <= 0;
            int count = bossWave ? 1 : Math.Min(Math.Max(1, tuning.WaveSize), left);

            IdleRandom dice = new IdleRandom(state.Stage * 7919L + battle.Wave * 104729L + 1L);
            double health = IdleModel.TargetHealthAt(state.Stage, tuning);
            double damagePerSecond = IdleSquad.EnemyDamagePerSecond(state, tuning);

            for (int at = 0; at < count; at++)
            {
                bool ranged = bossWave == false
                    && state.Stage >= tuning.RangedFoeFromStage
                    && dice.NextDouble() < tuning.RangedFoeChance;

                IdleFoe foe = new IdleFoe();
                foe.Index = battle.Spawned++;
                foe.Boss = bossWave;
                foe.Kind = ranged ? IdleFoeKind.Ranged : IdleFoeKind.Melee;
                foe.X = front + tuning.WaveSpawnDistance + at * tuning.WaveGapX;
                foe.Y = at == 0 ? 0d : (at % 2 == 1 ? -tuning.WaveGapY : tuning.WaveGapY);
                foe.MaxHealth = health * (bossWave ? tuning.BossHealthMultiplier : 1d);
                foe.Health = foe.MaxHealth;
                foe.Range = ranged ? tuning.FoeRangedRange : tuning.FoeMeleeRange;
                foe.Speed = bossWave ? tuning.BossMoveSpeed : tuning.FoeMoveSpeed;
                foe.AttackSeconds = bossWave ? tuning.BossAttackSeconds : tuning.FoeAttackSeconds;
                foe.Damage = damagePerSecond * foe.AttackSeconds;
                foe.Cooldown = 0d;
                battle.Foes.Add(foe);
            }

            battle.Wave += 1;
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
