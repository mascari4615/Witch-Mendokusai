using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace WitchMendokusai.EditorTools
{
    /// <summary>
    /// TASK-WM-201 — 기기 로그 서버 토큰을 *빌드 산출물에만* 심는다.
    ///
    /// WM 은 공개 저장소라 토큰을 에셋으로 커밋할 수 없다. 그렇다고 APK 안에 없으면 서버가
    /// 401 로 막아 폰 로그가 영영 안 온다. 그래서 **빌드 직전에 환경변수에서 받아 임시 파일로
    /// 쓰고, 빌드가 끝나면 지운다.**
    ///
    /// 빌드 스크립트(`Editor/Build/`)를 고치지 않고 유니티 빌드 콜백에 붙는 이유: 그 폴더는
    /// 다른 작업(TASK-WM-197)이 쥐고 있고, 무엇보다 *메뉴로 누른 빌드*에도 똑같이 걸려야 한다.
    ///
    /// 「빌드는 프로젝트 상태를 바꾸지 않는다」 원칙의 예외이며, 그 값을 치르는 조건은 셋:
    /// ① 파일은 gitignore ② 빌드 후·에디터 로드 시 양쪽에서 청소 ③ 심고 지운 것을 로그로 남김.
    ///
    /// 환경변수 `WM_DEVICE_LOG_TOKEN` 이 없으면 아무 일도 하지 않는다 — 토큰 없는 빌드는
    /// 로그를 못 보낼 뿐, 빌드 자체는 정상이다.
    /// </summary>
    public sealed class WMDeviceLogTokenBuildStep : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        private const string TOKEN_ENV = "WM_DEVICE_LOG_TOKEN";
        internal const string ASSET_PATH = "Assets/_WitchMendokusai/Core/Resources/DeviceLogToken.txt";

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
            if (File.Exists(Path.GetFullPath(ASSET_PATH)))
            {
                Debug.Log("[WM-BUILD] 토큰이 이미 심겨 있다 — 빌드 중 임포트 생략(크래시 회피)");
                return;
            }

            string token = Environment.GetEnvironmentVariable(TOKEN_ENV);
            if (string.IsNullOrEmpty(token))
            {
                Debug.Log($"[WM-BUILD] {TOKEN_ENV} 없음 — 기기 로그 토큰 없이 빌드한다 "
                    + "(서버가 토큰을 요구하면 폰 로그는 401 로 막힌다).");
                return;
            }

            string fullPath = Path.GetFullPath(ASSET_PATH);
            string directory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrEmpty(directory) == false && Directory.Exists(directory) == false)
            {
                Directory.CreateDirectory(directory);
            }
            File.WriteAllText(fullPath, token.Trim());
            AssetDatabase.ImportAsset(ASSET_PATH, ImportAssetOptions.ForceSynchronousImport);
            Debug.Log($"[WM-BUILD] 기기 로그 토큰 심음 → {ASSET_PATH} (빌드 후 삭제)");
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            Remove("빌드 종료");
        }

        /// <summary>빌드가 중간에 죽어 후처리가 안 불렸을 경우의 그물 — 다음 에디터 로드 때 지운다.</summary>
        [InitializeOnLoadMethod]
        private static void CleanupOnEditorLoad()
        {
            // 배치(빌드 머신)에선 지우면 안 된다 — CI 가 유니티 실행 *전에* 심어둔 파일이라
            // 여기서 청소하면 빌드가 그 자산 없이 나간다(무음 유실). 사람 에디터에서만 청소.
            if (UnityEngine.Application.isBatchMode)
            {
                return;
            }
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
                Debug.Log($"[WM-BUILD] 기기 로그 토큰 제거 — {reason}");
            }
            catch (Exception exception)
            {
                // 삼키면 비밀이 조용히 작업 폴더에 남는다.
                Debug.LogError($"[WM-BUILD] 기기 로그 토큰 제거 실패 — 수동으로 지워라: {ASSET_PATH}\n{exception}");
            }
        }
    }
}
