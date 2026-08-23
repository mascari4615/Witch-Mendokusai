namespace WitchMendokusai.DomainSDK.Idle
{
    /// <summary>잠재 등급 — 장비 등급이 정한다.</summary>
    public enum PotentialGrade
    {
        /// <summary>1등급 장비에는 잠재가 안 붙는다.</summary>
        None = 0,
        Rare = 1,
        Epic = 2,
        Unique = 3,
        Legendary = 4,
    }

    /// <summary>
    /// 잠재옵션 — <b>도박은 여기서만 한다</b> (TASK-WM-406).
    ///
    /// ★ 왜 여기냐 — 떨구기는 주사위를 안 굴린다(<see cref="IdleDrops"/>). 처치마다 굴리면
    ///   8시간 오프라인이 처치 수만큼 굴려야 해서 「60초 한 번 == 0.1초 600번」이 깨지고,
    ///   오프라인 보상이 통째로 그 성질 위에 서 있다.
    ///   그래서 무작위를 <b>사람이 누를 때만</b> 굴리는 자리로 옮겼다 — 누른 횟수만큼만 굴린다.
    ///   울티마 스쿼드도 도박은 여기서 한다.
    ///
    /// ★ 연쇄가 이 게임의 몸통이다: <b>깊이 → 장비 등급 → 잠재 등급 → 값</b>.
    ///   근거는 울티마 스쿼드의 실제 표다 — 2~3등급 레어 · 4~5 에픽 · 6 유니크 · 7~8 레전드리.
    ///   그래서 좋은 잠재를 원하면 <b>내려가는 수밖에</b> 없다. 등급을 못 건너뛴다.
    /// </summary>
    public static class IdlePotentials
    {
        /// <summary>
        /// 장비 등급이 정하는 잠재 등급 — 울티마 스쿼드 표 그대로.
        /// 1등급엔 안 붙는다(그래야 첫 등급이 「아직 시작도 안 한 것」이 된다).
        /// </summary>
        public static PotentialGrade GradeFor(int tier)
        {
            if (tier <= 1)
            {
                return PotentialGrade.None;
            }

            if (tier <= 3)
            {
                return PotentialGrade.Rare;
            }

            if (tier <= 5)
            {
                return PotentialGrade.Epic;
            }

            if (tier <= 6)
            {
                return PotentialGrade.Unique;
            }

            return PotentialGrade.Legendary;
        }

        /// <summary>
        /// 지금 이 등급을 감정할 수 있나 — 못 하면 <b>왜</b>.
        ///
        /// ★ 거절 조건이 두 벌이 되면 화면만 거짓말을 한다. 전에는 화면이 이 셋을
        ///   자기가 베껴 판정했다 — 코어가 규칙을 바꾸면 버튼은 켜져 있는데 눌러도
        ///   아무 일이 안 일어나는 상태가 된다. <see cref="TryAppraise"/> 도 이 답을 쓴다.
        /// </summary>
        public static AppraiseBlock WhyNot(IdleState state, IdleTuning tuning, int tier)
        {
            if (GradeFor(tier) == PotentialGrade.None)
            {
                return AppraiseBlock.TierTooLow;
            }

            int slot = tier - 1;
            if (slot < 0 || slot >= state.DroppedByTier.Length || state.DroppedByTier[slot] <= 0L)
            {
                return AppraiseBlock.NothingDropped;
            }

            // ★ 자원이 든다 — 공짜면 「올릴까 감정할까」가 결정이 아니다.
            //   기지가 낸 자원과 모험이 가져온 장비가 <b>같은 저울</b>에 올라가는 자리가 여기다.
            if (state.Resource < IdleGear.AppraiseCost(tier, tuning))
            {
                return AppraiseBlock.TooPoor;
            }

            return AppraiseBlock.None;
        }

        /// <summary>이 등급이 낼 수 있는 가장 낮은 값.</summary>
        public static double FloorOf(PotentialGrade grade, IdleTuning tuning)
        {
            if (grade == PotentialGrade.None)
            {
                return 0d;
            }

            return tuning.PotentialByGrade.At((int)grade - 1);
        }

        /// <summary>이 등급이 낼 수 있는 가장 높은 값(미만).</summary>
        public static double CeilingOf(PotentialGrade grade, IdleTuning tuning)
        {
            if (grade == PotentialGrade.None)
            {
                return 0d;
            }

            return FloorOf(grade, tuning) * tuning.PotentialSpread;
        }

        /// <summary>
        /// 떨어진 것 하나를 <b>감정한다</b> — 잠재가 붙어 나온다.
        ///
        /// ★ 개수를 하나 쓴다. 안 쓰면 무한히 굴릴 수 있고, 그러면 「깊이」가 아무 뜻이 없어진다
        ///   (얕은 데서 백만 번 굴려 최고값을 뽑을 수 있게 되므로).
        ///
        /// ★ <b>더 좋을 때만</b> 갈아 끼운다. 나쁜 게 나와도 잃지 않으니 사람이 계속 누를 수 있다 —
        ///   잃을 수 있게 만들면 안 누르게 되고, 그러면 도박이 아니라 벌이다.
        /// </summary>
        public static bool TryAppraise(IdleState state, IdleTuning tuning, int tier, out PotentialRoll roll)
        {
            roll = default;

            if (WhyNot(state, tuning, tier) != AppraiseBlock.None)
            {
                return false;
            }

            state.Resource -= IdleGear.AppraiseCost(tier, tuning);
            Roll(state, tuning, tier, out roll);
            return true;
        }

        /// <summary>
        /// 자원 없이 한 번 굴린다 — <b>감정 카드</b>의 길 (V2). 개수는 똑같이 쓴다.
        ///
        /// ★ 개수마저 공짜면 얕은 데서 무한히 굴릴 수 있어 「깊이」가 뜻을 잃는다 —
        ///   카드가 면제하는 것은 <b>자원 하나</b>다.
        /// </summary>
        public static bool RollFree(IdleState state, IdleTuning tuning, int tier, out PotentialRoll roll)
        {
            roll = default;

            AppraiseBlock why = WhyNot(state, tuning, tier);
            if (why != AppraiseBlock.None && why != AppraiseBlock.TooPoor)
            {
                return false;
            }

            Roll(state, tuning, tier, out roll);
            return true;
        }

        /// <summary>실제 굴림 — 값을 치른 뒤의 한 길. 두 벌이 되면 언젠가 갈린다.</summary>
        private static void Roll(IdleState state, IdleTuning tuning, int tier, out PotentialRoll roll)
        {
            PotentialGrade grade = GradeFor(tier);

            state.DroppedByTier[tier - 1] -= 1L;

            IdleRandom dice = new IdleRandom(state.RandomState);
            double value = dice.NextRange(FloorOf(grade, tuning), CeilingOf(grade, tuning));
            state.RandomState = dice.State;

            bool better = value > state.BestPotentialValue;
            if (better)
            {
                state.BestPotentialValue = value;
                state.BestPotentialGrade = (int)grade;
            }

            roll = new PotentialRoll(tier, grade, value, better);
        }
    }

    /// <summary>왜 감정을 못 하나 — 화면이 <b>이유를 말할</b> 재료.</summary>
    public enum AppraiseBlock
    {
        /// <summary>할 수 있다.</summary>
        None = 0,

        /// <summary>1등급엔 잠재가 안 붙는다.</summary>
        TierTooLow = 1,

        /// <summary>그 등급이 아직 안 떨어졌다.</summary>
        NothingDropped = 2,

        /// <summary>자원이 모자란다.</summary>
        TooPoor = 3,
    }

    /// <summary>한 번 감정한 결과 — 화면이 「무엇이 나왔나」를 보여줄 재료.</summary>
    public readonly struct PotentialRoll
    {
        public int Tier { get; }
        public PotentialGrade Grade { get; }

        /// <summary>이번에 나온 값(비율).</summary>
        public double Value { get; }

        /// <summary>여태 가진 것보다 좋았나 — 갈아 끼워졌다는 뜻.</summary>
        public bool Replaced { get; }

        public PotentialRoll(int tier, PotentialGrade grade, double value, bool replaced)
        {
            Tier = tier;
            Grade = grade;
            Value = value;
            Replaced = replaced;
        }
    }
}
