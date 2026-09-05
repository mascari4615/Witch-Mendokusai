using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace WitchMendokusai
{
    /// <summary>
    /// TASK-WM-201 — 폰에서 뜬 로그를 스스로 서버로 밀어 넣는다.
    ///
    /// 흐름: `logMessageReceivedThreaded`(아무 스레드) → 버퍼 → (기기 파일에 먼저 적고)
    /// → 주기/임계/에러 시 POST → 서버가 200 이면 확인 지점 기록. 앱이 죽어 못 보낸 줄은
    /// 다음 실행 부팅 때 스풀에서 발견해 먼저 밀어 넣는다.
    ///
    /// 설치 조건 = `Resources/DeviceLogSettings` 가 있고 Enabled. 에셋이 없거나 꺼져 있으면
    /// **아무것도 안 한다**(게임 흐름 0 영향). 에디터는 기본 비활성 — 콘솔이 이미 정본이다.
    ///
    /// 보는 곳: `https://yawnbot.mascari4615.com/device-log?t=<토큰>` (웹) /
    /// `GET /device-log/tail` (AI) / 에러는 디스코드 채널.
    /// </summary>
    public sealed partial class DeviceLogRelay : MonoBehaviour
    {
        private const string SETTINGS_RESOURCE = nameof(DeviceLogSettings);
        private const string TOKEN_RESOURCE = "DeviceLogToken";
        private const string TOKEN_ENV = "WM_DEVICE_LOG_TOKEN";
        private const string ENDPOINT_ENV = "WM_DEVICE_LOG_ENDPOINT";
        private const string SPOOL_FOLDER = "device-logs";
        private const string SECRET_HEADER = "X-Yawnbot-Secret";

        private static bool _installed;
        private static DeviceLogRelay _live;

        /// <summary>
        /// 화면 표시기가 묻는 「로그가 지금 나가고 있나」. 릴레이가 안 켜졌으면 그렇다고 답한다 —
        /// 이 장치의 유일한 무음 지점이라, 모르는 채로 두지 않는다.
        /// </summary>
        public static string StatusLine()
        {
            if (_live == null || _live._buffer == null)
            {
                return DeviceLogStatus.OffLine();
            }
            return DeviceLogStatus.Line(
                _live._sentLines,
                _live._buffer.Count,
                _live._consecutiveFailures,
                _live._lastResponseCode);
        }

        // ★ 방치형 빌드에서는 <b>스스로 뜨지 않는다</b> (표식 `WM_IDLE`, TASK-WM-406).
        //   본편 진단 장치는 씬에 아무것도 안 놔도 어디서나 뜬다. 그대로 팔면
        //   산 사람의 기계에서 남의 서버를 부르고(실측 2026-08-16: `[DeviceLog] 401`)
        //   화면에 개발용 표시가 뜬다. 본편에는 그대로 필요하므로 <b>지우지 않고</b>,
        //   「스스로 뜨게 하는 표지」만 뗀다 — 몸통은 그대로라 부르면 여전히 돈다.
#if !WM_IDLE
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
#endif
        private static void Install()
        {
            if (_installed)
            {
                return;
            }

            DeviceLogSettings settings = Resources.Load<DeviceLogSettings>(SETTINGS_RESOURCE);
            if (settings == null)
            {
                // 에셋을 못 읽어도 *조용히 죽지 않는다* — 진단 장치가 진단 불가로 사라지면
                // 폰에서 무슨 일이 있었는지 영영 알 수 없다(2026-08-06 실기 실측: 로그도
                // 표시기도 안 떴고 이유를 물을 방법조차 없었다). 기본값으로 켠다.
                settings = ScriptableObject.CreateInstance<DeviceLogSettings>();
                Debug.LogWarning("[DeviceLog] 설정 에셋을 못 읽었다 — 기본값으로 켠다");
            }
            if (settings.Enabled == false)
            {
                return;
            }
#if UNITY_EDITOR
            if (settings.EnabledInEditor == false)
            {
                return;
            }
#endif
            _installed = true;

            // ★ 비활성 상태로 만들어서 붙인다 — 활성 GameObject 에 AddComponent 하면 그 자리에서
            //   Awake 가 돌아 *설정을 넣기 전에* 터진다(2026-08-06 실기: 폰에서 로그도 표시기도
            //   없었던 진짜 이유. NRE 가 나도 아무도 못 보니 무음이었다).
            GameObject host = new GameObject(nameof(DeviceLogRelay));
            host.SetActive(false);
            DontDestroyOnLoad(host);
            host.hideFlags = HideFlags.HideAndDontSave;
            DeviceLogRelay relay = host.AddComponent<DeviceLogRelay>();
            relay._settings = settings;
            host.SetActive(true);
        }

        private DeviceLogSettings _settings;
        private DeviceLogBuffer _buffer;
        private DeviceLogSpool _spool;
        private string _session;
        private string _endpoint;
        private string _token;
        private string _deviceLabel;
        private string _buildLabel;
        private bool _flushRequested;
        private bool _sending;
        private int _consecutiveFailures;
        private int _sentLines;
        private long _lastResponseCode;

        private readonly List<DeviceLogEntry> _batch = new List<DeviceLogEntry>();

        private static string SpoolDirectory =>
            Path.Combine(UnityEngine.Application.persistentDataPath, SPOOL_FOLDER);

        private void Awake()
        {
            _live = this;
            _session = BuildSessionId();
            _endpoint = ResolveEndpoint();
            _token = ResolveToken();
            _deviceLabel = $"{SystemInfo.deviceModel} / {SystemInfo.operatingSystem}";
            // 화면 구석에 뜨는 글자와 *같은 정본*을 쓴다 — 두 곳이 각자 조립하면 하나만 바뀌어도
            // 「화면엔 이 빌드인데 로그엔 저 빌드」가 되어 추적이 끊긴다 (TASK-WM-201).
            _buildLabel = BuildInfo.Current.ShortLabel();
            _buffer = new DeviceLogBuffer(_settings.BufferCapacity);

            if (_settings.SpoolToDisk)
            {
                try
                {
                    _spool = new DeviceLogSpool(SpoolDirectory, _session, _settings.SpoolMaxBytes);
                    DeviceLogSpool.TrimOldSessions(SpoolDirectory, _session, _settings.SpoolKeepSessions);
                }
                catch (Exception ex)
                {
                    // 스풀이 못 서도 실시간 전송은 살린다 (부분 기능 > 전면 중단).
                    _spool = null;
                    Debug.LogWarning($"[DeviceLog] 기기 스풀 준비 실패 — 실시간 전송만 진행: {ex.Message}");
                }
            }

            UnityEngine.Application.logMessageReceivedThreaded += OnLogThreaded;
            UnityEngine.Application.quitting += OnQuitting;
            // 폰이 메모리를 회수하겠다고 알리는 순간 = 앱이 곧 죽을 수 있다는 유일한 예고.
            // 그 뒤엔 종료 콜백 없이 사라지는 게 보통이라, 여기서 남은 줄을 디스크에 박는다.
            UnityEngine.Application.lowMemory += OnLowMemory;

            Debug.Log($"[DeviceLog] 릴레이 켜짐 — session={_session} endpoint={_endpoint} " +
                $"token={(string.IsNullOrEmpty(_token) ? "없음" : "있음")} spool={(_spool != null ? SpoolDirectory : "끔")}");

            StartCoroutine(FlushLoop());
            StartCoroutine(SendOrphanSpools());
        }

        private void OnDestroy()
        {
            UnityEngine.Application.logMessageReceivedThreaded -= OnLogThreaded;
            UnityEngine.Application.quitting -= OnQuitting;
            UnityEngine.Application.lowMemory -= OnLowMemory;
        }

        /// <summary>세션 = 「언제 켠 실행인가」. 서버에서 파일명이 되므로 안전한 글자만.</summary>
        private static string BuildSessionId()
        {
            return ComposeSessionId(
                UnityEngine.Application.platform.ToString(),
                BuildInfo.Current.buildNumber,
                DateTime.Now);
        }

        /// <summary>
        /// 세션 이름에 빌드 번호를 넣는다 — 목록에서 *이름만 보고* 어느 빌드의 실행인지 갈리게.
        /// (손으로 구운 빌드는 번호가 없으므로 뺀다.) 파일명이 되므로 글자·숫자·붙임표만 남긴다.
        /// </summary>
        public static string ComposeSessionId(string platform, int buildNumber, DateTime now)
        {
            StringBuilder safe = new StringBuilder(platform == null ? 0 : platform.Length);
            if (platform != null)
            {
                foreach (char c in platform.ToLowerInvariant())
                {
                    safe.Append(char.IsLetterOrDigit(c) ? c : '-');
                }
            }
            if (safe.Length == 0)
            {
                safe.Append("device");
            }
            string stamp = now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            string build = buildNumber > 0
                ? "-b" + buildNumber.ToString(CultureInfo.InvariantCulture)
                : string.Empty;
            return $"{safe}{build}-{stamp}";
        }

        private string ResolveEndpoint()
        {
            string fromEnv = SafeEnv(ENDPOINT_ENV);
            return string.IsNullOrEmpty(fromEnv) ? _settings.Endpoint : fromEnv;
        }

        /// <summary>토큰: 환경변수(에디터·PC) → Resources 텍스트(빌드 시 생성, gitignore).</summary>
        private static string ResolveToken()
        {
            string fromEnv = SafeEnv(TOKEN_ENV);
            if (string.IsNullOrEmpty(fromEnv) == false)
            {
                return fromEnv;
            }
            TextAsset asset = Resources.Load<TextAsset>(TOKEN_RESOURCE);
            return asset != null ? asset.text.Trim() : string.Empty;
        }

        private static string SafeEnv(string name)
        {
            try
            {
                return Environment.GetEnvironmentVariable(name)?.Trim() ?? string.Empty;
            }
            catch (Exception)
            {
                // 일부 플랫폼은 환경변수 접근 자체가 막혀 있다.
                return string.Empty;
            }
        }

        /// <summary>어느 스레드에서든 불린다 — 여기서 하는 일은 버퍼에 넣는 것뿐.</summary>
        private void OnLogThreaded(string condition, string stackTrace, LogType type)
        {
            string level = LevelOf(type);
            bool isError = level == DeviceLogEntry.LEVEL_ERROR
                || level == DeviceLogEntry.LEVEL_EXCEPTION
                || level == DeviceLogEntry.LEVEL_ASSERT;

            if (isError == false && level == DeviceLogEntry.LEVEL_LOG && _settings.IncludeInfoLogs == false)
            {
                return;
            }
            // 자기 로그를 자기가 다시 보내는 되먹임 차단.
            if (condition != null && condition.StartsWith("[DeviceLog]", StringComparison.Ordinal))
            {
                return;
            }

            bool withStack = isError || (_settings.IncludeStackForWarnings && level == DeviceLogEntry.LEVEL_WARNING);
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            _buffer.Add(new DeviceLogEntry(now, level, condition, withStack ? stackTrace : string.Empty));

            if (isError && _settings.FlushImmediatelyOnError)
            {
                _flushRequested = true;
            }
            else if (_buffer.Count >= _settings.ImmediateFlushThreshold)
            {
                _flushRequested = true;
            }
        }

        private static string LevelOf(LogType type)
        {
            switch (type)
            {
                case LogType.Error:
                    return DeviceLogEntry.LEVEL_ERROR;
                case LogType.Exception:
                    return DeviceLogEntry.LEVEL_EXCEPTION;
                case LogType.Assert:
                    return DeviceLogEntry.LEVEL_ASSERT;
                case LogType.Warning:
                    return DeviceLogEntry.LEVEL_WARNING;
                default:
                    return DeviceLogEntry.LEVEL_LOG;
            }
        }

        /// <summary>기기가 메모리를 조인다 = 곧 죽을 수 있다. 남은 줄을 즉시 디스크로.</summary>
        private void OnLowMemory()
        {
            FlushToSpool();
        }

        /// <summary>
        /// 폰에서 가장 흔한 이별 경로 — 홈 버튼·전화·화면 끔. 안드로이드는 이때 프로세스를
        /// 조용히 죽여도 되므로 `quitting` 이 영영 안 올 수 있다. 그래서 내려가는 순간
        /// 남은 줄을 *디스크에 먼저* 박고, 살아있는 동안만 전송을 시도한다.
        /// </summary>
        private void OnApplicationPause(bool paused)
        {
            if (paused == false || _buffer == null)
            {
                return;
            }
            FlushToSpool();
        }

        /// <summary>정상 종료 경로 — 남은 줄을 동기로 한 번 더 밀어 넣는다(코루틴은 이미 못 돈다).</summary>
        private void OnQuitting()
        {
            if (_buffer == null)
            {
                return;
            }
            List<DeviceLogEntry> remaining = new List<DeviceLogEntry>();
            if (_buffer.TryTakeBatch(_settings.MaxLinesPerBatch, remaining) == false)
            {
                return;
            }
            if (_spool != null)
            {
                try
                {
                    // 종료 중엔 네트워크를 기다릴 수 없다 — 파일에만 남기고 다음 실행이 보낸다.
                    _spool.Append(remaining);
                }
                catch (Exception)
                {
                    // 종료 경로에서 더 할 수 있는 일이 없다.
                }
            }
        }
    }
}
