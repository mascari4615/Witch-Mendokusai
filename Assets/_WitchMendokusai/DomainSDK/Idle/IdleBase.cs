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
    /// ★ 쿠키 클리커의 뼈대 그대로 — 같은 생산자를 살수록 값이 <b>1.15배씩</b> 오른다(그 게임 실제 값).
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

        /// <summary>기지 전체가 내는 초당 자원.</summary>
        public static double OutputPerSecond(IdleState state, IdleTuning tuning)
        {
            double total = 0d;

            for (int kind = 0; kind < state.Owned.Length && kind < tuning.ProducerCount; kind++)
            {
                total += state.Owned[kind] * OutputOf(kind, tuning);
            }

            return total * IdleGear.BaseMultiplier(state, tuning);
        }

        /// <summary>
        /// 아직 안 보여줄 생산자인가 — <b>살 수 있게 되기 직전</b>까지만 감춘다.
        ///
        /// ★ 처음부터 여덟 줄을 다 보여주면 「지금 뭘 해야 하나」가 안 보인다.
        ///   쿠키 클리커도 다음 것을 슬쩍 보여주며 목표를 만든다.
        /// </summary>
        public static bool IsHidden(int kind, IdleState state, IdleTuning tuning)
        {
            if (kind <= 0)
            {
                return false;
            }

            if (state.Owned.Length > kind && state.Owned[kind] > 0L)
            {
                return false;
            }

            // 앞 번호를 하나라도 가졌고, 값의 절반은 모아 봤어야 보인다.
            bool hasPrevious = state.Owned.Length > kind - 1 && state.Owned[kind - 1] > 0L;
            bool nearlyAfford = state.Resource >= CostOf(kind, 0L, tuning) * 0.5d;

            return hasPrevious == false || nearlyAfford == false;
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
