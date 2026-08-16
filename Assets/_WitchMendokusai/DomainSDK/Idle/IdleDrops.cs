using System;

namespace WitchMendokusai.DomainSDK.Idle
{
    /// <summary>
    /// 떨구기 — <b>깊이가 곧 등급의 관문</b> (TASK-WM-406).
    ///
    /// ★ 레퍼런스는 울티마 스쿼드다. 거기서 스테이지마다 <b>나올 수 있는 등급의 상한</b>이 정해져 있고,
    ///   그 하나가 「같은 자리 반복은 성장이 아니다」를 만든다. 아무리 오래 서 있어도
    ///   상한 위의 등급은 안 나오므로, 더 좋은 것을 원하면 <b>내려가는 수밖에 없다.</b>
    ///
    /// ★ <b>주사위를 안 굴린다.</b> 처치마다 굴리면 8시간 자리를 비운 판에서 처치 수만큼 굴려야 해
    ///   한 스텝이 O(처치 수)가 되고, 「60초 한 번 == 0.1초 600번」도 깨진다(오프라인이 그 위에 서 있다).
    ///   대신 <b>잔여분을 들고 가는 누적</b>으로 센다 — 피해를 다루는 방식과 같은 꼴이다.
    ///   결과가 매끄러워지는 대신 셈이 정확하고 시험이 통계가 아니게 된다.
    ///   <b>도박의 자리는 잠재옵션</b>이다(울티마 스쿼드도 거기서 굴린다) — 무작위는 거기서 넣는다.
    /// </summary>
    public static class IdleDrops
    {
        /// <summary>이 단계에서 나올 수 있는 가장 높은 등급 (1부터).</summary>
        public static int MaxTierAt(int stage, IdleTuning tuning)
        {
            if (tuning.StagesPerTier <= 0 || tuning.MaxTier <= 1)
            {
                return tuning.MaxTier < 1 ? 1 : tuning.MaxTier;
            }

            int opened = 1 + (stage - 1) / tuning.StagesPerTier;

            if (opened < 1)
            {
                return 1;
            }

            return opened > tuning.MaxTier ? tuning.MaxTier : opened;
        }

        /// <summary>
        /// 지금 열린 등급들 사이의 몫 — 위로 갈수록 <see cref="IdleTuning.TierRarity"/> 배씩 귀해진다.
        /// 합이 1 이라, 상한이 열려도 <b>전체 개수는 안 늘고 나눠 갖는다</b> — 깊이의 값어치는
        /// 「더 많이」가 아니라 「더 좋은 것이 섞인다」여야 한다.
        /// </summary>
        public static double ShareOf(int tier, int maxTier, IdleTuning tuning)
        {
            if (tier < 1 || tier > maxTier)
            {
                return 0d;
            }

            double rarity = tuning.TierRarity;
            if (rarity <= 0d)
            {
                return tier == 1 ? 1d : 0d;
            }

            double total = 0d;
            for (int one = 1; one <= maxTier; one++)
            {
                total += Math.Pow(rarity, one - 1);
            }

            if (total <= 0d)
            {
                return 0d;
            }

            return Math.Pow(rarity, tier - 1) / total;
        }

        /// <summary>
        /// 처치 <paramref name="kills"/> 만큼의 몫을 쌓는다. 정수가 된 만큼만 실제로 떨어지고,
        /// 나머지는 다음으로 이어진다 — 그래서 <b>스텝을 어떻게 쪼개도 총합이 같다.</b>
        /// </summary>
        public static void Accrue(IdleState state, IdleTuning tuning, long kills, int stage)
        {
            if (kills <= 0L || tuning.DropsPerKill <= 0d)
            {
                return;
            }

            state.EnsureTierRoom(tuning.MaxTier);

            int maxTier = MaxTierAt(stage, tuning);
            double expected = kills * tuning.DropsPerKill;

            for (int tier = 1; tier <= maxTier; tier++)
            {
                double share = ShareOf(tier, maxTier, tuning);
                if (share <= 0d)
                {
                    continue;
                }

                int slot = tier - 1;
                double carried = state.DropProgressByTier[slot] + expected * share;

                long whole = (long)carried;
                if (whole > 0L)
                {
                    state.DroppedByTier[slot] += whole;
                    carried -= whole;
                }

                state.DropProgressByTier[slot] = carried;
            }
        }
    }
}
