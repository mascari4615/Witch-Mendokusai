namespace WitchMendokusai.DomainSDK.Idle
{
    /// <summary>
    /// 상점 무료 상자. 하루 1회, 뽑기 재화 소량 (사용자 2026-09-05. economy.md 표 2)
    ///
    /// ★ 날 경계는 입장권과 같은 자리 (<see cref="IdleDungeons.DayIndexOf"/>, <see cref="IdleTuning.DayResetOffsetSeconds"/>).
    ///   두 시계가 갈리면 사람이 하루를 두 번 세는 꼴
    /// ★ 무작위 없음. 양은 튜닝이 정한 상수. 여는 재미는 여기가 아니라 뽑기 쪽
    /// </summary>
    public static class IdleFreeBox
    {
        private const long SECONDS_PER_DAY = 86400L;

        /// <summary>오늘 아직 안 열었나</summary>
        public static bool IsReady(IdleState state, IdleTuning tuning, long nowUnixSeconds)
        {
            return state.FreeBoxDay != IdleDungeons.DayIndexOf(nowUnixSeconds, tuning.DayResetOffsetSeconds);
        }

        /// <summary>연다. 오늘 이미 열었으면 아무 일도 안 일어남</summary>
        public static bool TryOpen(IdleState state, IdleTuning tuning, long nowUnixSeconds, out long stones)
        {
            stones = 0L;

            if (IsReady(state, tuning, nowUnixSeconds) == false)
            {
                return false;
            }

            state.FreeBoxDay = IdleDungeons.DayIndexOf(nowUnixSeconds, tuning.DayResetOffsetSeconds);
            stones = tuning.FreeBoxStones > 0L ? tuning.FreeBoxStones : 0L;
            state.Stones += stones;
            return true;
        }

        /// <summary>다음 상자까지 남은 초. 열 수 있으면 0</summary>
        public static double SecondsLeft(IdleState state, IdleTuning tuning, long nowUnixSeconds)
        {
            long today = IdleDungeons.DayIndexOf(nowUnixSeconds, tuning.DayResetOffsetSeconds);

            if (state.FreeBoxDay != today)
            {
                return 0d;
            }

            long nextBoundary = (today + 1L) * SECONDS_PER_DAY + tuning.DayResetOffsetSeconds;
            return nextBoundary - nowUnixSeconds;
        }
    }
}
