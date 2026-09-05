namespace WitchMendokusai.DomainSDK.Life
{
    /// <summary>
    /// TASK-WM-168 INC-5c — 게임 시각(시:0~23)을 <see cref="TimeOfDay"/> 구간으로 접는 순수 함수 (DomainSDK).
    /// WorldClock.Hour → LifeAgent.SetTimeOfDay 연결의 변환 코어. ActivitySelector(INC-2)가 쓰는 시간대의 출처.
    /// 24시간 기준 명시 구간 — 아침(5~10)·낮(11~16)·저녁(17~21)·밤(22~4). 음수/24+ hour 도 정규화.
    /// </summary>
    public static class TimeOfDayMap
    {
        private const int HOURS_PER_DAY = 24;

        public static TimeOfDay FromHour(int hour)
        {
            int normalized = ((hour % HOURS_PER_DAY) + HOURS_PER_DAY) % HOURS_PER_DAY;

            if (normalized >= 5 && normalized < 11)
            {
                return TimeOfDay.Morning;
            }

            if (normalized >= 11 && normalized < 17)
            {
                return TimeOfDay.Afternoon;
            }

            if (normalized >= 17 && normalized < 22)
            {
                return TimeOfDay.Evening;
            }

            return TimeOfDay.Night;
        }
    }
}
