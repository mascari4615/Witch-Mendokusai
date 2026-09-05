using System;

namespace WitchMendokusai
{
    /// <summary>
    /// TASK-WM-201 — 기기에서 잡은 로그 한 줄. Unity 타입 의존 0 (EditMode 순수 검증 대상).
    ///
    /// 시각은 기기 시계 epoch(ms) 그대로 둔다 — 서버가 수신 시각으로 덮으면 「폰에서 언제
    /// 났는가」가 사라져서 순서 추적이 불가능해진다.
    /// </summary>
    public readonly struct DeviceLogEntry
    {
        public const string LEVEL_LOG = "log";
        public const string LEVEL_WARNING = "warning";
        public const string LEVEL_ERROR = "error";
        public const string LEVEL_EXCEPTION = "exception";
        public const string LEVEL_ASSERT = "assert";

        public readonly long TimestampMs;
        public readonly string Level;
        public readonly string Message;
        public readonly string StackTrace;

        public DeviceLogEntry(long timestampMs, string level, string message, string stackTrace)
        {
            TimestampMs = timestampMs;
            Level = string.IsNullOrEmpty(level) ? LEVEL_LOG : level;
            Message = message ?? string.Empty;
            StackTrace = stackTrace ?? string.Empty;
        }

        /// <summary>에러급 = 알릴 가치가 있는 줄. 버퍼가 넘칠 때 마지막까지 지키는 대상.</summary>
        public bool IsError =>
            string.Equals(Level, LEVEL_ERROR, StringComparison.Ordinal)
            || string.Equals(Level, LEVEL_EXCEPTION, StringComparison.Ordinal)
            || string.Equals(Level, LEVEL_ASSERT, StringComparison.Ordinal);
    }
}
