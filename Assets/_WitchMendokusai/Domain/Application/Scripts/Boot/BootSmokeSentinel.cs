using System;
using System.Collections;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace WitchMendokusai
{
    /// <summary>
    /// TASK-WM-118 I5 — 결정 부팅 standalone smoke 의 *런타임측 liveness*.
    ///
    /// 이전 스모크(wm-playmode-smoke.ps1 / PlayMode 러너)가 비-viable 였던 근본 =
    /// "게임 생명주기 ↔ 테스트 프레임워크 불일치 → 게임이 무한 실행, 테스트가
    /// 결론 못 냄". 본 센티넬이 그 근본을 해소: *결정 부팅이 끝나면(WorldReady)
    /// 결정적으로 프로세스를 종료*시킨다. 판정(도달 여부 && NRE 0)은 runner 가
    /// 결과파일/로그로 — 센티넬은 종료·계측만(관심사 분리).
    ///
    /// 비결정(일반 수동 플레이 / 에디터 토글 OFF)엔 *완전 inert* — 설치 자체를
    /// 안 함. 결정 모드(WM_BOOT_DETERMINISTIC 또는 BootModeEditorOverride)만:
    ///  · logMessageReceived 로 NullReferenceException 카운트(부팅 전 구간)
    ///  · BootObserver.OnBootComplete → settle N프레임 → 결과파일 PASS → Quit(0)
    ///  · timeout(기본 120s) → 결과파일 FAIL(reason) → Quit(1)
    ///
    /// 결과파일 = env WM_BOOT_SMOKE_RESULT || persistentDataPath/wm_boot_smoke_result.txt.
    /// key=value 라인: result / reason / worldReady / nre / frame / t.
    /// </summary>
    public sealed class BootSmokeSentinel : MonoBehaviour
    {
        private static bool _installed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void Install()
        {
            // 비결정 = inert (게임 정상 흐름 0 영향).
            if (BootMode.IsDeterministic == false)
            {
                return;
            }
            if (_installed)
            {
                return;
            }
            _installed = true;

            GameObject host = new GameObject(nameof(BootSmokeSentinel));
            DontDestroyOnLoad(host);
            host.hideFlags = HideFlags.HideAndDontSave;
            host.AddComponent<BootSmokeSentinel>();
        }

        private int _nreCount;
        private bool _done;

        private float TimeoutSec
        {
            get
            {
                string raw = Environment.GetEnvironmentVariable("WM_BOOT_SMOKE_TIMEOUT_SEC");
                if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) && v > 0f)
                {
                    return v;
                }
                return 120f;
            }
        }

        private int SettleFrames
        {
            get
            {
                string raw = Environment.GetEnvironmentVariable("WM_BOOT_SMOKE_SETTLE_FRAMES");
                if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) && v >= 0)
                {
                    return v;
                }
                return 180;
            }
        }

        private static string ResultPath
        {
            get
            {
                string env = Environment.GetEnvironmentVariable("WM_BOOT_SMOKE_RESULT");
                if (string.IsNullOrEmpty(env) == false)
                {
                    return env;
                }
                return Path.Combine(UnityEngine.Application.persistentDataPath, "wm_boot_smoke_result.txt");
            }
        }

        private void Awake()
        {
            UnityEngine.Application.logMessageReceived += OnLog;
            BootObserver.OnBootComplete += OnBootComplete;
            Debug.Log($"[BOOT-SMOKE] sentinel armed — timeout={TimeoutSec}s settle={SettleFrames}f "
                + $"result='{ResultPath}'");
            StartCoroutine(TimeoutWatch());

            // 설치 시점에 이미 WorldReady 면(센티넬보다 부팅이 빠른 경우) 즉시 처리.
            if (BootObserver.ReachedWorld)
            {
                OnBootComplete();
            }
        }

        private void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Exception && condition != null
                && condition.IndexOf("NullReferenceException", StringComparison.Ordinal) >= 0)
            {
                _nreCount++;
            }
        }

        private void OnBootComplete()
        {
            if (_done)
            {
                return;
            }
            _done = true;
            StartCoroutine(SettleThenPass());
        }

        private IEnumerator SettleThenPass()
        {
            int frames = SettleFrames;
            for (int i = 0; i < frames; i++)
            {
                yield return null;
            }

            bool pass = _nreCount == 0;
            WriteResult(
                pass ? "PASS" : "FAIL",
                pass ? "worldReady + nre0" : $"worldReady but nre={_nreCount}",
                true);
            Finish(pass ? 0 : 1);
        }

        private IEnumerator TimeoutWatch()
        {
            float deadline = Time.realtimeSinceStartup + TimeoutSec;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (_done)
                {
                    yield break;
                }
                yield return null;
            }

            if (_done)
            {
                yield break;
            }
            _done = true;
            WriteResult("FAIL", $"timeout {TimeoutSec}s — WorldReady 미도달 (last phase {BootObserver.Current})",
                BootObserver.ReachedWorld);
            Finish(1);
        }

        private void WriteResult(string result, string reason, bool worldReady)
        {
            string body =
                $"result={result}\n" +
                $"reason={reason}\n" +
                $"worldReady={(worldReady ? "true" : "false")}\n" +
                $"nre={_nreCount}\n" +
                $"frame={Time.frameCount}\n" +
                $"t={Time.realtimeSinceStartup.ToString("F1", CultureInfo.InvariantCulture)}\n";
            try
            {
                string path = ResultPath;
                string dir = Path.GetDirectoryName(path);
                if (string.IsNullOrEmpty(dir) == false && Directory.Exists(dir) == false)
                {
                    Directory.CreateDirectory(dir);
                }
                File.WriteAllText(path, body);
                Debug.Log($"[BOOT-SMOKE] {result} — {reason} (nre={_nreCount}) → {path}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BOOT-SMOKE] 결과파일 기록 실패: {ex.Message}\n{body}");
            }
        }

        private void Finish(int exitCode)
        {
            UnityEngine.Application.logMessageReceived -= OnLog;
            BootObserver.OnBootComplete -= OnBootComplete;
#if UNITY_EDITOR
            // 에디터(BootModeEditorOverride 토글 검증)에선 Quit 대신 Play 정지.
            Debug.Log($"[BOOT-SMOKE] (editor) exitCode={exitCode} — Play 정지");
            UnityEditor.EditorApplication.isPlaying = false;
#else
            UnityEngine.Application.Quit(exitCode);
#endif
        }
    }
}
