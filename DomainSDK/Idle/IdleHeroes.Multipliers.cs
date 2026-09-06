using System.Collections.Generic;
using WitchMendokusai.DomainSDK.Discovery;
using WitchMendokusai.DomainSDK.Upgrade;

namespace WitchMendokusai.DomainSDK.Idle
{
    // IdleHeroes.cs 의 Multipliers 조각. 같은 클래스의 partial. 상태(필드)는 원본 파일을 본다. 보유, 도감, 편성 배수와 사거리.
    public static partial class IdleHeroes
    {
        /// <summary>
        /// 한 축의 <b>보유 배수</b> — 같은 갈래는 더하고, 축끼리는 부르는 쪽이 따로 곱한다.
        /// </summary>
        public static double OwnedMultiplierOf(IdleState state, IdleTuning tuning, IdleHeroAxis axis)
        {
            double sum = 0d;

            for (int index = 0; index < state.Heroes.Count; index++)
            {
                IdleHeroOwned owned = state.Heroes[index];
                if (KindOf(owned.Id).Axis != axis)
                {
                    continue;
                }

                sum += OwnedShareOf(owned, tuning);
            }

            return 1d + sum;
        }

        /// <summary>
        /// 도감 점수 — <b>모은 종류 + 올린 ★</b>. 「많이 모을수록」과 「깊이 키울수록」을 한 수로 묶는다.
        /// </summary>
        public static int DiscoveryScoreOf(IdleState state)
        {
            int score = 0;

            for (int index = 0; index < state.Heroes.Count; index++)
            {
                score += 1 + state.Heroes[index].Stars;
            }

            return score;
        }

        /// <summary>
        /// 도감이 주는 <b>전체 배수</b> — 축과 무관하게 판 전체에 곱한다.
        ///
        /// ★ 문턱마다 한 계단씩 오른다. 매끈하게 오르면 「채운 순간」이 안 느껴진다 —
        ///   느껴져야 채울 이유가 된다.
        /// </summary>
        public static double DiscoveryMultiplierOf(IdleState state, IdleTuning tuning)
        {
            // 계단 셈은 판정 층 공용 (DiscoveryTiers). 본편 도감도 같은 계단
            return DiscoveryTiers.MultiplierOf(DiscoveryScoreOf(state), tuning.DiscoveryStepScore, tuning.DiscoveryStepBonus);
        }

        /// <summary>
        /// 편성한 얼굴이 주는 몫. <b>편성한 영웅만</b> 이 배수를 준다(보유 효과와 별개).
        ///
        /// ★ 보유는 가지고만 있어도, 편성은 내보내야. 둘을 갈라야
        ///   <b>누구를 내보낼까</b>가 결정이 된다 — 안 그러면 전원 참전이 늘 정답이다.
        /// ★ 메인 칸과 보조 칸의 몫이 다르다. 보조는 전장에 안 서서 맞지도 않으니
        ///   같은 몫이면 늘 보조가 정답이 된다. 보조 몫은 <see cref="IdleTuning.HeroSupportShareByGrade"/>.
        /// </summary>
        public static double PartyMultiplierOf(IdleState state, IdleTuning tuning, IdleHeroAxis axis)
        {
            double sum = 0d;

            for (int slot = 0; slot < state.Party.Length; slot++)
            {
                int id = state.Party[slot];
                if (id < 0)
                {
                    continue;
                }

                int at = state.IndexOfHero(id);
                if (at < 0)
                {
                    continue;
                }

                IdleHeroOwned owned = state.Heroes[at];
                if (KindOf(id).Axis != axis)
                {
                    continue;
                }

                double share = IsMainSlot(slot)
                    ? tuning.HeroPartyShareByGrade
                    : tuning.HeroSupportShareByGrade;

                sum += share * GradeWeight(KindOf(id).Grade) * GrowthOf(owned, tuning);
            }

            return 1d + sum;
        }

        /// <summary>
        /// 한 축이 지금 받는 배수 — <b>보유 × 파티</b>.
        ///
        /// ⚠ 도감은 여기 <b>안 들어간다</b>. 도감은 「축과 무관하게 판 전체에 한 번」인데
        ///   여기 넣어 두니 축마다 한 번씩, 즉 <b>네 군데</b>에서 곱해지고 있었다.
        ///   처치 속도는 공격력 × 공격속도라 도감이 <b>제곱</b>으로 들어갔고
        ///   (「판 전체 x1.10」이 실제로는 x1.21), 떨구기는 그 위에 또 한 겹이었다.
        ///   숨은 지수는 곡선을 통째로 거짓말로 만든다 — 그래서 <b>뿌리 둘</b>에서만 곱한다
        ///   (<see cref="IdleModel.DamageOf"/> · <see cref="IdleBase.OutputPerSecond"/>).
        ///   나머지는 그 둘에서 흘러오므로 저절로 정확히 한 번 받는다.
        /// </summary>
        public static double AxisMultiplierOf(IdleState state, IdleTuning tuning, IdleHeroAxis axis)
        {
            return OwnedMultiplierOf(state, tuning, axis)
                * PartyMultiplierOf(state, tuning, axis);
        }

        /// <summary>등급이 몫에 곱해지는 무게 — 위 등급일수록 크게.</summary>
        /// <summary>이 자리 인형의 사거리 (m). 축 기준 (combat.md 3). 빈 자리는 0</summary>
        public static double RangeOf(IdleState state, IdleTuning tuning, int seat)
        {
            if (IdleSquad.SeatTaken(state, seat) == false)
            {
                return 0d;
            }

            int id = state.Party[seat];
            if (Knows(id) == false)
            {
                return 0d;
            }

            int axis = (int)KindOf(id).Axis;
            double[] table = tuning.HeroRangeByAxis;
            if (table == null || table.Length == 0)
            {
                return 0d;
            }

            return axis < table.Length ? table[axis] : table[table.Length - 1];
        }
    }
}

