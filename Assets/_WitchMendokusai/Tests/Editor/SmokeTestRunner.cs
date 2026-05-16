using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace WitchMendokusai.Tests
{
    /// <summary>
    /// TASK-WM-117 — 라이브 Editor 의 Unity TestRunnerApi 직접 호출로 PlayMode smoke 구동.
    ///
    /// MCP-for-Unity TestJobManager 는 하드 15s init-cap 이라 이 프로젝트 PlayMode
    /// 진입(WM.Domain 헤비 deps)을 못 기다림 = run_tests 비호환. 본 헬퍼는 Unity 자체
    /// 러너(그 cap 없음)를 직접 쓰고, 콜백/api 를 static 으로 잡아 async run 동안 생존.
    /// 결과는 유니크 prefix `[WM117-SMOKE]` 로 Console emit → MCP read_console 로 수거.
    ///
    /// 트리거: MCP execute_code 로 `WitchMendokusai.Tests.SmokeTestRunner.RunPlayModeSmoke()`.
    /// (batchmode CLI = 별 durable 경로, MCP Unity 프로젝트 락 때문에 in-situ 불가.)
    /// </summary>
    public static class SmokeTestRunner
    {
        // static = execute_code 스코프 종료 후에도 async TestRunner run 동안 GC 회피.
        private static TestRunnerApi _api;
        private static Callbacks _callbacks;

        [MenuItem("WM/Tests/Run PlayMode Smoke")]
        public static void RunPlayModeSmoke()
        {
            _api = ScriptableObject.CreateInstance<TestRunnerApi>();
            _callbacks = new Callbacks();
            _api.RegisterCallbacks(_callbacks);

            Filter filter = new Filter
            {
                testMode = TestMode.PlayMode,
                assemblyNames = new[] { "WM.Tests.PlayMode" }
            };
            Debug.Log("[WM117-SMOKE] EXECUTE requested (PlayMode, WM.Tests.PlayMode)");
            _api.Execute(new ExecutionSettings(filter));
        }

        private sealed class Callbacks : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun)
            {
                Debug.Log("[WM117-SMOKE] RUN-STARTED count=" + (testsToRun != null ? testsToRun.TestCaseCount : -1));
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                Debug.Log("[WM117-SMOKE] RUN-FINISHED result=" + result.TestStatus
                    + " passed=" + result.PassCount
                    + " failed=" + result.FailCount
                    + " skipped=" + result.SkipCount
                    + " duration=" + result.Duration.ToString("F1") + "s");
                if (_api != null && _callbacks != null)
                {
                    _api.UnregisterCallbacks(_callbacks);
                }
            }

            public void TestStarted(ITestAdaptor test) { }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (result.Test.IsSuite)
                {
                    return;
                }
                string msg = "[WM117-SMOKE] TEST " + result.Test.Name + " = " + result.TestStatus;
                if (result.TestStatus != TestStatus.Passed)
                {
                    msg += " | " + result.Message;
                }
                Debug.Log(msg);
            }
        }
    }
}
