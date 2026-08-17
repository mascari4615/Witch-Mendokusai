namespace WitchMendokusai.DomainSDK.Idle
{
    /// <summary>지금 할 <b>한 걸음</b>이 무엇인가.</summary>
    public enum IdleStep
    {
        /// <summary>모으는 중 — 지금 당장 할 것이 없다.</summary>
        Wait = 0,

        /// <summary>판 위를 지나가는 것을 잡아라 — 놓치면 사라진다.</summary>
        CatchVisitor = 1,

        /// <summary>가방이 찼다 — 지금 떨구는 것은 버려진다.</summary>
        BagFull = 2,

        /// <summary>환생할 때다 — 더 내려가도 등급이 안 열린다.</summary>
        Prestige = 3,

        /// <summary>영웅을 뽑을 수 있다.</summary>
        Pull = 4,

        /// <summary>같은 것 셋을 합칠 수 있다.</summary>
        Merge = 5,

        /// <summary>생산자를 살 수 있다.</summary>
        BuyProducer = 6,

        /// <summary>강화를 올릴 수 있다.</summary>
        Raise = 7,

        /// <summary>손으로 때려라 — 아직 아무것도 안 도는 첫 1분.</summary>
        Tap = 8,
    }

    /// <summary>한 걸음과 그에 딸린 숫자 (없으면 0).</summary>
    public readonly struct IdleAdviceResult
    {
        public IdleAdviceResult(IdleStep step, int subject, double amount)
        {
            Step = step;
            Subject = subject;
            Amount = amount;
        }

        public IdleStep Step { get; }

        /// <summary>무엇에 대한 것인가 — 생산자 번호 등. 없으면 -1.</summary>
        public int Subject { get; }

        /// <summary>딸린 값 — 환생석 개수, 수입 배수, 기다릴 초 등. 없으면 0.</summary>
        public double Amount { get; }
    }

    /// <summary>
    /// 지금 <b>가장 값어치 있는 한 걸음</b>을 고른다 (TASK-WM-406).
    ///
    /// ★ 왜 코어에 있나 — 이 판단은 <b>규칙</b>이지 그림이 아니다. 화면 안에 두면
    ///   ① 시험할 수 없고 ② 창마다 다른 답을 낸다(같은 판이 다르게 보인다).
    ///   말로 옮기는 것만 화면이 한다.
    ///
    /// ★ 기존 지적: 「첫 30분에 뭘 눌러야 하는지가 안 보인다」.
    ///   튜토리얼 팝업 대신 <b>지금 상태에서 파생된 한 걸음</b>이라 낡지 않고,
    ///   후반에도 다음 목표를 계속 가리킨다.
    ///
    /// ★ 순서가 곧 규칙이다 — 사라지는 것 → 손해 보는 것 → 판을 가르는 것 →
    ///   모은 것을 쓰는 것 → 사는 것 → 기다림.
    ///   여러 개를 한꺼번에 말하면 그건 안내가 아니라 <b>목록</b>이고, 목록은 이미 서랍에 있다.
    /// </summary>
    public static class IdleAdvice
    {
        public static IdleAdviceResult NextStep(IdleSnapshot snapshot)
        {
            // ① 사라지는 것이 먼저다 — 지금 안 누르면 없어진다.
            if (snapshot.VisitorSecondsLeft > 0d)
            {
                return new IdleAdviceResult(IdleStep.CatchVisitor, -1, snapshot.VisitorSecondsLeft);
            }

            // ② 가방이 찬 동안에는 잡을수록 손해다 — 버는 것보다 잃는 것을 먼저 막는다.
            if (snapshot.Bag.Length >= snapshot.BagCapacity)
            {
                return new IdleAdviceResult(IdleStep.BagFull, -1, snapshot.BagCapacity);
            }

            // ③ 천장에 닿았으면 더 내려가도 등급이 안 열린다 — 판을 가를 때다.
            if (snapshot.PrestigeAward > 0L && snapshot.MaxTierNow >= snapshot.TierCeiling)
            {
                return new IdleAdviceResult(IdleStep.Prestige, -1, snapshot.PrestigeAward);
            }

            if (snapshot.CanPull)
            {
                return new IdleAdviceResult(IdleStep.Pull, -1, snapshot.PullCost);
            }

            if (MergeableCount(snapshot) > 0)
            {
                return new IdleAdviceResult(IdleStep.Merge, -1, MergeableCount(snapshot));
            }

            int cheapest = CheapestAffordableProducer(snapshot);
            if (cheapest >= 0)
            {
                return new IdleAdviceResult(IdleStep.BuyProducer, cheapest,
                    snapshot.Producers[cheapest].IncomeGain);
            }

            if (snapshot.Damage.CanAfford || snapshot.AttackSpeed.CanAfford)
            {
                return new IdleAdviceResult(IdleStep.Raise, -1, 0d);
            }

            // 아무것도 안 도는 첫 1분 — 손이 제일 빠르다.
            if (snapshot.IncomePerSecond <= 0d)
            {
                return new IdleAdviceResult(IdleStep.Tap, -1, 0d);
            }

            return new IdleAdviceResult(IdleStep.Wait, -1, SoonestWait(snapshot));
        }

        /// <summary>합칠 수 있는 묶음 수 — 같은 부위·같은 등급 셋.</summary>
        public static int MergeableCount(IdleSnapshot snapshot)
        {
            int[] counts = new int[64];
            int found = 0;

            for (int index = 0; index < snapshot.Bag.Length; index++)
            {
                IdleItem one = snapshot.Bag[index];
                int key = one.Tier * 4 + (int)one.Slot;

                if (key < 0 || key >= counts.Length)
                {
                    continue;
                }

                counts[key]++;
                if (counts[key] == 3)
                {
                    found++;
                }
            }

            return found;
        }

        /// <summary>지금 살 수 있는 것 중 가장 싼 생산자 — 없으면 -1.</summary>
        public static int CheapestAffordableProducer(IdleSnapshot snapshot)
        {
            int found = -1;
            double best = double.PositiveInfinity;

            for (int kind = 0; kind < snapshot.Producers.Length; kind++)
            {
                IdleProducerView view = snapshot.Producers[kind];
                if (view.Hidden || view.CanAfford == false || view.NextCost >= best)
                {
                    continue;
                }

                best = view.NextCost;
                found = kind;
            }

            return found;
        }

        /// <summary>가장 빨리 살 수 있게 되는 것까지 남은 초 — 없으면 무한.</summary>
        private static double SoonestWait(IdleSnapshot snapshot)
        {
            double soonest = snapshot.Damage.SecondsToAfford;

            if (soonest <= 0d)
            {
                soonest = double.PositiveInfinity;
            }

            for (int kind = 0; kind < snapshot.Producers.Length; kind++)
            {
                IdleProducerView view = snapshot.Producers[kind];
                if (view.Hidden || view.SecondsToAfford <= 0d)
                {
                    continue;
                }

                if (view.SecondsToAfford < soonest)
                {
                    soonest = view.SecondsToAfford;
                }
            }

            return soonest;
        }
    }
}
