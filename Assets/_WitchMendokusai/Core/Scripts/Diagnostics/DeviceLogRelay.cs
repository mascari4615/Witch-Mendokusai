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
    public sealed class DeviceLogRelay : MonoBehaviour
    {
        private const string SETTINGS_RESOURCE = nameof(DeviceLogSettings);
        private const string TOKEN_RESOURCE = "DeviceLogToken";
        private const string TOKEN_ENV = "WM_DEVICE_LOG_TOKEN";
        private const string ENDPOINT_ENV = "WM_DEVICE_LOG_ENDPOINT";
        private const string SPOOL_FOLDER = "device-logs";
        private const string SECRET_HEADER = "X-Yawnbot-Secret";

        private static bool _installed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void Install()
        {
            if (_installed)
            {
                return;
            }

            DeviceLogSettings settings = Resources.Load<DeviceLogSettings>(SETTINGS_RESOURCE);
            if (settings == null || settings.Enabled == false)
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

            GameObject host = new GameObject(nameof(DeviceLogRelay));
            DontDestroyOnLoad(host);
            host.hideFlags = HideFlags.HideAndDontSave;
            DeviceLogRelay relay = host.AddComponent<DeviceLogRelay>();
            relay._settings = settings;
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

        private readonly List<DeviceLogEntry> _batch = new List<DeviceLogEntry>();

        private static string SpoolDirectory =>
            Path.Combine(UnityEngine.Application.persistentDataPath, SPOOL_FOLDER);

        private void Awake()
        {
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

            Debug.Log($"[DeviceLog] 릴레이 켜짐 — session={_session} endpoint={_endpoint} " +
                $"token={(string.IsNullOrEmpty(_token) ? "없음" : "있음")} spool={(_spool != null ? SpoolDirectory : "끔")}");

            StartCoroutine(FlushLoop());
            StartCoroutine(SendOrphanSpools());
        }

        private void OnDestroy()
        {
            UnityEngine.Application.logMessageReceivedThreaded -= OnLogThreaded;
            UnityEngine.Application.quitting -= OnQuitting;
        }

        /// <summary>세션 = 「언제 켠 실행인가」. 서버에서 파일명이 되므로 안전한 글자만.</summary>
        private static string BuildSessionId()
        {
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            string platform = UnityEngine.Application.platform.ToString().ToLowerInvariant();
            StringBuilder safe = new StringBuilder(platform.Length);
            foreach (char c in platform)
            {
                safe.Append(char.IsLetterOrDigit(c) ? c : '-');
            }
            return $"{safe}-{stamp}";
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

        private IEnumerator FlushLoop()
        {
            while (true)
            {
                float waited = 0f;
                while (waited < _settings.FlushIntervalSeconds && _flushRequested == false)
                {
                    waited += Time.unscaledDeltaTime;
                    yield return null;
                }
                _flushRequested = false;
                yield return StartCoroutine(FlushOnce());

                if (_consecutiveFailures >= _settings.BackoffAfterFailures)
                {
                    float backoff = Mathf.Min(
                        _settings.MaxBackoffSeconds,
                        _settings.FlushIntervalSeconds * _consecutiveFailures);
                    yield return new WaitForSecondsRealtime(backoff);
                }
            }
        }

        private IEnumerator FlushOnce()
        {
            if (_sending)
            {
                yield break;
            }
            if (_buffer.TryTakeBatch(_settings.MaxLinesPerBatch, _batch) == false)
            {
                yield break;
            }

            // 보내기 *전에* 기기에 적는다 — 이 순서가 뒤집히면 죽기 직전 줄이 사라진다.
            long spoolOffset = -1L;
            if (_spool != null)
            {
                try
                {
                    _spool.Append(_batch);
                    spoolOffset = _spool.CurrentLength;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[DeviceLog] 스풀 기록 실패: {ex.Message}");
                }
            }

            string payload = DeviceLogBuffer.BuildPayloadJson(
                _session, _deviceLabel, UnityEngine.Application.platform.ToString(),
                UnityEngine.Application.version, _buildLabel, _batch);
            List<DeviceLogEntry> inFlight = new List<DeviceLogEntry>(_batch);

            _sending = true;
            yield return SendPayload(payload, (bool ok) =>
            {
                _sending = false;
                if (ok)
                {
                    _consecutiveFailures = 0;
                    _sentLines += inFlight.Count;
                    if (_spool != null && spoolOffset >= 0)
                    {
                        try
                        {
                            _spool.MarkSentThrough(spoolOffset);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"[DeviceLog] 확인 지점 기록 실패: {ex.Message}");
                        }
                    }
                }
                else
                {
                    _consecutiveFailures++;
                    // 스풀이 살아 있으면 파일이 진실 — 메모리로 되돌리면 다음 실행 때 중복된다.
                    if (_spool == null)
                    {
                        _buffer.PushFront(inFlight);
                    }
                }
            });
        }

        private IEnumerator SendPayload(string payload, Action<bool> onDone)
        {
            using (UnityWebRequest request = new UnityWebRequest(_endpoint, UnityWebRequest.kHttpVerbPOST))
            {
                byte[] body = Encoding.UTF8.GetBytes(payload);
                request.uploadHandler = new UploadHandlerRaw(body);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                if (string.IsNullOrEmpty(_token) == false)
                {
                    request.SetRequestHeader(SECRET_HEADER, _token);
                }
                request.timeout = _settings.RequestTimeoutSeconds;

                yield return request.SendWebRequest();

                bool ok = request.result == UnityWebRequest.Result.Success
                    && request.responseCode >= 200 && request.responseCode < 300;
                if (ok == false && _consecutiveFailures == 0)
                {
                    // 첫 실패만 콘솔에 남긴다 — 실패마다 로그하면 그 로그가 또 버퍼로 들어간다.
                    Debug.LogWarning($"[DeviceLog] 전송 실패 ({request.responseCode} {request.error}) — 나중에 다시 시도한다.");
                }
                onDone?.Invoke(ok);
            }
        }

        /// <summary>지난 실행이 못 보낸 줄(=유언)을 부팅 직후 밀어 넣는다.</summary>
        private IEnumerator SendOrphanSpools()
        {
            if (_spool == null)
            {
                yield break;
            }
            List<string> orphans;
            try
            {
                orphans = DeviceLogSpool.FindOrphanSessions(SpoolDirectory, _session);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[DeviceLog] 지난 세션 조회 실패: {ex.Message}");
                yield break;
            }

            foreach (string orphan in orphans)
            {
                List<string> pending;
                try
                {
                    pending = DeviceLogSpool.ReadPendingLines(SpoolDirectory, orphan);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[DeviceLog] 지난 세션 {orphan} 읽기 실패: {ex.Message}");
                    continue;
                }
                if (pending.Count == 0)
                {
                    DeviceLogSpool.DeleteSession(SpoolDirectory, orphan);
                    continue;
                }

                Debug.Log($"[DeviceLog] 지난 실행에서 못 보낸 {pending.Count}줄 발견 — 다시 보낸다 ({orphan})");
                bool allSent = true;
                for (int start = 0; start < pending.Count; start += _settings.MaxLinesPerBatch)
                {
                    int count = Math.Min(_settings.MaxLinesPerBatch, pending.Count - start);
                    List<string> slice = pending.GetRange(start, count);
                    string payload = DeviceLogBuffer.BuildPayloadJsonFromRawLines(
                        orphan, _deviceLabel, UnityEngine.Application.platform.ToString(),
                        UnityEngine.Application.version, _buildLabel, slice);

                    bool ok = false;
                    yield return SendPayload(payload, (bool result) => ok = result);
                    if (ok == false)
                    {
                        allSent = false;
                        break;
                    }
                }
                if (allSent)
                {
                    DeviceLogSpool.DeleteSession(SpoolDirectory, orphan);
                }
            }
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
