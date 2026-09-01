using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace WitchMendokusai.EditorTools
{
    /// <summary>
    /// TASK-WM-118 I5b — 결정 부팅 standalone smoke 의 *빌드* phase.
    ///
    /// 진짜 "테스트불가 최종해소"는 에디터 서비스 상태와 무관한 헤드리스 회귀 게이트. 그 게이트는
    /// 2-phase: ① 빌드(에디터-bound, 느림, lock 필요) ② 실행(순수 헤드리스, 에디터
    /// 0 의존 = CI-able). 본 클래스 = ①. 산출 exe 를 wm-boot-smoke.ps1 이 env
    /// WM_BOOT_DETERMINISTIC=1 로 -batchmode 실행 → BootSmokeSentinel 결과파일 판정.
    ///
    /// CLI: Unity -quit -batchmode -projectPath . -executeMethod
    ///      WitchMendokusai.EditorTools.BootSmokeBuilder.BuildFromCLI
    ///      -wmSmokeOut &lt;exe 경로&gt;   (생략 = Build/Smoke/WM.exe)
    /// Development 빌드(로그/ logMessageReceived 완전 동작). 씬 = EditorBuildSettings
    /// (게임 부팅 = scene 0). batchmode 시 EditorApplication.Exit(0/1).
    /// </summary>
    public static class BootSmokeBuilder
    {
        private const string DEFAULT_REL = "Build/Smoke/WM.exe";

        [MenuItem("WM/Boot/Build Smoke Standalone")]
        private static void BuildMenu()
        {
            string outPath = Path.Combine(ProjectRoot(), DEFAULT_REL);
            bool ok = Build(outPath, out string summary);
            if (ok)
            {
                Debug.Log($"[BOOT-SMOKE-BUILD] OK → {outPath}\n{summary}");
                EditorUtility.RevealInFinder(outPath);
            }
            else
            {
                Debug.LogError($"[BOOT-SMOKE-BUILD] FAILED\n{summary}");
            }
        }

        // Unity -executeMethod 진입점 (batchmode). 종료코드로 빌드 성패 전달.
        public static void BuildFromCLI()
        {
            string outPath = ArgValue("-wmSmokeOut");
            if (string.IsNullOrEmpty(outPath))
            {
                outPath = Path.Combine(ProjectRoot(), DEFAULT_REL);
            }

            bool ok;
            string summary;
            try
            {
                ok = Build(outPath, out summary);
            }
            catch (Exception ex)
            {
                ok = false;
                summary = ex.ToString();
            }

            if (ok)
            {
                Debug.Log($"[BOOT-SMOKE-BUILD] OK → {outPath}\n{summary}");
            }
            else
            {
                Debug.LogError($"[BOOT-SMOKE-BUILD] FAILED\n{summary}");
            }

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(ok ? 0 : 1);
            }
        }

        private static bool Build(string outPath, out string summary)
        {
            string[] scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                summary = "EditorBuildSettings 에 enabled 씬 0 — 부팅 씬 리스트 없음. "
                    + "Build Settings 에 부팅 씬(scene 0)부터 등록 필요.";
                return false;
            }

            string dir = Path.GetDirectoryName(outPath);
            if (string.IsNullOrEmpty(dir) == false && Directory.Exists(dir) == false)
            {
                Directory.CreateDirectory(dir);
            }

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outPath,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                // Development = Debug.Log / logMessageReceived 완전 동작 (NRE 카운트 신뢰).
                options = BuildOptions.Development,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary s = report.summary;
            summary = $"result={s.result} scenes={scenes.Length} "
                + $"size={s.totalSize}B time={s.totalTime} "
                + $"errors={s.totalErrors} warnings={s.totalWarnings}";
            return s.result == BuildResult.Succeeded;
        }

        private static string ProjectRoot()
        {
            // Application.dataPath = <project>/Assets
            return Directory.GetParent(Application.dataPath).FullName;
        }

        private static string ArgValue(string key)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], key, StringComparison.Ordinal))
                {
                    return args[i + 1];
                }
            }
            return null;
        }
    }
}
