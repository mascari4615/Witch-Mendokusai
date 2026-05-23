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
        private string _scenarioResult;
        private string _scenarioReason;
        // TASK-WM-134 — 삭제된 평행 표면(BootCoreFlowSmokeTest) 흡수. probe 도달
        // 전(timeout/nre-fail) = "NONE"(미평가), 도달 후 = "PASS" 또는 실패 사유.
        private string _bootInvariants = "NONE";

        private static BootSmokeSentinel _inst;

        // ── TASK-WM-117 Tier-B 시나리오 seam ──────────────────────────────
        // 부팅(WM-118 I5)을 *게임로직 회귀*로 확장하는 최소 정적 핸드오프.
        // 인터페이스 X (1 시나리오 speculative interface = 데드 인터페이스;
        // 후속 Effect/전투 시나리오도 같은 delegate set = 더 lean root).
        //
        // 계약: 시나리오는 결정 모드 + 자기 env 일 때만 self-install 하여
        //   ScenarioRoutine/ScenarioName 을 set. WorldReady+settle+부팅 nre0
        //   이면 센티넬이 그 coroutine 을 1회 구동, 시나리오는 *마지막에*
        //   ReportScenario(pass,reason) 1회 호출 후 yield break. WriteResult/
        //   Quit 은 센티넬 단독 권위(이중 종료 0). 모든 대기는 시나리오 내부
        //   realtime-deadline 으로 self-bound (hang 0). 미등록 = 기존 부팅
        //   PASS 경로 완전 무변경(Tier-A 회귀망 0 영향).
        public static Func<IEnumerator> ScenarioRoutine;
        public static string ScenarioName;

        /// <summary>시나리오가 NRE delta 측정에 쓰는 전역 누적 NRE 수.</summary>
        public static int CurrentNreCount => _inst != null ? _inst._nreCount : 0;

        /// <summary>시나리오 종료 시 1회 호출(기록만 — 종료/판정은 센티넬).</summary>
        public static void ReportScenario(bool pass, string reason)
        {
            if (_inst == null)
            {
                return;
            }
            _inst._scenarioResult = pass ? "PASS" : "FAIL";
            _inst._scenarioReason = reason ?? string.Empty;
            Debug.Log($"[BOOT-SMOKE] scenario report: {(pass ? "PASS" : "FAIL")} — {reason}");
        }

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
            _inst = this;
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

            // 부팅 자체 회귀(nre>0) = 게임로직 시나리오 무의미 → 부팅 판정 종료
            // (시나리오 스킵). 부팅이 깨졌는데 던전 구동은 의미 없음.
            if (_nreCount != 0)
            {
                WriteResult("FAIL", $"worldReady but nre={_nreCount}", true, DdolManifest());
                Finish(1);
                yield break;
            }

            // TASK-WM-134 — 부팅 불변식 probe (삭제된 BootCoreFlowSmokeTest
            // 의 고유 회귀망 흡수). ddol baseline = DDOL 매니저 *존재*만 →
            // 플레이어 실제 스폰·바인드(WM-115 R3a/R5)는 별개 distinct
            // 회귀 클래스라 여기서 1회 결정 검증. UIManager/DungeonManager
            // presence = WorldReady 에 implied(부재면 NRE/조립실패→미도달)
            // → over-gate X (measured distinct 만, data-gate 정합).
            string invariantFail = BootInvariantFailure();
            if (invariantFail != null)
            {
                _bootInvariants = invariantFail;
                WriteResult("FAIL", $"worldReady but bootInvariant: {invariantFail}", true, DdolManifest());
                Finish(1);
                yield break;
            }
            _bootInvariants = "PASS";

            // 시나리오 미등록 = 기존 부팅 PASS 경로 (TASK-WM-118 I5 / Tier-A
            // 회귀망 100% 무변경 — WM_BOOT_SCENARIO 미설정 시 여기로).
            if (ScenarioRoutine == null)
            {
                WriteResult("PASS", "worldReady + nre0", true, DdolManifest());
                Finish(0);
                yield break;
            }

            // 시나리오 등록 = 부팅 OK 후 게임로직 회귀 구동 (TASK-WM-117 Tier-B).
            // 시나리오 내부 대기는 전부 realtime-deadline self-bound → hang 0.
            Debug.Log($"[BOOT-SMOKE] 부팅 OK(nre0) — scenario '{ScenarioName}' 구동");
            yield return StartCoroutine(ScenarioRoutine());

            if (_scenarioResult == null)
            {
                _scenarioResult = "FAIL";
                _scenarioReason = "scenario completed without ReportScenario";
            }
            // 시나리오 중 발생 NRE 도 전역 _nreCount 에 누적 → 종합 판정에 반영.
            bool scenarioPass = string.Equals(_scenarioResult, "PASS", StringComparison.Ordinal);
            bool overallPass = scenarioPass && _nreCount == 0;
            WriteResult(
                overallPass ? "PASS" : "FAIL",
                overallPass
                    ? $"worldReady + nre0 + scenario({ScenarioName}) PASS"
                    : $"scenario({ScenarioName}) {_scenarioResult} nre={_nreCount} — {_scenarioReason}",
                true,
                DdolManifest());
            Finish(overallPass ? 0 : 1);
        }

        /// <summary>
        /// TASK-WM-118 broader-A unblocker — WorldReady 시점 DontDestroyOnLoad
        /// 루트 GameObject 매니페스트(정렬·결정적). eager-resolve 매니저는
        /// .DontDestroyOnLoad() 라 여기 나타남 = "Awake 실행됨(활성)"의 헤드리스
        /// 관측가능 신호. broader-A 가 eager 제거 시 매니저 활성을 떨구면(NRE
        /// 아닌 무음 회귀) 이 매니페스트 diff 로 포착 — 스크린샷/타입하드코딩 X.
        /// </summary>
        private static string DdolManifest()
        {
            try
            {
                GameObject probe = new GameObject("__ddol_probe__");
                DontDestroyOnLoad(probe);
                UnityEngine.SceneManagement.Scene ddol = probe.scene;
                GameObject[] roots = ddol.GetRootGameObjects();
                System.Collections.Generic.List<string> names = new System.Collections.Generic.List<string>(roots.Length);
                foreach (GameObject go in roots)
                {
                    if (go != probe)
                    {
                        names.Add(go.name);
                    }
                }
                Destroy(probe);
                names.Sort(System.StringComparer.Ordinal);
                return names.Count + ":" + string.Join("|", names);
            }
            catch (Exception ex)
            {
                return "ERR:" + ex.GetType().Name;
            }
        }

        /// <summary>
        /// TASK-WM-134 — WorldReady+settle+nre0 후 1회 결정 불변식 검증.
        /// 삭제된 BootCoreFlowSmokeTest 의 유일 distinct 커버리지(플레이어
        /// 실제 스폰·바인드 = WM-115 R3a/R5)를 substrate 로 흡수. null = PASS.
        /// </summary>
        private static string BootInvariantFailure()
        {
            PlayerProvider playerProvider =
                UnityEngine.Object.FindAnyObjectByType<PlayerProvider>(FindObjectsInactive.Include);
            if (playerProvider == null)
            {
                return "PlayerProvider 부재 (DI/부팅 와이어 회귀)";
            }
            if (playerProvider.CurrentObject == null)
            {
                return "playerProvider.CurrentObject==null — 플레이어 미스폰/미바인드 (WM-115 R3a/R5)";
            }
            return null;
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
                BootObserver.ReachedWorld, DdolManifest());
            Finish(1);
        }

        private void WriteResult(string result, string reason, bool worldReady, string ddol)
        {
            string body =
                $"result={result}\n" +
                $"reason={reason}\n" +
                $"worldReady={(worldReady ? "true" : "false")}\n" +
                $"nre={_nreCount}\n" +
                $"bootInvariants={_bootInvariants}\n" +
                $"frame={Time.frameCount}\n" +
                $"t={Time.realtimeSinceStartup.ToString("F1", CultureInfo.InvariantCulture)}\n" +
                $"ddol={ddol}\n" +
                $"scenario={ScenarioName ?? "none"}\n" +
                $"scenarioResult={_scenarioResult ?? "NONE"}\n" +
                $"scenarioReason={_scenarioReason ?? string.Empty}\n";
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
