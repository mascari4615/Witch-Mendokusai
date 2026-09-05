using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace WitchMendokusai
{
    /// <summary>
    /// TASK-WM-201 — 아직 서버가 받았다고 확인해주지 않은 줄을 기기 디스크에 남겨두는 스풀.
    /// Unity 의존 0 (경로만 주입 — EditMode 에서 임시 폴더로 검증).
    ///
    /// 근본: 폰에서 제일 알고 싶은 줄은 *앱이 죽기 직전 줄*인데, 그 줄은 네트워크로 나가기
    /// 전에 프로세스가 사라진다. 그래서 **디스크에 먼저 적고 → 보내고 → 확인된 지점(offset)만
    /// 기록**한다. 다음 실행 때 offset 뒤쪽이 남아 있으면 그게 지난번에 못 보낸 유언이다.
    ///
    /// 파일: <dir>/<session>.ndjson (줄) + <dir>/<session>.offset (확인된 바이트 수).
    /// </summary>
    public sealed class DeviceLogSpool
    {
        private const string LOG_EXTENSION = ".ndjson";
        private const string OFFSET_EXTENSION = ".offset";

        private readonly string _directory;
        private readonly string _session;
        private readonly long _maxBytes;

        public DeviceLogSpool(string directory, string session, long maxBytes)
        {
            _directory = directory;
            _session = session;
            _maxBytes = maxBytes > 0 ? maxBytes : long.MaxValue;
            Directory.CreateDirectory(_directory);
        }

        public string LogPath => Path.Combine(_directory, _session + LOG_EXTENSION);

        public string OffsetPath => Path.Combine(_directory, _session + OFFSET_EXTENSION);

        /// <summary>현재 스풀 파일 길이(byte). 전송 성공 시 이 값을 확인 지점으로 기록한다.</summary>
        public long CurrentLength => File.Exists(LogPath) ? new FileInfo(LogPath).Length : 0L;

        /// <summary>
        /// 줄을 스풀에 적는다. 상한을 넘으면 *적지 않고* false — 디스크를 채워
        /// 게임 저장까지 망가뜨리는 쪽이 로그를 잃는 쪽보다 나쁘다.
        /// </summary>
        public bool Append(IReadOnlyList<DeviceLogEntry> entries)
        {
            if (entries == null || entries.Count == 0)
            {
                return true;
            }
            if (CurrentLength >= _maxBytes)
            {
                return false;
            }

            StringBuilder builder = new StringBuilder(entries.Count * 128);
            for (int i = 0; i < entries.Count; i++)
            {
                builder.Append(DeviceLogBuffer.BuildEntryJson(entries[i]));
                builder.Append('\n');
            }
            File.AppendAllText(LogPath, builder.ToString(), Encoding.UTF8);
            return true;
        }

        /// <summary>서버가 받았음이 확인된 바이트 지점 기록. 여기까지는 다시 안 보낸다.</summary>
        public void MarkSentThrough(long byteOffset)
        {
            File.WriteAllText(OffsetPath, byteOffset.ToString(System.Globalization.CultureInfo.InvariantCulture), Encoding.UTF8);
        }

        public long ReadSentOffset()
        {
            return ReadSentOffset(_directory, _session);
        }

        private static long ReadSentOffset(string directory, string session)
        {
            string path = Path.Combine(directory, session + OFFSET_EXTENSION);
            if (File.Exists(path) == false)
            {
                return 0L;
            }
            string raw = File.ReadAllText(path, Encoding.UTF8).Trim();
            return long.TryParse(raw, System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out long value) && value >= 0
                ? value
                : 0L;
        }

        /// <summary>확인 지점 뒤의 줄들 = 아직 못 보낸 것. 원본 JSON 문자열 그대로(무변환 재전송).</summary>
        public static List<string> ReadPendingLines(string directory, string session)
        {
            List<string> pending = new List<string>();
            string path = Path.Combine(directory, session + LOG_EXTENSION);
            if (File.Exists(path) == false)
            {
                return pending;
            }
            long offset = ReadSentOffset(directory, session);

            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                if (offset > 0 && offset < stream.Length)
                {
                    stream.Seek(offset, SeekOrigin.Begin);
                }
                else if (offset >= stream.Length)
                {
                    return pending;
                }

                using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        string trimmed = line.Trim();
                        // 크래시로 잘린 마지막 줄이 있을 수 있다 — 닫히지 않은 줄은 버린다.
                        if (trimmed.Length > 0 && trimmed[0] == '{' && trimmed[trimmed.Length - 1] == '}')
                        {
                            pending.Add(trimmed);
                        }
                    }
                }
            }
            return pending;
        }

        /// <summary>지난 실행이 남긴 세션들 (현재 세션 제외) — 최근 것부터.</summary>
        public static List<string> FindOrphanSessions(string directory, string currentSession)
        {
            List<string> sessions = new List<string>();
            if (Directory.Exists(directory) == false)
            {
                return sessions;
            }
            string[] files = Directory.GetFiles(directory, "*" + LOG_EXTENSION);
            Array.Sort(files, (a, b) => File.GetLastWriteTimeUtc(b).CompareTo(File.GetLastWriteTimeUtc(a)));
            foreach (string file in files)
            {
                string session = Path.GetFileNameWithoutExtension(file);
                if (string.Equals(session, currentSession, StringComparison.Ordinal) == false)
                {
                    sessions.Add(session);
                }
            }
            return sessions;
        }

        public static void DeleteSession(string directory, string session)
        {
            string log = Path.Combine(directory, session + LOG_EXTENSION);
            string offset = Path.Combine(directory, session + OFFSET_EXTENSION);
            if (File.Exists(log))
            {
                File.Delete(log);
            }
            if (File.Exists(offset))
            {
                File.Delete(offset);
            }
        }

        /// <summary>보관 세션 수 상한 — 기기에 무한히 쌓이지 않게(최근 것부터 남긴다).</summary>
        public static void TrimOldSessions(string directory, string currentSession, int keepCount)
        {
            List<string> orphans = FindOrphanSessions(directory, currentSession);
            for (int i = keepCount; i < orphans.Count; i++)
            {
                DeleteSession(directory, orphans[i]);
            }
        }
    }
}
