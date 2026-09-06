using System;

namespace WitchMendokusai.DomainSDK.Idle
{
    // IdleBattleSim.cs 의 Move 조각. 같은 클래스의 partial. 상태(필드)는 원본 파일을 본다. 이동.
    public static partial class IdleBattleSim
    {
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
                double range = battle.StatRange[seat];
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
    }
}

