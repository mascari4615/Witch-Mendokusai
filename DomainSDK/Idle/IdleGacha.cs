using System.Collections.Generic;

namespace WitchMendokusai.DomainSDK.Idle
{
    /// <summary>한 번 뽑은 결과 — 화면이 보여줄 것.</summary>
    public readonly struct IdleHeroPull
    {
        public IdleHeroPull(int id, IdleHeroGrade grade, bool isNew, bool starredUp, int stars, bool byPity)
        {
            Id = id;
            Grade = grade;
            IsNew = isNew;
            StarredUp = starredUp;
            Stars = stars;
            ByPity = byPity;
        }

        public int Id { get; }
        public IdleHeroGrade Grade { get; }

        /// <summary>처음 본 얼굴.</summary>
        public bool IsNew { get; }

        /// <summary>중복이 쌓여 ★ 이 올랐다.</summary>
        public bool StarredUp { get; }

        public int Stars { get; }

        /// <summary><b>천장</b>에 걸려 나온 것 — 「드디어」를 화면이 말할 수 있게.</summary>
        public bool ByPity { get; }
    }

    /// <summary>
    /// 영웅 뽑기 (TASK-WM-406).
    ///
    /// ★ 사용자 결정 2026-08-17 = <b>관대한 판</b>. 한 번 환생하면 여러 번 뽑고,
    ///   최고 등급도 가끔 나온다. 재미를 「모으는 맛」이 아니라 <b>「짜는 맛」</b>에 둔다.
    ///   돈을 안 받는 싱글 게임에서 짠맛은 <b>그냥 기다림</b>이지 긴장이 아니다.
    ///
    /// ★ <b>천장</b>(pity)이 있다. 없으면 불운 한 번이 곧 이탈이다 — 확률이 아무리 옳아도
    ///   사람은 자기 표본만 본다. 천장은 「언젠가는 온다」를 <b>약속</b>으로 바꾼다.
    ///
    /// ★ 무작위는 <b>사람이 누를 때만</b> 굴린다 — 이 게임의 규칙이다(감정과 같은 갈래).
    ///   방치 진행은 결정적이라야 오프라인 보상이 성립한다.
    /// </summary>
    public static class IdleGacha
    {
        /// <summary>
        /// 지금 한 번 뽑는 값 (<b>자원</b>) — 뽑을수록 오른다.
        ///
        /// ★ 값이 뽑은 횟수를 따라 오르면 자원이 아무리 많아도 뽑기 수는 로그로 눌린다.
        ///   생산자와 같은 꼴이라 따로 배울 것이 없다.
        /// </summary>
        public static double CostOf(IdleState state, IdleTuning tuning)
        {
            return tuning.PullCostBase * System.Math.Pow(tuning.PullCostRatio, state.PullsDone);
        }

        /// <summary>한 번 뽑는 데 드는 환생석 — 자원과 <b>둘 다</b> 낸다(사용자 결정 2026-08-17).</summary>
        public static long StoneCostOf(IdleTuning tuning)
        {
            return tuning.PullStoneCost;
        }

        /// <summary>지금 뽑을 수 있나 — 둘 다 있어야 한다.</summary>
        public static bool CanPull(IdleState state, IdleTuning tuning)
        {
            return state.Resource >= CostOf(state, tuning)
                && state.Stones >= StoneCostOf(tuning);
        }

        /// <summary>묶음 뽑기 값 (자원). 1회의 묶음 수 배, 할인 없음 (사용자 2026-09-05)</summary>
        public static double BatchCostOf(IdleState state, IdleTuning tuning)
        {
            return CostOf(state, tuning) * tuning.PullBatchCount;
        }

        /// <summary>묶음 뽑기에 드는 환생석. 1회의 묶음 수 배</summary>
        public static long BatchStoneCostOf(IdleTuning tuning)
        {
            return StoneCostOf(tuning) * tuning.PullBatchCount;
        }

        /// <summary>묶음으로 뽑을 수 있나</summary>
        public static bool CanPullBatch(IdleState state, IdleTuning tuning)
        {
            return tuning.PullBatchCount > 0
                && state.Resource >= BatchCostOf(state, tuning)
                && state.Stones >= BatchStoneCostOf(tuning);
        }

        /// <summary>
        /// 한 번 뽑는다. 자원이나 환생석이 모자라면 아무 일도 안 일어난다.
        /// </summary>
        public static bool TryPull(IdleState state, IdleTuning tuning, out IdleHeroPull pull)
        {
            return TryPull(state, tuning, -1, out pull);
        }

        /// <summary>한 번 뽑는다. <paramref name="pickupId"/> 가 같은 등급에 있으면 그 얼굴이 더 잘 나온다 (픽업)</summary>
        public static bool TryPull(IdleState state, IdleTuning tuning, int pickupId, out IdleHeroPull pull)
        {
            pull = default;

            if (CanPull(state, tuning) == false)
            {
                return false;
            }

            state.Resource -= CostOf(state, tuning);
            state.Stones -= StoneCostOf(tuning);
            pull = RollOne(state, tuning, pickupId, IdleHeroGrade.Common);
            return true;
        }

        /// <summary>
        /// 묶음으로 뽑는다 (사용자 2026-09-05: 10회). 값은 1회의 묶음 수 배, 할인 없음.
        /// 묶음 안에 <see cref="IdleTuning.PullBatchFloorGrade"/> 이상이 하나도 없으면 마지막 하나를 그 등급으로
        /// </summary>
        public static bool TryPullBatch(IdleState state, IdleTuning tuning, int pickupId, List<IdleHeroPull> into)
        {
            if (CanPullBatch(state, tuning) == false)
            {
                return false;
            }

            state.Resource -= BatchCostOf(state, tuning);
            state.Stones -= BatchStoneCostOf(tuning);

            IdleHeroGrade floor = (IdleHeroGrade)tuning.PullBatchFloorGrade;
            bool floorSeen = false;
            for (int index = 0; index < tuning.PullBatchCount; index++)
            {
                bool last = index == tuning.PullBatchCount - 1;
                IdleHeroGrade least = last && floorSeen == false ? floor : IdleHeroGrade.Common;
                IdleHeroPull pull = RollOne(state, tuning, pickupId, least);
                floorSeen = floorSeen || pull.Grade >= floor;
                into.Add(pull);
            }

            return true;
        }

        /// <summary>값을 이미 낸 뒤 하나를 굴린다. 천장과 최저 등급을 여기서 맞춘다</summary>
        private static IdleHeroPull RollOne(IdleState state, IdleTuning tuning, int pickupId, IdleHeroGrade least)
        {
            state.PullsDone += 1L;
            state.PullsSincePity += 1;

            // 주사위는 판이 들고 다님. 껐다 켜서 다시 굴리는 것을 막는 자리 (감정과 같은 규칙)
            IdleRandom dice = new IdleRandom(state.RandomState);

            bool byPity = state.PullsSincePity >= tuning.PityPulls;
            IdleHeroGrade grade = byPity ? IdleHeroGrade.Legend : RollGrade(ref dice, tuning);
            if (grade < least)
            {
                grade = least;
            }

            if (grade == IdleHeroGrade.Legend)
            {
                state.PullsSincePity = 0;
            }

            int id = PickOfGrade(ref dice, grade, pickupId, tuning.PickupWeight);
            state.RandomState = dice.State;

            return Give(state, tuning, id, grade, byPity);
        }

        /// <summary>
        /// 픽업을 셀 때 쓰는 판. 사진마다 새로 만들면 밤새 켜 둔 만큼 쌓인다 (실측 2026-09-05)
        ///
        /// ★ 값을 들고 있지 않다. 채우고 바로 읽고 버리는 자리라 판정 결과에 영향 없음
        /// </summary>
        private static readonly List<int> pickupPool = new List<int>();

        /// <summary>
        /// 지금 픽업인 인형 (사용자 2026-09-05: 특정 인형 확률 2배, 주마다 교체).
        /// 얼굴이 있는 가장 높은 등급에서 주기 번호 순으로. 명단이 비면 -1
        /// </summary>
        public static int PickupHeroOf(IdleTuning tuning, long nowUnixSeconds)
        {
            List<int> pool = pickupPool;
            pool.Clear();
            for (IdleHeroGrade grade = IdleHeroGrade.Legend; grade >= IdleHeroGrade.Common; grade--)
            {
                IdleHeroes.IdsOfGrade(grade, pool);
                if (pool.Count > 0)
                {
                    break;
                }
            }

            if (pool.Count == 0)
            {
                return -1;
            }

            long period = PickupPeriodOf(tuning, nowUnixSeconds);
            int at = (int)(((period % pool.Count) + pool.Count) % pool.Count);
            return pool[at];
        }

        /// <summary>픽업이 바뀌기까지 남은 초</summary>
        public static double PickupSecondsLeft(IdleTuning tuning, long nowUnixSeconds)
        {
            long period = PickupPeriodOf(tuning, nowUnixSeconds);
            long nextBoundary = (period + 1L) * PickupDaysOf(tuning) * SECONDS_PER_DAY + tuning.DayResetOffsetSeconds;
            return nextBoundary - nowUnixSeconds;
        }

        private const long SECONDS_PER_DAY = 86400L;

        private static long PickupDaysOf(IdleTuning tuning)
        {
            return tuning.PickupDays > 0L ? tuning.PickupDays : 1L;
        }

        /// <summary>몇 번째 픽업 주기인가. 날 번호를 주기 길이로 내림 나눗셈 (음수도 아래로)</summary>
        private static long PickupPeriodOf(IdleTuning tuning, long nowUnixSeconds)
        {
            long day = IdleDungeons.DayIndexOf(nowUnixSeconds, tuning.DayResetOffsetSeconds);
            long days = PickupDaysOf(tuning);
            long period = day / days;
            if (day < 0L && day % days != 0L)
            {
                period -= 1L;
            }

            return period;
        }

        /// <summary>등급을 굴린다 — 위에서부터 훑어 내려간다.</summary>
        private static IdleHeroGrade RollGrade(ref IdleRandom dice, IdleTuning tuning)
        {
            double roll = dice.NextDouble();

            if (roll < tuning.LegendChance)
            {
                return IdleHeroGrade.Legend;
            }

            if (roll < tuning.LegendChance + tuning.EpicChance)
            {
                return IdleHeroGrade.Epic;
            }

            if (roll < tuning.LegendChance + tuning.EpicChance + tuning.RareChance)
            {
                return IdleHeroGrade.Rare;
            }

            return IdleHeroGrade.Common;
        }

        /// <summary>등급 안에서 하나. 픽업이 그 등급에 있으면 무게 <paramref name="pickupWeight"/>, 나머지는 1</summary>
        private static int PickOfGrade(ref IdleRandom dice, IdleHeroGrade grade, int pickupId, double pickupWeight)
        {
            List<int> pool = new List<int>();
            IdleHeroes.IdsOfGrade(grade, pool);

            if (pool.Count == 0)
            {
                return 0;
            }

            int pickupAt = pickupId >= 0 ? pool.IndexOf(pickupId) : -1;
            if (pickupAt >= 0 && pickupWeight > 1d)
            {
                double total = pool.Count - 1 + pickupWeight;
                double roll = dice.NextDouble() * total;
                if (roll < pickupWeight || pool.Count == 1)
                {
                    return pickupId;
                }

                int others = (int)(roll - pickupWeight);
                if (others >= pool.Count - 1)
                {
                    others = pool.Count - 2;
                }

                return pool[others < pickupAt ? others : others + 1];
            }

            int at = (int)(dice.NextDouble() * pool.Count);
            if (at >= pool.Count)
            {
                at = pool.Count - 1;
            }

            return pool[at];
        }

        /// <summary>
        /// 뽑힌 영웅을 넣는다 — 처음이면 새 얼굴, 아니면 중복이 쌓여 ★ 이 오른다.
        ///
        /// ★ ★ 이 상한에 닿아도 <b>버리지 않는다</b> — 조각으로 남는다.
        ///   중복이 완전히 꽝이 되는 순간이 수집형이 죽는 자리다.
        /// </summary>
        private static IdleHeroPull Give(IdleState state, IdleTuning tuning, int id,
            IdleHeroGrade grade, bool byPity)
        {
            int at = state.IndexOfHero(id);

            if (at < 0)
            {
                state.Heroes.Add(new IdleHeroOwned(id));
                AutoFillParty(state, id);
                return new IdleHeroPull(id, grade, true, false, 0, byPity);
            }

            IdleHeroOwned owned = state.Heroes[at];
            owned.Copies += 1;

            bool starredUp = false;
            int needed = CopiesForNextStar(owned.Stars, tuning);

            if (owned.Stars < tuning.MaxStars && owned.Copies >= needed)
            {
                owned.Copies -= needed;
                owned.Stars += 1;
                starredUp = true;
            }

            state.Heroes[at] = owned;
            return new IdleHeroPull(id, grade, false, starredUp, owned.Stars, byPity);
        }

        /// <summary>다음 ★ 까지 필요한 중복 수 — 위로 갈수록 는다.</summary>
        public static int CopiesForNextStar(int stars, IdleTuning tuning)
        {
            return tuning.CopiesPerStar * (stars + 1);
        }

        /// <summary>
        /// 파티에 빈 자리가 있으면 새 얼굴을 <b>자동으로</b> 앉힌다.
        ///
        /// ★ 처음 뽑은 영웅이 아무 일도 안 하면 「뽑아도 그대로네」가 된다.
        ///   빈 자리를 채우는 건 결정이 아니라 잡일이라 기계가 한다 — 결정은 <b>자리가 찼을 때</b>부터다.
        /// </summary>
        private static void AutoFillParty(IdleState state, int id)
        {
            for (int slot = 0; slot < state.Party.Length; slot++)
            {
                if (state.Party[slot] < 0)
                {
                    state.Party[slot] = id;
                    return;
                }
            }
        }
    }
}
