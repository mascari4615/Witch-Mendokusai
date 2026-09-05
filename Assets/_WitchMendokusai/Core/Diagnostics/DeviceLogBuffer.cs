using System;
using System.Collections.Generic;
using System.Text;

namespace WitchMendokusai
{
    /// <summary>
    /// TASK-WM-201 — 기기 로그 줄 버퍼 + 전송 페이로드 직렬화. Unity 의존 0 (EditMode 검증).
    ///
    /// 로그 콜백은 *아무 스레드*에서 온다(logMessageReceivedThreaded) → Add 는 lock.
    /// 소비(TryTakeBatch)는 메인 스레드 코루틴 하나뿐.
    ///
    /// 넘칠 때 버리는 정책 = **에러급을 마지막까지 지킨다.** 상한에 닿으면 가장 오래된
    /// *비에러* 줄부터 버리고, 전부 에러면 그때 가장 오래된 것을 버린다. 그냥 링버퍼면
    /// 폰이 초당 수백 줄을 뱉는 순간 정작 원인인 예외가 밀려 나간다.
    /// </summary>
    public sealed class DeviceLogBuffer
    {
        private readonly object _gate = new object();
        private readonly LinkedList<DeviceLogEntry> _entries = new LinkedList<DeviceLogEntry>();
        private readonly int _capacity;
        private int _droppedTotal;

        public DeviceLogBuffer(int capacity)
        {
            _capacity = capacity > 0 ? capacity : 1;
        }

        public int Count
        {
            get
            {
                lock (_gate)
                {
                    return _entries.Count;
                }
            }
        }

        /// <summary>상한 때문에 버려진 누적 줄 수 — 서버로 함께 보고해서 침묵과 구분한다.</summary>
        public int DroppedTotal
        {
            get
            {
                lock (_gate)
                {
                    return _droppedTotal;
                }
            }
        }

        public void Add(DeviceLogEntry entry)
        {
            lock (_gate)
            {
                if (_entries.Count >= _capacity)
                {
                    EvictOne();
                }
                _entries.AddLast(entry);
            }
        }

        /// <summary>가장 오래된 비에러 줄 → 없으면 가장 오래된 줄. 호출부가 이미 lock 안.</summary>
        private void EvictOne()
        {
            LinkedListNode<DeviceLogEntry> node = _entries.First;
            while (node != null)
            {
                if (node.Value.IsError == false)
                {
                    _entries.Remove(node);
                    _droppedTotal++;
                    return;
                }
                node = node.Next;
            }

            if (_entries.First != null)
            {
                _entries.RemoveFirst();
                _droppedTotal++;
            }
        }

        /// <summary>
        /// 앞에서부터 최대 maxLines 줄을 꺼내 into 에 담는다(FIFO). 꺼낸 줄은 버퍼에서 사라지므로
        /// 전송 실패 시 되돌릴 책임은 호출부 — 스풀 파일이 그 역할(크래시 유실 방지)을 겸한다.
        /// </summary>
        public bool TryTakeBatch(int maxLines, List<DeviceLogEntry> into)
        {
            if (into == null)
            {
                throw new ArgumentNullException(nameof(into));
            }
            into.Clear();
            lock (_gate)
            {
                while (into.Count < maxLines && _entries.First != null)
                {
                    into.Add(_entries.First.Value);
                    _entries.RemoveFirst();
                }
            }
            return into.Count > 0;
        }

        /// <summary>전송 실패한 배치를 앞쪽(더 오래된 자리)으로 되돌린다.</summary>
        public void PushFront(List<DeviceLogEntry> batch)
        {
            if (batch == null || batch.Count == 0)
            {
                return;
            }
            lock (_gate)
            {
                for (int i = batch.Count - 1; i >= 0; i--)
                {
                    if (_entries.Count >= _capacity)
                    {
                        EvictOne();
                    }
                    _entries.AddFirst(batch[i]);
                }
            }
        }

        /// <summary>서버 `POST /device-log` 본문. JsonUtility 는 중첩 배열·이스케이프가 약해 직접 만든다.</summary>
        public static string BuildPayloadJson(
            string session,
            string device,
            string platform,
            string appVersion,
            string build,
            IReadOnlyList<DeviceLogEntry> lines)
        {
            StringBuilder builder = new StringBuilder(1024);
            builder.Append('{');
            AppendField(builder, "session", session, true);
            AppendField(builder, "device", device, true);
            AppendField(builder, "platform", platform, true);
            AppendField(builder, "appVersion", appVersion, true);
            AppendField(builder, "build", build, true);
            builder.Append("\"lines\":[");
            if (lines != null)
            {
                for (int i = 0; i < lines.Count; i++)
                {
                    if (i > 0)
                    {
                        builder.Append(',');
                    }
                    AppendEntryJson(builder, lines[i]);
                }
            }
            builder.Append("]}");
            return builder.ToString();
        }

        /// <summary>
        /// 스풀에서 읽어온 *이미 JSON 인* 줄들로 페이로드를 만든다 (지난 실행 유실분 재전송).
        /// 다시 파싱했다가 다시 직렬화하면 그 과정에서 깨질 여지만 생긴다 — 원문을 그대로 싣는다.
        /// </summary>
        public static string BuildPayloadJsonFromRawLines(
            string session,
            string device,
            string platform,
            string appVersion,
            string build,
            IReadOnlyList<string> rawLines)
        {
            StringBuilder builder = new StringBuilder(1024);
            builder.Append('{');
            AppendField(builder, "session", session, true);
            AppendField(builder, "device", device, true);
            AppendField(builder, "platform", platform, true);
            AppendField(builder, "appVersion", appVersion, true);
            AppendField(builder, "build", build, true);
            builder.Append("\"lines\":[");
            if (rawLines != null)
            {
                for (int i = 0; i < rawLines.Count; i++)
                {
                    if (i > 0)
                    {
                        builder.Append(',');
                    }
                    builder.Append(rawLines[i]);
                }
            }
            builder.Append("]}");
            return builder.ToString();
        }

        /// <summary>스풀 파일 한 줄(NDJSON) — 페이로드 안 줄과 같은 모양이라 파일→전송이 무변환.</summary>
        public static string BuildEntryJson(DeviceLogEntry entry)
        {
            StringBuilder builder = new StringBuilder(256);
            AppendEntryJson(builder, entry);
            return builder.ToString();
        }

        private static void AppendEntryJson(StringBuilder builder, DeviceLogEntry entry)
        {
            builder.Append("{\"t\":");
            builder.Append(entry.TimestampMs.ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.Append(",\"level\":\"");
            AppendEscaped(builder, entry.Level);
            builder.Append("\",\"msg\":\"");
            AppendEscaped(builder, entry.Message);
            if (string.IsNullOrEmpty(entry.StackTrace) == false)
            {
                builder.Append("\",\"stack\":\"");
                AppendEscaped(builder, entry.StackTrace);
            }
            builder.Append("\"}");
        }

        private static void AppendField(StringBuilder builder, string name, string value, bool trailingComma)
        {
            builder.Append('"');
            builder.Append(name);
            builder.Append("\":\"");
            AppendEscaped(builder, value ?? string.Empty);
            builder.Append('"');
            if (trailingComma)
            {
                builder.Append(',');
            }
        }

        private static void AppendEscaped(StringBuilder builder, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                switch (c)
                {
                    case '"':
                        builder.Append("\\\"");
                        break;
                    case '\\':
                        builder.Append("\\\\");
                        break;
                    case '\n':
                        builder.Append("\\n");
                        break;
                    case '\r':
                        builder.Append("\\r");
                        break;
                    case '\t':
                        builder.Append("\\t");
                        break;
                    default:
                        if (c < 0x20)
                        {
                            builder.Append("\\u");
                            builder.Append(((int)c).ToString("x4", System.Globalization.CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            builder.Append(c);
                        }
                        break;
                }
            }
        }
    }
}
