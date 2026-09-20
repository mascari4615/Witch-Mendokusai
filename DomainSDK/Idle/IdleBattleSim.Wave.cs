using System;

namespace WitchMendokusai.DomainSDK.Idle
{
    // IdleBattleSim.cs 의 Wave 조각. 같은 클래스의 partial. 상태(필드)는 원본 파일을 본다. 처치, 부활, 실측, 웨이브 소환.
    public static partial class IdleBattleSim
    {
        /// <summary>죽은 적 정리와 처치 반영. 반환은 처치 수</summary>
        private static long ClearDead(IdleState state, IdleTuning tuning, in IdleArena arena)
        {
            IdleBattle battle = arena.Battle;
            long kills = 0L;

            for (int at = battle.Foes.Count - 1; at >= 0; at--)
            {
                if (battle.Foes[at].Health > 0d)
                {
                    continue;
                }

                bool boss = battle.Foes[at].Boss;
                battle.Foes.RemoveAt(at);
                kills++;

                if (arena.Dungeon)
                {
                    // 던전 안 처치. 구역 셈은 없고 칸 규칙만 (changes/idle-dungeon-run v2)
                    if (OnDungeonKill(state, tuning, arena, boss))
                    {
                        battle.Foes.Clear();
                        break;
                    }
                    continue;
                }

                OnKill(state, tuning);

                if (battle.StageSeen != state.Stage)
                {
                    // 구역 클리어. 남은 적은 무의미. Tick 끝에서 Reset
                    battle.Foes.Clear();
                    break;
                }
            }

            if (kills > 0L && arena.Dungeon && state.Dungeon.Active && state.Dungeon.Finished == false && battle.Foes.Count == 0)
            {
                // 던전 웨이브 하나를 다 잡음. 장비 던전은 여기서 장비, 마지막이면 끝
                IdleDungeons.OnWaveCleared(state, tuning);
            }

            return kills;
        }

        /// <summary>던전 안 처치 하나. 드롭은 칸 구역 기준, 회복은 던전 체력에. 반환은 판이 끝났나</summary>
        private static bool OnDungeonKill(IdleState state, IdleTuning tuning, in IdleArena arena, bool boss)
        {
            IdleDungeonStageSpec cell = IdleDungeons.CurrentCell(state, tuning);
            state.Kills += 1L;
            IdleDrops.Accrue(state, tuning, 1L, cell != null ? cell.Level : state.Stage);
            IdleSquad.HealOnKills(state, tuning, arena, 1L);
            return IdleDungeons.OnKill(state, tuning, boss);
        }

        /// <summary>본판 처치 하나. 구역 판정은 지금 코어 <c>IdleModel.Resolve</c> 와 동일</summary>
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

        private static void Revive(IdleState state, IdleTuning tuning, in IdleArena arena, double delta)
        {
            IdleBattle battle = arena.Battle;

            double rear = double.PositiveInfinity;
            for (int seat = 0; seat < IdleSquad.SEAT_COUNT; seat++)
            {
                if (IdleSquad.Standing(state, arena, seat) && battle.X[seat] < rear)
                {
                    rear = battle.X[seat];
                }
            }

            for (int seat = 0; seat < IdleSquad.SEAT_COUNT; seat++)
            {
                if (IdleSquad.SeatTaken(state, seat) == false || arena.SeatHealth[seat] > 0d)
                {
                    continue;
                }

                arena.SeatReviveSeconds[seat] += delta;

                if (arena.SeatReviveSeconds[seat] + EPSILON >= tuning.ReviveSeconds)
                {
                    arena.SeatHealth[seat] = IdleSquad.MaxHealthOf(state, tuning, seat);
                    arena.SeatReviveSeconds[seat] = 0d;
                    battle.Cooldown[seat] = 0d;
                    // 자기 줄 맨 뒤로 복귀
                    battle.X[seat] = double.IsInfinity(rear) ? 0d : rear - tuning.SeatBackStep;
                }
            }
        }

        /// <summary>
        /// 구역별 실측 (combat.md 6). 같은 구역에서 <see cref="IdleTuning.MeasureSeconds"/> 를 채우면
        /// 초당 처치 확정. 실측이 있는 가장 깊은 구역 것만 보존. 본판만
        /// </summary>
        private static void Measure(IdleState state, IdleTuning tuning, in IdleArena arena, double delta, long kills)
        {
            IdleBattle battle = arena.Battle;

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
        /// 다음 웨이브를 부대 앞에. 본판은 구역의 마지막 하나가 보스 혼자, 던전은 칸 규칙 (IdleDungeons.WaveOf).
        /// 좌표는 부대 맨 뒤를 0 으로 재설정 (계속 커지지 않게)
        /// </summary>
        private static void SpawnWave(IdleState state, IdleTuning tuning, in IdleArena arena)
        {
            IdleBattle battle = arena.Battle;

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

            // 웨이브 사이 전진 유지. 원점 이동 간격을 넘을 때만 자리와 적을 함께 되감고
            // 민 거리는 OriginX 에 쌓여 무대가 같은 프레임에 전부 같이 옮김
            if (rear >= tuning.BattleRebaseDistance)
            {
                for (int seat = 0; seat < IdleSquad.SEAT_COUNT; seat++)
                {
                    battle.X[seat] -= rear;
                }

                for (int at = 0; at < battle.Foes.Count; at++)
                {
                    battle.Foes[at].X -= rear;
                }

                battle.OriginX += rear;
                front -= rear;
            }

            bool bossWave;
            int count;
            int level;
            double health;
            double damagePerSecond;

            if (arena.Dungeon)
            {
                IdleDungeons.WaveOf(state, tuning, out bossWave, out count, out level, out health, out damagePerSecond);
            }
            else
            {
                int mobs = tuning.KillsPerStage - 1;
                int left = mobs - state.KillsInStage;
                bossWave = left <= 0;
                count = bossWave ? 1 : Math.Min(Math.Max(1, tuning.WaveSize), left);
                level = state.Stage;
                health = IdleModel.TargetHealthAt(state.Stage, tuning) * (bossWave ? tuning.BossHealthMultiplier : 1d);
                damagePerSecond = IdleSquad.EnemyDamagePerSecond(state, tuning);
            }

            IdleRandom dice = new IdleRandom(level * 7919L + battle.Wave * 104729L + 1L);

            for (int at = 0; at < count; at++)
            {
                bool ranged = bossWave == false
                    && level >= tuning.RangedFoeFromStage
                    && dice.NextDouble() < tuning.RangedFoeChance;

                IdleFoe foe = new IdleFoe();
                foe.Index = battle.Spawned++;
                foe.Boss = bossWave;
                foe.Kind = ranged ? IdleFoeKind.Ranged : IdleFoeKind.Melee;
                foe.X = front + tuning.WaveSpawnDistance + at * tuning.WaveGapX;
                foe.Y = at == 0 ? 0d : (at % 2 == 1 ? -tuning.WaveGapY : tuning.WaveGapY);
                foe.MaxHealth = health;
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
    }
}
