using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace WitchMendokusai.EditorTools
{
    /// <summary>
    /// TASK-WM-195 — 노트북 빌드머신용 플레이어 빌드 CLI 진입점.
    ///
    /// 데스크톱에서 빌드를 돌리면 작업이 막히고 성능을 다 먹으므로, 빌드는 노트북
    /// self-hosted runner (masca 사용자 세션) 에서 돈다. 본 클래스가 그 runner 가
    /// 호출하는 유일한 진입점이다.
    ///
    /// <see cref="BootSmokeBuilder"/> 와 역할 분리: 저쪽은 *부팅 회귀 게이트*용
    /// Development 스모크 exe, 이쪽은 *배포용 플레이어*.
    ///
    /// CLI:
    ///   Unity -quit -batchmode -nographics -projectPath . -logFile &lt;log&gt;
    ///         -executeMethod WitchMendokusai.EditorTools.WMBuilder.BuildFromCLI
    ///         -wmOut &lt;exe 경로&gt;      (생략 = Build/Player/WitchMendokusai.exe)
    ///         -wmDev                   (박히면 Development 빌드)
    ///         -wmVersion 0.1.2         (생략 = ProjectSettings 값 유지)
    ///         -wmReport &lt;json 경로&gt;  (빌드 요약 JSON — CI 가 파싱)
    ///
    /// 종료코드 = 빌드 성패 (0/1). batchmode 가 아니면 Exit 하지 않는다.
    /// </summary>
    public static class WMBuilder
    {
        private const string DEFAULT_REL = "Build/Player/WitchMendokusai.exe";

        [MenuItem("WM/Build/Player (Release)")]
        private static void BuildReleaseMenu()
        {
            RunMenuBuild(false);
        }

        [MenuItem("WM/Build/Player (Development)")]
        private static void BuildDevelopmentMenu()
        {
            RunMenuBuild(true);
        }

        private static void RunMenuBuild(bool development)
        {
            string outPath = Path.Combine(ProjectRoot(), DEFAULT_REL);
            BuildSummary summary = Build(outPath, development, out string detail);
            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[WM-BUILD] OK → {outPath}\n{detail}");
                EditorUtility.RevealInFinder(outPath);
            }
            else
            {
                Debug.LogError($"[WM-BUILD] FAILED\n{detail}");
            }
        }

        // Unity -executeMethod 진입점 (batchmode). 종료코드로 빌드 성패 전달.
        public static void BuildFromCLI()
        {
            string outPath = ArgValue("-wmOut");
            if (string.IsNullOrEmpty(outPath))
            {
                outPath = Path.Combine(ProjectRoot(), DEFAULT_REL);
            }

            bool development = HasFlag("-wmDev");
            string version = ArgValue("-wmVersion");
            string reportPath = ArgValue("-wmReport");

            if (string.IsNullOrEmpty(version) == false)
            {
                PlayerSettings.bundleVersion = version;
                Debug.Log($"[WM-BUILD] bundleVersion → {version}");
            }

            BuildSummary summary;
            string detail;
            bool succeeded;
            try
            {
                summary = Build(outPath, development, out detail);
                succeeded = summary.result == BuildResult.Succeeded;
            }
            catch (Exception exception)
            {
                summary = default;
                detail = exception.ToString();
                succeeded = false;
            }

            if (succeeded)
            {
                Debug.Log($"[WM-BUILD] OK → {outPath}\n{detail}");
            }
            else
            {
                Debug.LogError($"[WM-BUILD] FAILED\n{detail}");
            }

            if (string.IsNullOrEmpty(reportPath) == false)
            {
                WriteReport(reportPath, outPath, development, summary, succeeded);
            }

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(succeeded ? 0 : 1);
            }
        }

        private static BuildSummary Build(string outPath, bool development, out string detail)
        {
            string[] scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                detail = "EditorBuildSettings 에 enabled 씬 0 — 부팅 씬(scene 0)부터 등록 필요.";
                return default;
            }

            string directory = Path.GetDirectoryName(outPath);
            if (string.IsNullOrEmpty(directory) == false && Directory.Exists(directory) == false)
            {
                Directory.CreateDirectory(directory);
            }

            BuildOptions options = BuildOptions.None;
            if (development)
            {
                options |= BuildOptions.Development;
            }

            BuildPlayerOptions playerOptions = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outPath,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = options,
            };

            BuildReport report = BuildPipeline.BuildPlayer(playerOptions);
            BuildSummary summary = report.summary;

            StringBuilder builder = new StringBuilder();
            builder.AppendLine($"result={summary.result} scenes={scenes.Length} dev={development}");
            builder.AppendLine($"version={PlayerSettings.bundleVersion} size={summary.totalSize}B time={summary.totalTime}");
            builder.AppendLine($"errors={summary.totalErrors} warnings={summary.totalWarnings}");
            AppendFailedSteps(builder, report);
            detail = builder.ToString();
            return summary;
        }

        // 실패 시 어느 step 의 어떤 메시지인지 로그에 남긴다 (CI 로그만 보고 원인 추적).
        private static void AppendFailedSteps(StringBuilder builder, BuildReport report)
        {
            foreach (BuildStep step in report.steps)
            {
                List<BuildStepMessage> problems = step.messages
                    .Where(message => message.type == LogType.Error || message.type == LogType.Exception)
                    .ToList();
                if (problems.Count == 0)
                {
                    continue;
                }

                builder.AppendLine($"--- step: {step.name} ---");
                foreach (BuildStepMessage problem in problems)
                {
                    builder.AppendLine($"  [{problem.type}] {problem.content}");
                }
            }
        }

        private static void WriteReport(string reportPath, string outPath, bool development, BuildSummary summary, bool succeeded)
        {
            try
            {
                string directory = Path.GetDirectoryName(reportPath);
                if (string.IsNullOrEmpty(directory) == false && Directory.Exists(directory) == false)
                {
                    Directory.CreateDirectory(directory);
                }

                long sizeBytes = 0;
                if (File.Exists(outPath))
                {
                    sizeBytes = new FileInfo(outPath).Length;
                }

                BuildReportJson json = new BuildReportJson
                {
                    succeeded = succeeded,
                    result = summary.result.ToString(),
                    outPath = outPath.Replace('\\', '/'),
                    version = PlayerSettings.bundleVersion,
                    development = development,
                    totalSizeBytes = (long)summary.totalSize,
                    exeSizeBytes = sizeBytes,
                    totalSeconds = summary.totalTime.TotalSeconds,
                    errors = (int)summary.totalErrors,
                    warnings = (int)summary.totalWarnings,
                    finishedAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                };

                File.WriteAllText(reportPath, JsonUtility.ToJson(json, true), Encoding.UTF8);
                Debug.Log($"[WM-BUILD] report → {reportPath}");
            }
            catch (Exception exception)
            {
                // 리포트 실패가 빌드 성패를 뒤집지 않는다 — 로그만 남긴다.
                Debug.LogWarning($"[WM-BUILD] report 쓰기 실패: {exception.Message}");
            }
        }

        [Serializable]
        private struct BuildReportJson
        {
            public bool succeeded;
            public string result;
            public string outPath;
            public string version;
            public bool development;
            public long totalSizeBytes;
            public long exeSizeBytes;
            public double totalSeconds;
            public int errors;
            public int warnings;
            public string finishedAtUtc;
        }

        private static string ProjectRoot()
        {
            // Application.dataPath = <project>/Assets
            return Directory.GetParent(Application.dataPath).FullName;
        }

        private static bool HasFlag(string key)
        {
            return Environment.GetCommandLineArgs()
                .Any(argument => string.Equals(argument, key, StringComparison.Ordinal));
        }

        private static string ArgValue(string key)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int i = 0; i < arguments.Length - 1; i++)
            {
                if (string.Equals(arguments[i], key, StringComparison.Ordinal))
                {
                    return arguments[i + 1];
                }
            }
            return null;
        }
    }
}
