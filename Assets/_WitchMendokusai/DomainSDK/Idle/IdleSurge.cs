namespace WitchMendokusai.DomainSDK.Idle
{
    /// <summary>지금 걸려 있는 폭주의 종류.</summary>
    public enum IdleSurgeKind
    {
        None = 0,

        /// <summary>판 전체가 잠시 빨라진다 (쿠키 클리커 Frenzy 자리).</summary>
        Frenzy = 1,

        /// <summary>손으로 때리는 값이 잠시 폭증한다 (Click Frenzy 자리).</summary>
        HandFrenzy = 2,
    }

    /// <summary>
    /// <b>지나가는 것</b> — 잠깐 떴다 사라지고, 누르면 잠시 폭주한다 (TASK-WM-406).
    ///
    /// ★ 왜 필요한가 (조사 1순위, <c>refs/cookie-clicker.md</c>) — 방치형은 기대값이 <b>평탄</b>해서
    ///   언제 꺼도 손해가 같다. 그러면 「지금 이 화면을 볼 이유」가 없다.
    ///   변동성은 그 평탄함에 <b>봉우리</b>를 만든다 — 놓치면 아깝고, 잡으면 판이 한참 앞서간다.
    ///
    /// ★ 뜨는 시각을 <b>다항 램프</b>로 만든다 — 쿠키 클리커의 <c>((t-Tmin)/T)^5</c>.
    ///   지수분포(무기억)면 「슬슬 뜰 때가 됐다」는 감각이 <b>거짓</b>이 되고, 그러면
    ///   다시 볼 이유도 사라진다. 5제곱 램프는 사람의 그 감각과 실제 확률을 <b>일치</b>시킨다.
    ///
    /// ★ 이건 <b>보고 있는 동안만</b> 일어난다. 자리를 비운 동안에는 안 뜬다 —
    ///   오프라인 보상은 결정적이어야 하고(스텝 불변), 놓친 봉우리를 나중에 주워 주면
    ///   「지금 볼 이유」가 도로 사라진다.
    /// </summary>
    public static class IdleSurge
    {
        /// <summary>
        /// 시간을 흘린다. 떠 있는 것이 사라지거나, 새로 뜨거나, 폭주가 끝난다.
        ///
        /// ★ 화면이 부르는 것이라 <see cref="IdleModel.Step"/> 과 갈라 둔다 —
        ///   판정(방치 진행)은 결정적이고 이건 사람이 보는 동안에만 도는 층이다.
        /// </summary>
        public static void Advance(IdleState state, IdleTuning tuning, double seconds)
        {
            if (seconds <= 0d)
            {
                return;
            }

            if (state.SurgeSecondsLeft > 0d)
            {
                state.SurgeSecondsLeft -= seconds;
                if (state.SurgeSecondsLeft <= 0d)
                {
                    state.SurgeSecondsLeft = 0d;
                    state.SurgeKind = (int)IdleSurgeKind.None;
                }
            }

            if (state.VisitorSecondsLeft > 0d)
            {
                // 떠 있는 것은 <b>기다려 주지 않는다</b> — 그래서 누르는 것이 사건이 된다.
                state.VisitorSecondsLeft -= seconds;
                if (state.VisitorSecondsLeft < 0d)
                {
                    state.VisitorSecondsLeft = 0d;
                }

                return;
            }

            state.SinceVisitorSeconds += seconds;
            if (TryAppear(state, tuning, seconds))
            {
                state.SinceVisitorSeconds = 0d;
                state.VisitorSecondsLeft = tuning.VisitorStaySeconds;
            }
        }

        /// <summary>
        /// 이번 조각에서 떴나 — <b>기다린 만큼 잘 뜬다</b>(5제곱 램프).
        /// </summary>
        private static bool TryAppear(IdleState state, IdleTuning tuning, double seconds)
        {
            double waited = state.SinceVisitorSeconds;
            if (waited < tuning.VisitorEarliestSeconds)
            {
                return false;
            }

            double span = tuning.VisitorLatestSeconds - tuning.VisitorEarliestSeconds;
            if (span <= 0d)
            {
                return true;
            }

            double walked = (waited - tuning.VisitorEarliestSeconds) / span;
            if (walked >= 1d)
            {
                return true;
            }

            // 이번 조각 안에 뜰 확률 — 램프가 높을수록 자주 뜬다.
            double chancePerSecond = walked * walked * walked * walked * walked / span * 5d;
            double chance = chancePerSecond * seconds;

            IdleRandom dice = new IdleRandom(state.RandomState);
            bool appeared = dice.NextDouble() < chance;
            state.RandomState = dice.State;

            return appeared;
        }

        /// <summary>지금 눌러서 잡을 수 있나.</summary>
        public static bool CanCatch(IdleState state)
        {
            return state.VisitorSecondsLeft > 0d;
        }

        /// <summary>
        /// 잡는다 — 무엇이 걸릴지는 <b>잡는 순간</b> 정해진다.
        ///
        /// ★ 사람이 누를 때만 굴린다(이 게임의 규칙). 미리 정해 두면 저장을 되돌려 고를 수 있다.
        /// </summary>
        public static bool TryCatch(IdleState state, IdleTuning tuning, out IdleSurgeKind caught)
        {
            caught = IdleSurgeKind.None;

            if (CanCatch(state) == false)
            {
                return false;
            }

            state.VisitorSecondsLeft = 0d;
            state.SinceVisitorSeconds = 0d;

            IdleRandom dice = new IdleRandom(state.RandomState);
            bool hand = dice.NextDouble() < tuning.HandFrenzyChance;
            state.RandomState = dice.State;

            caught = hand ? IdleSurgeKind.HandFrenzy : IdleSurgeKind.Frenzy;
            state.SurgeKind = (int)caught;
            state.SurgeSecondsLeft = tuning.SurgeSeconds;
            return true;
        }

        /// <summary>지금 판 전체에 걸린 배수 (폭주가 아니면 1).</summary>
        public static double Multiplier(IdleState state, IdleTuning tuning)
        {
            return (IdleSurgeKind)state.SurgeKind == IdleSurgeKind.Frenzy && state.SurgeSecondsLeft > 0d
                ? tuning.FrenzyMultiplier
                : 1d;
        }

        /// <summary>지금 손 때리기에 걸린 배수 (폭주가 아니면 1).</summary>
        public static double HandMultiplier(IdleState state, IdleTuning tuning)
        {
            return (IdleSurgeKind)state.SurgeKind == IdleSurgeKind.HandFrenzy && state.SurgeSecondsLeft > 0d
                ? tuning.HandFrenzyMultiplier
                : 1d;
        }

        public static string NameOf(IdleSurgeKind kind)
        {
            switch (kind)
            {
                case IdleSurgeKind.Frenzy: return "폭주";
                case IdleSurgeKind.HandFrenzy: return "손폭주";
                default: return "없음";
            }
        }
    }
}
