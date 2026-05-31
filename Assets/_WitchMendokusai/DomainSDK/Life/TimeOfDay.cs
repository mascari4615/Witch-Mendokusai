namespace WitchMendokusai.DomainSDK.Life
{
    /// <summary>
    /// TASK-WM-168 INC-2 — 자율 활동 선택의 시간 맥락. 순수 enum (DomainSDK).
    /// 급한 욕구가 없을 때 기본 활동을 가른다(밤=자기). INC-5 바인딩에서 WorldClock 의 시각이 여기로 매핑.
    /// </summary>
    public enum TimeOfDay
    {
        Morning = 0,
        Afternoon = 1,
        Evening = 2,
        Night = 3,
    }
}
