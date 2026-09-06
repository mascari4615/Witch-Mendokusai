using System;
using WitchMendokusai.DomainSDK.Upgrade;

namespace WitchMendokusai.DomainSDK.Idle
{
    /// <summary>
    /// 기지 — <b>시간이 자원을 낸다</b> (TASK-WM-406).
    ///
    /// ★ 왜 층을 나눴나 (사용자: "클리커랑 모험 두 가지가 되면 안 되나"):
    ///   그전에는 잡기 하나가 자원도 장비도 다 냈다. 그러니 나머지가 <b>얹힌 것</b>처럼 놀았다
    ///   — 사용자 표현으로 「안 녹아든다」.
    ///   이제 갈라 둔다: <b>자원은 기지가, 장비는 모험이</b> 낸다.
    ///   기지만 키우면 자원은 넘치는데 용병이 약해 못 내려가고,
    ///   모험만 밀면 장비는 있는데 감정·합치기·강화할 자원이 없다. <b>서로를 부른다.</b>
    ///
    /// ★ 생산자 클리커 계열의 뼈대 그대로 — 같은 생산자를 살수록 값이 <b>1.15배씩</b> 오른다(그 게임 실제 값).
    ///   그 하나가 「지금 싼 것을 여럿 살까, 비싼 것을 하나 살까」를 매번 묻는다.
    ///
    /// ★ 이름을 안 박는다 — 컨셉이 바뀌어도 뼈대는 산다(코어 규칙).
    ///   생산자는 <b>번호</b>로만 구분한다. 부르는 이름은 표현이 붙인다.
    /// </summary>
    public static class IdleBase
    {
        /// <summary>이 생산자를 <paramref name="owned"/> 개 가진 상태에서 한 개 더 살 때의 값.</summary>
        public static double CostOf(int kind, long owned, IdleTuning tuning)
        {
            if (kind < 0 || kind >= tuning.ProducerCount)
            {
                return double.PositiveInfinity;
            }

            double first = tuning.ProducerCostByKind.At(kind);
            if (owned <= 0L)
            {
                return first;
            }

            return first * Math.Pow(tuning.ProducerCostRatio, owned);
        }

        /// <summary>이 생산자 하나가 내는 초당 자원.</summary>
        public static double OutputOf(int kind, IdleTuning tuning)
        {
            if (kind < 0 || kind >= tuning.ProducerCount)
            {
                return 0d;
            }

            return tuning.ProducerOutputByKind.At(kind);
        }

        /// <summary>
        /// 배수를 <b>안 곱한</b> 기지 산출 — 「하나 더 사면 몇 배가 되나」를 재는 바닥.
        ///
        /// ★ 배수(장비·영웅·도감·폭주)는 사도 안 사도 똑같이 곱해지므로 <b>비율에서 지워진다</b>.
        ///   그래서 그 비율은 이 바닥만으로 잰다 — 전에는 그걸 재려고 <b>생산자를 넣었다 뺐다</b> 했다.
        ///   조회하는 자리가 판을 건드리면, 그 사이에 무슨 일이 나는 순간 공짜 생산자가 남는다.
        /// </summary>
        public static double RawOutputPerSecond(IdleState state, IdleTuning tuning)
        {
            double total = 0d;

            for (int kind = 0; kind < state.Owned.Length && kind < tuning.ProducerCount; kind++)
            {
                total += state.Owned[kind] * OutputOf(kind, tuning);
            }

            return total;
        }

        /// <summary>기지 전체가 내는 초당 자원.</summary>
        public static double OutputPerSecond(IdleState state, IdleTuning tuning)
        {
            double total = RawOutputPerSecond(state, tuning);

            // ★ 폭주는 <b>판 전체</b>다 — 전에는 때리는 속도에만 걸려 있어서, 기지가 수입의 거의
            //   전부가 되는 중반 이후에는 「폭주!」가 떠도 실제로는 거의 아무 일도 안 일어났다.
            //   변동성이 봉우리를 만들라고 넣은 것인데 봉우리가 평지였던 셈이다.
            //   (자리를 비운 동안에는 폭주가 안 걸린다 — CatchUp 이 지우므로 방치 판정은 그대로 결정적이다.)
            return total * IdleGear.BaseMultiplier(state, tuning)
                * IdleHeroes.AxisMultiplierOf(state, tuning, IdleHeroAxis.Base)
                // 도감은 <b>여기서 한 번</b> — 기지 쪽의 뿌리가 여기다 (싸움 쪽은 DamageOf).
                * IdleHeroes.DiscoveryMultiplierOf(state, tuning)
                * IdleSurge.Multiplier(state, tuning)
                // 긴급 보급 카드 — 스텝이 보급 경계에서 끊기므로 (IdleModel.Step) 이 배수는
                // 스텝 안에서 상수다. 그래야 60초 한 번 == 0.1초 600번이 유지된다.
                * IdleCards.SupplyMultiplier(state, tuning);
        }

        /// <summary>
        /// 아직 안 보여줄 생산자인가 — <b>바로 다음 것까지는 늘 보인다</b>.
        ///
        /// ★ 고쳐 쓴 자리 (사용자 지적 2026-08-16): 전에는 <b>값의 절반을 모아야</b> 다음 줄이
        ///   나타났다. 그래서 돈이 모자라는 동안 다음 단계가 <b>사라진 것처럼</b> 보였고,
        ///   사람 눈에는 그게 버그다 — 목표가 안 보이면 모을 이유도 안 보인다.
        ///   생산자 클리커 계열도 다음 건물을 <b>회색으로 값과 함께</b> 띄워 둔다. 감추는 건
        ///   <b>그 다음</b>부터다 — 여덟 줄을 한 번에 펴 놓으면 뭘 할지가 안 보이니까.
        /// </summary>
        public static bool IsHidden(int kind, IdleState state)
        {
            if (kind <= 0)
            {
                return false;
            }

            if (state.Owned.Length > kind && state.Owned[kind] > 0L)
            {
                return false;
            }

            // 앞 번호를 하나라도 가졌으면 보인다 — 자원은 안 따진다(못 사도 값은 보여야 한다).
            return state.Owned.Length <= kind - 1 || state.Owned[kind - 1] <= 0L;
        }

        /// <summary>한 개 산다. 자원이 모자라면 아무 일도 안 일어난다.</summary>
        public static bool TryBuy(IdleState state, IdleTuning tuning, int kind)
        {
            if (kind < 0 || kind >= tuning.ProducerCount)
            {
                return false;
            }

            state.EnsureProducerRoom(tuning.ProducerCount);

            double cost = CostOf(kind, state.Owned[kind], tuning);
            if (double.IsInfinity(cost) || state.Resource < cost)
            {
                return false;
            }

            state.Resource -= cost;
            state.Owned[kind] += 1L;
            return true;
        }

        /// <summary>
        /// 지금 <b>살 수 있는 것 중 가장 싼</b> 생산자 — 없으면 -1.
        ///
        /// ★ 곡선 시뮬레이션의 개별 구매 대상 선택.
        /// </summary>
        public static int CheapestAffordable(IdleState state, IdleTuning tuning)
        {
            int cheapest = -1;
            double best = double.PositiveInfinity;

            for (int kind = 0; kind < tuning.ProducerCount && kind < state.Owned.Length; kind++)
            {
                if (IsHidden(kind, state))
                {
                    continue;
                }

                double cost = CostOf(kind, state.Owned[kind], tuning);
                if (cost <= state.Resource && cost < best)
                {
                    best = cost;
                    cheapest = kind;
                }
            }

            return cheapest;
        }

        /// <summary>기지에 쏟은 것이 다 합쳐 얼마나 되나 — 「얼마나 지었나」를 한 수로.</summary>
        public static long TotalOwned(IdleState state)
        {
            long total = 0L;
            for (int kind = 0; kind < state.Owned.Length; kind++)
            {
                total += state.Owned[kind];
            }

            return total;
        }
    }
}
