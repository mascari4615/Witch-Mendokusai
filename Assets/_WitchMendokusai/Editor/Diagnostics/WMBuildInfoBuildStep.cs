using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using Debug = UnityEngine.Debug;

namespace WitchMendokusai.EditorTools
{
    /// <summary>
    /// TASK-WM-201 — 빌드 직전에 「이 빌드가 무엇인지」를 산출물 안으로 굽는다.
    ///
    /// 폰에 여러 개를 깔아 놓고 테스트하면 화면만 봐선 어느 것이 어느 커밋인지 알 수 없다.
    /// 그래서 커밋·가지·구운 시각·CI 실행번호·더러움 여부를 <c>Resources/BuildInfo.json</c> 으로
    /// 굽고, 런타임의 <see cref="BuildInfo"/> 가 그것만 읽는다(문자열 파싱 X, 정본 1개).
    ///
    /// 빌드가 끝나면 지운다 — 작업 폴더에 남으면 *에디터에서 옛 빌드 정보가 보이는* 거짓말이 된다.
    /// 빌드 스크립트(`Editor/Build/`, TASK-WM-197 소유)를 안 건드리려고 빌드 콜백에 붙였고,
    /// 그 덕에 메뉴로 누른 빌드에도 똑같이 걸린다.
    /// </summary>
    public sealed class WMBuildInfoBuildStep : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        internal const string ASSET_PATH = "Assets/_WitchMendokusai/Core/Resources/BuildInfo.json";
        private const int GIT_TIMEOUT_MS = 10_000;

        public int callbackOrder => 0;

        /// <summary>
        /// 빌드 도중 에셋을 새로 심는 일이 유니티를 네이티브로 죽이는지 가리는 스위치
        /// (2026-08-06, 안드로이드 빌드 4연속 크래시 — 죽는 자리가 매번 임포트 직후였다).
        /// `WM_DIAG_BUILD_STEPS=0` 이면 이 단계를 통째로 건너뛴다.
        /// </summary>
        private static bool StepsDisabled()
        {
            return Environment.GetEnvironmentVariable("WM_DIAG_BUILD_STEPS") == "0";
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            if (StepsDisabled())
            {
                Debug.Log("[WM-BUILD] WM_DIAG_BUILD_STEPS=0 — 진단 자산 심기 건너뜀");
                return;
            }
            // CI 가 유니티 실행 *전에* 써 둔 파일이 있으면 그대로 쓴다 — 빌드 도중 에셋을
            // 새로 심는 행위가 안드로이드 빌드를 네이티브로 죽였다(2026-08-06, 4연속 실측).
            if (File.Exists(Path.GetFullPath(ASSET_PATH)))
            {
                Debug.Log("[WM-BUILD] 빌드 정보가 이미 있다 — 빌드 중 임포트 생략(크래시 회피)");
                return;
            }

            bool development = (report.summary.options & BuildOptions.Development) != 0;
            BuildInfo info = Collect(development, report.summary.platform.ToString());

            string fullPath = Path.GetFullPath(ASSET_PATH);
            string directory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrEmpty(directory) == false && Directory.Exists(directory) == false)
            {
                Directory.CreateDirectory(directory);
            }
            File.WriteAllText(fullPath, UnityEngine.JsonUtility.ToJson(info, true));
            AssetDatabase.ImportAsset(ASSET_PATH, ImportAssetOptions.ForceSynchronousImport);

            Debug.Log($"[WM-BUILD] 빌드 정보 새김 — {info.CollapsedLine()} / {info.builtAtKst} KST"
                + (info.dirty ? " ⚠ 커밋 안 된 변경 포함" : string.Empty));
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            Remove("빌드 종료");
        }

        /// <summary>빌드가 중간에 죽었을 때의 그물 — 남아 있으면 에디터가 옛 빌드인 척하게 된다.</summary>
        [InitializeOnLoadMethod]
        private static void CleanupOnEditorLoad()
        {
            if (File.Exists(Path.GetFullPath(ASSET_PATH)))
            {
                Remove("에디터 로드 시 잔여 발견");
            }
        }

        private static void Remove(string reason)
        {
            string fullPath = Path.GetFullPath(ASSET_PATH);
            if (File.Exists(fullPath) == false)
            {
                return;
            }
            try
            {
                if (AssetDatabase.DeleteAsset(ASSET_PATH) == false && File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                }
                Debug.Log($"[WM-BUILD] 빌드 정보 파일 제거 — {reason}");
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[WM-BUILD] 빌드 정보 파일 제거 실패 — 수동으로 지워라: {ASSET_PATH}\n{exception}");
            }
        }

        public static BuildInfo Collect(bool development, string platform)
        {
            // CI 는 실행번호·주소를 환경변수로 준다. 손으로 구운 빌드는 그게 없으므로 0/빈칸.
            string runNumberRaw = Environment.GetEnvironmentVariable("GITHUB_RUN_NUMBER");
            int.TryParse(runNumberRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int runNumber);

            string repository = Environment.GetEnvironmentVariable("GITHUB_REPOSITORY");
            string runId = Environment.GetEnvironmentVariable("GITHUB_RUN_ID");
            string runUrl = string.IsNullOrEmpty(repository) || string.IsNullOrEmpty(runId)
                ? string.Empty
                : $"https://github.com/{repository}/actions/runs/{runId}";

            string commit = Git("rev-parse HEAD");
            // CI 는 detached HEAD 라 가지 이름이 안 나온다 — 그때는 CI 가 알려준 이름을 쓴다.
            string branch = Git("rev-parse --abbrev-ref HEAD");
            if (string.IsNullOrEmpty(branch) || branch == "HEAD")
            {
                branch = Environment.GetEnvironmentVariable("GITHUB_REF_NAME") ?? string.Empty;
            }
            bool dirty = string.IsNullOrEmpty(Git("status --porcelain")) == false;

            return new BuildInfo
            {
                commit = commit,
                branch = branch,
                buildNumber = runNumber,
                builtAtKst = KstNow(),
                channel = development ? "dev" : "release",
                platform = platform,
                unityVersion = UnityEngine.Application.unityVersion,
                runUrl = runUrl,
                dirty = dirty,
            };
        }

        /// <summary>빌드 시각은 늘 KST 로 적는다 — CI 러너가 UTC 라 그대로 두면 9시간 어긋나 보인다.</summary>
        public static string KstNow()
        {
            DateTime kst = DateTime.UtcNow.AddHours(9);
            return kst.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        }

        /// <summary>git 이 없거나 느려도 빌드를 막지 않는다 — 정보가 조금 비는 게 빌드 실패보다 낫다.</summary>
        private static string Git(string arguments)
        {
            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo("git", arguments)
                {
                    WorkingDirectory = Path.GetFullPath("."),
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                using (Process process = Process.Start(startInfo))
                {
                    if (process == null)
                    {
                        return string.Empty;
                    }
                    string output = process.StandardOutput.ReadToEnd();
                    if (process.WaitForExit(GIT_TIMEOUT_MS) == false)
                    {
                        process.Kill();
                        return string.Empty;
                    }
                    return process.ExitCode == 0 ? output.Trim() : string.Empty;
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[WM-BUILD] git {arguments} 실패 — 빌드 정보 일부가 빈다: {exception.Message}");
                return string.Empty;
            }
        }
    }
}
