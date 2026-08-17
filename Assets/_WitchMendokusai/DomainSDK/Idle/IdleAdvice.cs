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

        /// <summary>가방에 더 좋은 것이 있는데 안 차고 있다 — <b>공짜로</b> 세진다.</summary>
        Wear = 9,

        /// <summary>생산자를 살 수 있다.</summary>
        BuyProducer = 6,

        /// <summary>강화를 올릴 수 있다.</summary>
        Raise = 7,

        /// <summary>손으로 때려라 — 아직 아무것도 안 도는 첫 1분.</summary>
        Tap = 8,
    }

    /// <summary>서랍의 칸 — 화면의 순서와 같다.</summary>
    public enum IdleTab
    {
        Base = 0,
        Upgrade = 1,
        Gear = 2,
        Hero = 3,
        Prestige = 4,
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
        /// <summary>합칠 것을 세는 데 쓰는 <b>한 장짜리 판</b> — 매번 새로 만들지 않는다.</summary>
        [System.ThreadStatic]
        private static int[] scratch;


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

            // ④ <b>공짜로 세지는 것</b>이 먼저다. 차는 데는 아무것도 안 든다 —
            //    그걸 두고 「돈을 써라」라고 말하는 안내는 틀린 안내다.
            //    (전에는 아예 말하지 않아서, 좋은 장비가 가방에서 잠자도 화면이 조용했다.)
            if (HasBetterUnworn(snapshot))
            {
                return new IdleAdviceResult(IdleStep.Wear, -1, 0d);
            }

            if (snapshot.CanPull)
            {
                return new IdleAdviceResult(IdleStep.Pull, -1, snapshot.PullCost);
            }

            int mergeable = MergeableCount(snapshot);
            if (mergeable > 0)
            {
                return new IdleAdviceResult(IdleStep.Merge, -1, mergeable);
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

        /// <summary>
        /// 그 칸에 <b>지금 할 것이 있나</b> — 닫힌 칸에 점을 찍기 위한 것 (TASK-WM-406).
        ///
        /// ★ 서랍을 들이면서 다섯 칸 중 넷이 늘 <b>안 보이게</b> 됐다. 안 보이는 곳에서
        ///   할 수 있는 일이 생기면 사람은 그걸 영영 모른다 — 서랍이 만든 빚이다.
        ///   <see cref="NextStep"/> 는 <b>하나</b>만 가리키므로 이 빚을 못 갚는다.
        ///   그래서 칸마다 따로 묻는다.
        ///
        /// ★ 장비의 「더 좋은 것을 안 찼다」는 <b>좁게</b> 본다 — 칸이 비었거나, 등급이
        ///   더 높거나, 등급이 같고 잠재가 더 높을 때만. 실제 값어치는 튜닝(등급 보너스)에
        ///   걸리는데 화면 꼴은 튜닝을 안 들고 있다. 넓게 어림잡아 <b>틀린 점</b>을 찍느니
        ///   확실한 것만 찍는다 — 거짓 점은 점을 못 믿게 만들고, 못 믿는 점은 없는 것만 못하다.
        /// </summary>
        public static bool HasSomethingToDo(IdleSnapshot snapshot, IdleTab tab)
        {
            switch (tab)
            {
                case IdleTab.Base:
                    return CheapestAffordableProducer(snapshot) >= 0;

                case IdleTab.Upgrade:
                    return snapshot.Damage.CanAfford || snapshot.AttackSpeed.CanAfford;

                case IdleTab.Gear:
                    return snapshot.Bag.Length >= snapshot.BagCapacity
                        || MergeableCount(snapshot) > 0
                        || HasBetterUnworn(snapshot);

                case IdleTab.Hero:
                    return snapshot.CanPull || HasEmptyPartySeat(snapshot);

                case IdleTab.Prestige:
                    return snapshot.PrestigeAward > 0L && snapshot.MaxTierNow >= snapshot.TierCeiling;

                default:
                    return false;
            }
        }

        /// <summary>가방에 <b>확실히</b> 더 나은 것이 있나 — 위 주석의 좁은 뜻.</summary>
        public static bool HasBetterUnworn(IdleSnapshot snapshot)
        {
            for (int index = 0; index < snapshot.Bag.Length; index++)
            {
                IdleItem one = snapshot.Bag[index];
                if (one.IsEmpty)
                {
                    continue;
                }

                int slot = (int)one.Slot;
                if (slot < 0 || slot >= snapshot.Worn.Length)
                {
                    continue;
                }

                IdleItem wearing = snapshot.Worn[slot];
                if (wearing.IsEmpty)
                {
                    return true;
                }

                if (one.Tier > wearing.Tier)
                {
                    return true;
                }

                if (one.Tier == wearing.Tier && one.PotentialValue > wearing.PotentialValue)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>영웅이 있는데 파티 자리가 비었나 — 안 세우면 배수가 그냥 놀고 있다.</summary>
        public static bool HasEmptyPartySeat(IdleSnapshot snapshot)
        {
            if (snapshot.Heroes.Length <= 0)
            {
                return false;
            }

            int seated = 0;
            for (int seat = 0; seat < snapshot.Party.Length; seat++)
            {
                if (snapshot.Party[seat] >= 0)
                {
                    seated++;
                }
            }

            return seated < snapshot.Party.Length && seated < snapshot.Heroes.Length;
        }

        /// <summary>합칠 수 있는 묶음 수 — 같은 부위·같은 등급 셋.</summary>
        public static int MergeableCount(IdleSnapshot snapshot)
        {
            // ⚠ 전에는 부를 때마다 <b>새 판</b>을 하나 만들었다(new int[64]).
            //   화면이 매 프레임 이걸 예닐곱 번 부른다(서랍 점 다섯 + 다음 한 걸음) —
            //   8시간 켜 두는 게임에서 그건 초당 수백 개의 쓰레기다.
            //   방치형은 <b>오래 켜 두는 것</b>이 기본값이라 프레임당 할당이 곧 끊김이 된다.
            //   판은 한 번만 만들고 씻어 쓴다. [ThreadStatic] 인 이유는 시험이 여러 판에서
            //   동시에 돌기 때문이다(한 판을 나눠 쓰면 서로의 셈을 밟는다).
            int[] counts = scratch;

            if (counts == null)
            {
                counts = new int[64];
                scratch = counts;
            }
            else
            {
                System.Array.Clear(counts, 0, counts.Length);
            }

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

                // ⚠ 전에는 「3 에 닿을 때 한 번」만 셌다 — 여섯 개를 들고 있어도 <b>한 벌</b>이라고
                //   말했다(실제로는 두 벌을 합칠 수 있다). 그리고 그 3 이 <b>여기 박혀</b> 있어서,
                //   인스펙터에서 손잡이를 4 로 바꾸면 안내만 조용히 거짓말을 했다.
                //   이제 판이 말해 주는 수로 세고, 벌이 찰 때마다 센다.
                if (snapshot.MergeCount > 1 && counts[key] % snapshot.MergeCount == 0)
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
            // ⚠ 두 축을 <b>다</b> 본다. 전에는 공격력만 보고 공격속도를 빼먹었다 —
            //   속도가 20초 뒤에 살 수 있는데도 화면이 「3분 뒤」라고 말할 수 있었다.
            //   기다리라는 말은 <b>얼마나</b>가 맞아야 안내가 된다. 틀린 시각은 침묵보다 나쁘다.
            double soonest = Sooner(double.PositiveInfinity, snapshot.Damage.SecondsToAfford);
            soonest = Sooner(soonest, snapshot.AttackSpeed.SecondsToAfford);

            for (int kind = 0; kind < snapshot.Producers.Length; kind++)
            {
                IdleProducerView view = snapshot.Producers[kind];
                if (view.Hidden || view.SecondsToAfford <= 0d)
                {
                    continue;
                }

                soonest = Sooner(soonest, view.SecondsToAfford);
            }

            return soonest;
        }

        /// <summary>둘 중 이른 쪽 — 0 이하는 「모른다」라서 안 센다.</summary>
        private static double Sooner(double soonest, double seconds)
        {
            if (seconds <= 0d)
            {
                return soonest;
            }

            return seconds < soonest ? seconds : soonest;
        }
    }
}
