using System.Collections.Generic;

namespace WitchMendokusai.DomainSDK.Idle
{
    /// <summary>
    /// 장비 — 가방 · 착용 · 합치기 (TASK-WM-406).
    ///
    /// ★ 사용자가 고른 「짜는 축」이다. 그전까지 이 게임에서 사람이 정하는 것은
    ///   <b>무엇을 올릴까</b> 하나뿐이었고, 그래서 재미가 없었다.
    ///   짜는 맛은 <b>자리가 나뉘어 있고 재료가 모자랄 때</b> 난다 —
    ///   부위 넷, 가방 한정, 합치면 사라지는 잠재. 셋 다 「무엇을 포기할까」를 만든다.
    ///
    /// ★ 대열 방치 전투 계열 심화 그대로 — 깊이가 등급 상한을 정하고, 같은 등급을 합쳐 올리고,
    ///   잠재는 감정으로 굴린다. 이미 있던 떨구기·잠재 규칙을 그대로 쓴다.
    /// </summary>
    public static class IdleGear
    {
        /// <summary>부위 수 — <see cref="IdleItemSlot"/> 와 맞춘다.</summary>
        public const int SLOT_COUNT = 4;

        /// <summary>
        /// 떨어진 것을 <b>아이템으로</b> 가방에 넣는다.
        ///
        /// ★ 부위는 <b>순서대로 돌려 준다</b>. 무작위로 주면 오프라인 정산이 통계가 되고,
        ///   이 게임의 떨구기는 결정적이어야 한다(오프라인 보상이 그 위에 서 있다).
        ///   대신 한 부위만 계속 나와 못 쓰는 일도 없다.
        ///
        /// ★ 가방이 차면 <b>안 들어온다</b>. 대열 방치 전투 계열에도 「장비 꽉참」이 있다 —
        ///   차는 것 자체가 「무엇을 합치고 무엇을 버릴까」라는 결정을 만든다.
        /// </summary>
        public static int Stow(IdleState state, IdleTuning tuning, int tier, long count)
        {
            if (tier < 1 || count <= 0L)
            {
                return 0;
            }

            int room = tuning.BagCapacity - state.Bag.Count;
            if (room <= 0)
            {
                return 0;
            }

            int taking = count > room ? room : (int)count;

            for (int one = 0; one < taking; one++)
            {
                IdleItemSlot slot = (IdleItemSlot)(state.DropSequence % SLOT_COUNT);
                state.DropSequence++;
                state.Bag.Add(new IdleItem(tier, slot));
            }

            return taking;
        }

        /// <summary>
        /// 이 한 벌의 <b>부위 번호가 실재하나</b> — 저장에서 온 값을 문 앞에서 거를 때 쓴다.
        ///
        /// 빈 것은 참으로 본다(빈 자리는 정상이다). 범위를 벗어난 부위가 섞이면
        /// 차는 순간 <c>Worn[그 번호]</c> 가 배열 밖을 짚어 터진다.
        /// </summary>
        public static bool IsRealSlot(IdleItem one)
        {
            if (one.IsEmpty)
            {
                return true;
            }

            int at = (int)one.Slot;
            return at >= 0 && at < SLOT_COUNT;
        }

        /// <summary>가방이 찼나 — 화면이 「정리해라」를 말할 수 있게.</summary>
        public static bool IsBagFull(IdleState state, IdleTuning tuning)
        {
            return state.Bag.Count >= tuning.BagCapacity;
        }

        /// <summary>
        /// 같은 부위·같은 등급 <see cref="IdleTuning.MergeCount"/> 개를 합쳐 한 단계 위로.
        ///
        /// ★ 합치면 <b>잠재는 사라진다</b>(새 것은 미감정). 안 그러면 좋은 잠재를 그대로 들고
        ///   등급만 올릴 수 있어 감정이 한 번으로 끝난다 — 도박이 한 번짜리가 되면 도박이 아니다.
        ///   그래서 「좋은 잠재를 지킬까, 등급을 올릴까」가 매번 결정이 된다.
        /// </summary>
        public static bool TryMerge(IdleState state, IdleTuning tuning, int tier, IdleItemSlot slot, out IdleItem made)
        {
            made = default;

            if (tuning.MergeCount < 2 || tier < 1)
            {
                return false;
            }

            // ★ 자원이 든다 — 그래야 기지와 모험이 같은 저울에 올라간다.
            double cost = MergeCost(tier, tuning);
            if (state.Resource < cost)
            {
                return false;
            }

            // ★ <b>나쁜 것부터</b> 재료로 쓴다. 전에는 가방 앞에서부터 셋을 집었다 —
            //   그래서 잘 나온 잠재를 들고 있으면 합치기 한 번에 <b>그게 먼저 먹혔다</b>.
            //   사람이 고른 적도 없는데 제일 좋은 것이 사라지는 것은 결정이 아니라 사고다.
            //   (「좋은 잠재를 지킬까, 등급을 올릴까」는 남는다 — 재료가 그것밖에 없으면 여전히 먹힌다.)
            List<int> picked = new List<int>();

            for (int index = 0; index < state.Bag.Count; index++)
            {
                IdleItem one = state.Bag[index];
                if (one.Tier != tier)
                {
                    continue;
                }

                int at = picked.Count;
                while (at > 0 && state.Bag[picked[at - 1]].PotentialValue > one.PotentialValue)
                {
                    at--;
                }

                picked.Insert(at, index);
            }

            if (picked.Count < tuning.MergeCount)
            {
                return false;
            }

            picked.RemoveRange(tuning.MergeCount, picked.Count - tuning.MergeCount);
            picked.Sort();

            // 뒤에서부터 지운다 — 앞에서 지우면 뒤 자리가 밀린다.
            for (int index = picked.Count - 1; index >= 0; index--)
            {
                state.Bag.RemoveAt(picked[index]);
            }

            state.Resource -= cost;

            IdleRandom random = new IdleRandom(state.RandomState);
            IdleItemSlot madeSlot = (IdleItemSlot)(int)(random.NextDouble() * SLOT_COUNT);
            state.RandomState = random.State;
            made = new IdleItem(tier + 1, madeSlot);
            state.Bag.Add(made);
            return true;
        }

        /// <summary>몇 벌이나 합칠 수 있나 — 화면이 「합치기」 버튼을 켤지 정하는 값.</summary>
        public static int CountMergeable(IdleState state, IdleTuning tuning, int tier, IdleItemSlot slot)
        {
            if (tuning.MergeCount < 2)
            {
                return 0;
            }

            int have = 0;
            for (int index = 0; index < state.Bag.Count; index++)
            {
                IdleItem one = state.Bag[index];
                if (one.Tier == tier)
                {
                    have++;
                }
            }

            return have / tuning.MergeCount;
        }

        /// <summary>
        /// 가방의 것을 그 부위에 <b>찬다</b>. 차고 있던 것은 가방으로 돌아온다 —
        /// 갈아 끼우다 잃으면 아무도 안 갈아 끼운다.
        /// </summary>
        public static bool TryEquip(IdleState state, int bagIndex)
        {
            if (bagIndex < 0 || bagIndex >= state.Bag.Count)
            {
                return false;
            }

            IdleItem taking = state.Bag[bagIndex];
            if (taking.IsEmpty)
            {
                return false;
            }

            int slot = (int)taking.Slot;
            IdleItem wearing = state.Worn[slot];

            state.Bag.RemoveAt(bagIndex);
            state.Worn[slot] = taking;

            if (wearing.IsEmpty == false)
            {
                state.Bag.Add(wearing);
            }

            return true;
        }

        /// <summary>
        /// 한 부위가 주는 배수.
        ///
        /// ★ <b>등급 자체도 값어치가 있다</b> — 잠재가 안 붙은 것도 차는 뜻이 있어야
        ///   「일단 등급부터 올릴까」가 선택지가 된다. 잠재만 세면 미감정 장비가 쓰레기다.
        ///
        /// ★ <b>부위마다 다른 축을 올린다</b> (사용자 지적 「안 녹아든다」의 답):
        ///   머리 = 공격력 · 손 = 공격속도 · 발 = 떨구기 · 몸 = 기지 생산.
        ///   그래야 장비가 <b>두 층 모두와</b> 물린다 — 모험이 가져온 것이 기지도 키운다.
        /// </summary>
        public static double MultiplierOf(IdleState state, IdleTuning tuning, IdleItemSlot slot)
        {
            IdleItem one = state.Worn.Length > (int)slot ? state.Worn[(int)slot] : default;
            return MultiplierOfItem(one, tuning);
        }

        /// <summary>
        /// 이 한 벌이 <b>주는 배수</b> — 차고 있든 가방에 있든 같은 셈이다.
        ///
        /// ★ 화면이 「차면 얼마나 좋아지나」를 말하려면 <b>가방에 있는 것</b>의 값도 필요하다.
        ///   그 셈을 화면이 따로 쓰면 언젠가 갈린다 — 이 저장소가 오늘 하루에만
        ///   같은 이유로 세 번 데었다(합치기 개수·도감 배수·기본값 드리프트).
        /// </summary>
        public static double MultiplierOfItem(IdleItem one, IdleTuning tuning)
        {
            if (one.IsEmpty)
            {
                return 1d;
            }

            return 1d + one.Tier * tuning.GearTierBonus + one.PotentialValue;
        }

        /// <summary>
        /// 이 한 벌을 <b>차면 그 부위가 어떻게 되나</b> — 지금 값과 찬 뒤 값.
        ///
        /// ★ 화면이 「x1.45 → x1.60」을 적으려면 필요한 셈인데, 화면에 두면 시험을 못 한다.
        ///   실제로 화면에 뒀다가 곧바로 틀렸다 (2026-08-17): 가방 <b>자리 번호</b>로 착용
        ///   배열의 경계를 봐서, 가방 다섯 번째 칸부터는 늘 「아무것도 안 찬 것」으로 쳤다
        ///   (가방은 40칸, 부위는 4개다). 부위로 찾는 셈은 부위를 아는 쪽에 있어야 한다.
        /// </summary>
        public static void CompareToWorn(IdleItem[] worn, IdleItem one, IdleTuning tuning,
            out double now, out double after)
        {
            now = 1d;
            after = MultiplierOfItem(one, tuning);

            int at = (int)one.Slot;

            if (worn != null && at >= 0 && at < worn.Length)
            {
                now = MultiplierOfItem(worn[at], tuning);
            }
        }

        /// <summary>머리 — 공격력.</summary>
        public static double DamageMultiplier(IdleState state, IdleTuning tuning)
        {
            return MultiplierOf(state, tuning, IdleItemSlot.Head);
        }

        /// <summary>손 — 공격속도.</summary>
        public static double SpeedMultiplier(IdleState state, IdleTuning tuning)
        {
            return MultiplierOf(state, tuning, IdleItemSlot.Hands);
        }

        /// <summary>발 — 떨구기.</summary>
        public static double DropMultiplier(IdleState state, IdleTuning tuning)
        {
            return MultiplierOf(state, tuning, IdleItemSlot.Feet);
        }

        /// <summary>몸 — 기지 생산.</summary>
        public static double BaseMultiplier(IdleState state, IdleTuning tuning)
        {
            return MultiplierOf(state, tuning, IdleItemSlot.Body);
        }

        /// <summary>감정에 드는 자원 — 등급이 높을수록 비싸다.</summary>
        public static double AppraiseCost(int tier, IdleTuning tuning)
        {
            if (tier < 1)
            {
                return double.PositiveInfinity;
            }

            return tuning.AppraiseBaseCost * System.Math.Pow(tuning.AppraiseCostRatio, tier - 1);
        }

        /// <summary>합치기에 드는 자원.</summary>
        public static double MergeCost(int tier, IdleTuning tuning)
        {
            return AppraiseCost(tier, tuning) * tuning.MergeCostFactor;
        }
    }
}
