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
    // DeviceLogRelay 의 보내기와 밀린 것 부분. 같은 클래스의 partial 조각이다. 상태(필드)는 DeviceLogRelay.cs 를 본다.
    public sealed partial class DeviceLogRelay : MonoBehaviour
    {
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

        /// <summary>
        /// 서버가 「너 누구냐」(401/403) 로 막으면 재시도는 의미가 없다 — 토큰은 앱 안에 박혀 있어
        /// 실행 중엔 안 바뀐다. 그런데도 계속 두드리면 배터리·데이터만 태운다. 그래서 길게 쉰다.
        /// 완전 포기는 안 한다: 서버 쪽 토큰이 갱신되면 다음 창에서 저절로 복구되어야 한다.
        /// 화면 표시기가 그 사이 「막힘 401 토큰 불일치」로 이유를 말해준다.
        /// </summary>
        private const float AUTH_BLOCKED_RETRY_SECONDS = 300f;

        private float _authBlockedUntil;
        private bool _serverFull;

        private IEnumerator FlushOnce()
        {
            if (_sending)
            {
                yield break;
            }
            if (_serverFull)
            {
                // 서버가 이 세션 파일은 꽉 찼다고 답했다 — 더 보내도 안 받는다. 스풀만 쌓는다.
                yield break;
            }
            if (Time.realtimeSinceStartup < _authBlockedUntil)
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
                // 화면 표시기가 「막힘 401 토큰 불일치」처럼 *이유까지* 말할 수 있게 남긴다.
                _lastResponseCode = ok ? 0 : request.responseCode;

                if (ok == false && (request.responseCode == 401 || request.responseCode == 403))
                {
                    _authBlockedUntil = Time.realtimeSinceStartup + AUTH_BLOCKED_RETRY_SECONDS;
                }
                else if (ok && request.downloadHandler != null
                    && request.downloadHandler.text != null
                    && request.downloadHandler.text.Contains("\"stop\":true"))
                {
                    // 서버가 「이 세션은 그만」이라 답했다 (파일 상한). 줄은 스풀에 계속 쌓인다.
                    _serverFull = true;
                    Debug.LogWarning("[DeviceLog] 서버가 이 세션 로그를 그만 받는다 — 기기 파일에만 쌓는다.");
                }
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

        /// <summary>남은 줄을 디스크로 옮긴다(네트워크 대기 X). 내려감·메모리 압박 공용 경로.</summary>
        private void FlushToSpool()
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
                    _spool.Append(remaining);
                }
                catch (Exception)
                {
                    // 여기서 더 할 수 있는 일이 없다 — 다음 실행이 스풀을 본다.
                }
            }
            else
            {
                // 스풀이 없으면 메모리가 유일한 사본 — 되돌려 두고 깨어나면 보낸다.
                _buffer.PushFront(remaining);
            }
        }
    }
}
