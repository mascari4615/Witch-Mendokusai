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

        /// <summary>
        /// 한 번 뽑는다. 자원이나 환생석이 모자라면 아무 일도 안 일어난다.
        /// </summary>
        public static bool TryPull(IdleState state, IdleTuning tuning, out IdleHeroPull pull)
        {
            pull = default;

            if (CanPull(state, tuning) == false)
            {
                return false;
            }

            state.Resource -= CostOf(state, tuning);
            state.Stones -= StoneCostOf(tuning);
            state.PullsDone += 1L;
            state.PullsSincePity += 1;

            // 주사위는 판이 들고 다닌다 — 껐다 켜서 다시 굴리는 것을 막는다(감정과 같은 규칙).
            IdleRandom dice = new IdleRandom(state.RandomState);

            bool byPity = state.PullsSincePity >= tuning.PityPulls;
            IdleHeroGrade grade = byPity ? IdleHeroGrade.Legend : RollGrade(ref dice, tuning);

            if (grade == IdleHeroGrade.Legend)
            {
                state.PullsSincePity = 0;
            }

            int id = PickOfGrade(ref dice, grade);
            state.RandomState = dice.State;

            pull = Give(state, tuning, id, grade, byPity);
            return true;
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

        private static int PickOfGrade(ref IdleRandom dice, IdleHeroGrade grade)
        {
            List<int> pool = new List<int>();
            IdleHeroes.IdsOfGrade(grade, pool);

            if (pool.Count == 0)
            {
                return 0;
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
